using Bi.Domain.Entities;
using Bi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace Bi.Application.Services;

/// <summary>
/// 文档后台处理服务
/// 异步处理文档队列：解析、分块、向量化
/// 支持批量处理和断点续传
/// </summary>
public class DocumentProcessingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentProcessingService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(5);  // 检查间隔
    private const int BatchSize = 5;  // 每批处理的分块数量
    private const int MaxRetries = 3;  // 单个分块最大重试次数
    private const int RetryDelayMs = 2000;  // 重试延迟（毫秒）

    public DocumentProcessingService(
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentProcessingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("文档处理后台服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingDocumentsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理文档时发生错误");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("文档处理后台服务已停止");
    }

    /// <summary>
    /// 处理待处理的文档
    /// 支持断点续传：从已处理的分块位置继续
    /// </summary>
    private async Task ProcessPendingDocumentsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BiDbContext>();
        var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
        var textChunker = scope.ServiceProvider.GetRequiredService<ITextChunkerService>();

        // 获取待处理的文档（pending状态且有原始内容，或processing状态需要恢复）
        var pendingDoc = await context.KnowledgeDocuments
            .Where(d => (d.Status == "pending" || d.Status == "processing") && d.RawContent != null)
            .OrderBy(d => d.Status == "processing" ? 0 : 1)  // 优先处理中断的文档
            .ThenBy(d => d.CreatedAt)
            .FirstOrDefaultAsync(stoppingToken);

        if (pendingDoc == null) return;

        // 检查是否是恢复处理
        var isResume = pendingDoc.Status == "processing";
        var startIndex = isResume ? pendingDoc.ProcessedChunkCount : 0;

        _logger.LogInformation("{Action}文档: {Id} - {Title}，从索引 {StartIndex} 开始",
            isResume ? "恢复处理" : "开始处理",
            pendingDoc.Id, pendingDoc.Title, startIndex);

        try
        {
            // 更新状态为处理中
            pendingDoc.Status = "processing";
            pendingDoc.ErrorMessage = null;  // 清除之前的错误信息
            pendingDoc.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(stoppingToken);

            // 分块（使用较小的分块大小以避免超出embedding模型上下文限制）
            var options = new ChunkOptions
            {
                ChunkSize = 350,      // 减小分块大小，约350字符 ≈ 250 tokens
                ChunkOverlap = 50,    // 重叠50字符保证上下文连续性
                Strategy = ChunkStrategy.Paragraph
            };
            var chunks = textChunker.ChunkText(pendingDoc.RawContent!, options);
            pendingDoc.ChunkCount = chunks.Count;
            await context.SaveChangesAsync(stoppingToken);

            _logger.LogInformation("文档 {Id} 分块完成，共 {Count} 个分块，从 {Start} 开始处理",
                pendingDoc.Id, chunks.Count, startIndex);

            // 从断点继续处理
            for (int i = startIndex; i < chunks.Count; i++)
            {
                if (stoppingToken.IsCancellationRequested) break;

                var chunk = chunks[i];
                float[]? embedding = null;

                // 带重试的向量化
                for (int retry = 0; retry < MaxRetries; retry++)
                {
                    try
                    {
                        embedding = await embeddingService.GetEmbeddingAsync(chunk.Content);
                        break;  // 成功则跳出重试循环
                    }
                    catch (Exception ex) when (retry < MaxRetries - 1)
                    {
                        _logger.LogWarning(ex, "分块 {Index} 向量化失败，第 {Retry} 次重试",
                            chunk.Index, retry + 1);
                        await Task.Delay(RetryDelayMs, stoppingToken);
                    }
                }

                // 如果所有重试都失败，使用空向量继续（不阻塞整体处理）
                if (embedding == null)
                {
                    _logger.LogWarning("分块 {Index} 向量化最终失败，使用空向量继续", chunk.Index);
                    embedding = Array.Empty<float>();
                }

                var chunkEntity = new KnowledgeChunk
                {
                    DocumentId = pendingDoc.Id,
                    ChunkIndex = chunk.Index,
                    Content = chunk.Content,
                    ContentLength = chunk.Length,
                    PageNumber = chunk.PageNumber,
                    SectionTitle = chunk.SectionTitle,
                    Embedding = embedding.Length > 0 ? new Vector(embedding) : null,
                    CreatedAt = DateTime.UtcNow
                };
                context.KnowledgeChunks.Add(chunkEntity);

                // 更新进度
                pendingDoc.ProcessedChunkCount = i + 1;
                pendingDoc.ProcessProgress = (int)((i + 1) * 100.0 / chunks.Count);
                pendingDoc.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(stoppingToken);

                // 每处理10个分块记录一次日志
                if ((i + 1) % 10 == 0 || i == chunks.Count - 1)
                {
                    _logger.LogInformation("文档 {Id} 处理进度: {Progress}% ({Processed}/{Total})",
                        pendingDoc.Id, pendingDoc.ProcessProgress, i + 1, chunks.Count);
                }
            }

            // 完成处理
            pendingDoc.Status = "completed";
            pendingDoc.ProcessProgress = 100;
            pendingDoc.RawContent = null;  // 清空原始内容节省空间
            pendingDoc.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(stoppingToken);

            _logger.LogInformation("文档 {Id} 处理完成，共 {Count} 个分块", pendingDoc.Id, chunks.Count);
        }
        catch (OperationCanceledException)
        {
            // 取消操作，保持processing状态以便下次恢复
            _logger.LogInformation("文档 {Id} 处理被取消，将在下次启动时恢复", pendingDoc.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文档 {Id} 处理失败", pendingDoc.Id);
            pendingDoc.Status = "failed";
            pendingDoc.ErrorMessage = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            pendingDoc.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(stoppingToken);
        }
    }
}


using System.Text.Json;
using Bi.Domain.Entities;
using Bi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bi.Application.Services;

/// <summary>
/// 指标检索服务实现（使用JSON格式存储向量，兼容无pgvector环境）
/// </summary>
public class KpiRetrieverService : IKpiRetrieverService
{
    private readonly BiDbContext _db;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<KpiRetrieverService> _logger;

    public KpiRetrieverService(
        BiDbContext db,
        IEmbeddingService embeddingService,
        ILogger<KpiRetrieverService> logger)
    {
        _db = db;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    /// <summary>
    /// 根据自然语言查询检索相关指标
    /// </summary>
    public async Task<List<KpiSearchResult>> SearchAsync(string query, int topK = 5, double minScore = 0.5)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<KpiSearchResult>();

        try
        {
            // 1. 生成查询向量
            var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query);

            // 2. 获取所有有向量的指标（在内存中计算相似度）
            var kpis = await _db.KpiDefinitions
                .Where(k => k.IsEnabled && k.EmbeddingJson != null)
                .ToListAsync();

            // 3. 计算余弦相似度并排序
            var results = kpis
                .Select(k => new
                {
                    Kpi = k,
                    Score = CalculateCosineSimilarity(queryEmbedding, ParseEmbedding(k.EmbeddingJson))
                })
                .Where(r => r.Score >= minScore)
                .OrderByDescending(r => r.Score)
                .Take(topK)
                .Select(r => new KpiSearchResult
                {
                    Kpi = r.Kpi,
                    Score = r.Score
                })
                .ToList();

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "指标检索失败: {Query}", query);
            return new List<KpiSearchResult>();
        }
    }

    /// <summary>
    /// 为单个指标生成向量嵌入
    /// </summary>
    public async Task GenerateEmbeddingAsync(long kpiId)
    {
        var kpi = await _db.KpiDefinitions.FindAsync(kpiId);
        if (kpi == null)
        {
            _logger.LogWarning("指标不存在: {KpiId}", kpiId);
            return;
        }

        await GenerateEmbeddingForKpiAsync(kpi);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// 批量为指标生成向量嵌入
    /// </summary>
    public async Task GenerateEmbeddingsAsync(List<long>? kpiIds = null)
    {
        IQueryable<KpiDefinition> query = _db.KpiDefinitions.Where(k => k.IsEnabled);

        if (kpiIds != null && kpiIds.Count > 0)
        {
            query = query.Where(k => kpiIds.Contains(k.Id));
        }
        else
        {
            // 只处理未生成向量的指标
            query = query.Where(k => k.EmbeddingJson == null);
        }

        var kpis = await query.ToListAsync();
        _logger.LogInformation("开始为 {Count} 个指标生成向量", kpis.Count);

        // 批量处理，每批20个
        const int batchSize = 20;
        for (int i = 0; i < kpis.Count; i += batchSize)
        {
            var batch = kpis.Skip(i).Take(batchSize).ToList();
            var texts = batch.Select(BuildKpiText).ToList();

            try
            {
                var embeddings = await _embeddingService.GetEmbeddingsAsync(texts);

                for (int j = 0; j < batch.Count && j < embeddings.Count; j++)
                {
                    batch[j].EmbeddingJson = JsonSerializer.Serialize(embeddings[j]);
                    batch[j].EmbeddingUpdatedAt = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync();
                _logger.LogInformation("已处理 {Processed}/{Total} 个指标", Math.Min(i + batchSize, kpis.Count), kpis.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量生成向量失败，批次起始: {Start}", i);
            }
        }
    }

    private async Task GenerateEmbeddingForKpiAsync(KpiDefinition kpi)
    {
        var text = BuildKpiText(kpi);
        var embedding = await _embeddingService.GetEmbeddingAsync(text);
        kpi.EmbeddingJson = JsonSerializer.Serialize(embedding);
        kpi.EmbeddingUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 构建用于向量化的指标文本
    /// </summary>
    private static string BuildKpiText(KpiDefinition kpi)
    {
        var parts = new List<string> { kpi.Name };

        if (!string.IsNullOrEmpty(kpi.Definition))
            parts.Add(kpi.Definition);
        if (!string.IsNullOrEmpty(kpi.Formula))
            parts.Add($"计算公式: {kpi.Formula}");
        if (!string.IsNullOrEmpty(kpi.Unit))
            parts.Add($"单位: {kpi.Unit}");

        return string.Join(" ", parts);
    }

    /// <summary>
    /// 解析JSON格式的向量
    /// </summary>
    private static float[] ParseEmbedding(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return Array.Empty<float>();

        try
        {
            return JsonSerializer.Deserialize<float[]>(json) ?? Array.Empty<float>();
        }
        catch
        {
            return Array.Empty<float>();
        }
    }

    /// <summary>
    /// 计算余弦相似度
    /// </summary>
    private static double CalculateCosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length)
            return 0;

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
            return 0;

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}


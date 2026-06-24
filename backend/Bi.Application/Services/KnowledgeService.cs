using System.Security.Cryptography;
using System.Text;
using Bi.Domain.Entities;
using Bi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Bi.Application.Services;

/// <summary>
/// 知识库服务实现
/// 提供文档管理、分块、向量化和检索功能
/// </summary>
public class KnowledgeService : IKnowledgeService
{
    private readonly BiDbContext _context;
    private readonly IEmbeddingService _embeddingService;
    private readonly IDocumentParserService _documentParser;
    private readonly ITextChunkerService _textChunker;
    private readonly ILogger<KnowledgeService> _logger;
    private readonly IConfigService _configService;

    // 分块配置
    private const int DefaultChunkSize = 500;      // 默认分块大小（字符数）
    private const int DefaultChunkOverlap = 50;    // 默认重叠大小

    public KnowledgeService(
        BiDbContext context,
        IEmbeddingService embeddingService,
        IDocumentParserService documentParser,
        ITextChunkerService textChunker,
        ILogger<KnowledgeService> logger,
        IConfigService configService)
    {
        _context = context;
        _embeddingService = embeddingService;
        _documentParser = documentParser;
        _textChunker = textChunker;
        _logger = logger;
        _configService = configService;
    }

    #region 分类管理
    public async Task<List<KnowledgeCategory>> GetCategoriesAsync()
    {
        return await _context.KnowledgeCategories
            .Include(c => c.Children)
            .Where(c => c.ParentId == null)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<KnowledgeCategory> CreateCategoryAsync(string name, long? parentId = null, string? description = null)
    {
        var category = new KnowledgeCategory
        {
            Name = name,
            ParentId = parentId,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
        _context.KnowledgeCategories.Add(category);
        await _context.SaveChangesAsync();
        return category;
    }

    public async Task<KnowledgeCategory> UpdateCategoryAsync(long id, string name, string? description = null)
    {
        var category = await _context.KnowledgeCategories.FindAsync(id)
            ?? throw new KeyNotFoundException($"分类不存在: {id}");
        category.Name = name;
        category.Description = description;
        await _context.SaveChangesAsync();
        return category;
    }

    public async Task DeleteCategoryAsync(long id)
    {
        var category = await _context.KnowledgeCategories
            .Include(c => c.Documents)
            .Include(c => c.Children)
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException($"分类不存在: {id}");

        if (category.Children.Any())
            throw new InvalidOperationException("请先删除子分类");
        if (category.Documents.Any())
            throw new InvalidOperationException("请先删除分类下的文档");

        _context.KnowledgeCategories.Remove(category);
        await _context.SaveChangesAsync();
    }
    #endregion

    #region 文档管理
    public async Task<(List<KnowledgeDocument> Items, int Total)> GetDocumentsAsync(
        long? categoryId = null,
        string? status = null,
        string? keyword = null,
        int page = 1,
        int pageSize = 20)
    {
        var query = _context.KnowledgeDocuments.AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(d => d.CategoryId == categoryId);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(d => d.Status == status);
        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(d => d.Title.Contains(keyword) || (d.FileName != null && d.FileName.Contains(keyword)));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(d => d.Category)
            .ToListAsync();

        return (items, total);
    }

    public async Task<KnowledgeDocument?> GetDocumentAsync(long id)
    {
        return await _context.KnowledgeDocuments
            .Include(d => d.Category)
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<KnowledgeDocument> CreateDocumentAsync(
        string title,
        string fileName,
        string fileType,
        long fileSize,
        Stream fileStream,
        long? categoryId = null,
        long? datasourceId = null,
        long? createdBy = null)
    {
        // 检查文件类型是否支持
        if (!_documentParser.IsSupported(fileType))
        {
            throw new NotSupportedException($"不支持的文件类型: {fileType}");
        }

        // 复制流以便多次读取
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        // 解析文档内容
        var parseResult = await _documentParser.ParseAsync(memoryStream, fileType);
        if (!parseResult.Success)
        {
            throw new InvalidOperationException($"文档解析失败: {parseResult.ErrorMessage}");
        }

        var content = parseResult.Content;

        // 计算内容哈希
        var contentHash = ComputeHash(content);

        // 生成文件保存的相对路径
        var relativePath = $"knowledge/{DateTime.UtcNow:yyyyMMdd}/{Guid.NewGuid()}{Path.GetExtension(fileName)}";

        // 获取完整的物理路径并保存文件
        var uploadDir = Path.Combine(AppContext.BaseDirectory, "uploads");
        var fullPath = Path.Combine(uploadDir, relativePath);
        var directory = Path.GetDirectoryName(fullPath)!;

        // 确保目录存在
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 保存文件到磁盘
        memoryStream.Position = 0;
        await using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
        {
            await memoryStream.CopyToAsync(fs);
        }

        _logger.LogInformation("文档文件已保存: {Path}", fullPath);

        // 创建文档记录，状态为pending，等待后台服务处理
        var document = new KnowledgeDocument
        {
            Title = title,
            FileName = fileName,
            FileType = fileType,
            FileSize = fileSize,
            FilePath = fullPath,          // 保存完整物理路径
            ContentHash = contentHash,
            Status = "pending",           // 待处理状态
            RawContent = content,         // 存储原始内容供后台处理
            ProcessProgress = 0,
            ProcessedChunkCount = 0,
            CategoryId = categoryId,
            DatasourceId = datasourceId,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.KnowledgeDocuments.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation("文档 {Id} 已创建，等待后台处理", document.Id);

        // 立即返回，后台服务会处理分块和向量化
        return document;
    }

    public async Task DeleteDocumentAsync(long id)
    {
        var document = await _context.KnowledgeDocuments.FindAsync(id)
            ?? throw new KeyNotFoundException($"文档不存在: {id}");

        // 级联删除会自动删除分块
        _context.KnowledgeDocuments.Remove(document);
        await _context.SaveChangesAsync();
    }

    public async Task ReprocessDocumentAsync(long id)
    {
        var document = await _context.KnowledgeDocuments
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new KeyNotFoundException($"文档不存在: {id}");

        // 删除现有分块
        _context.KnowledgeChunks.RemoveRange(document.Chunks);
        document.Status = "processing";
        document.ChunkCount = 0;
        await _context.SaveChangesAsync();

        // 重新处理（这里需要重新读取文件内容，简化处理）
        // 实际应从文件系统读取
        _logger.LogWarning("重新处理文档需要从文件系统读取内容，当前为简化实现");
        document.Status = "completed";
        document.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
    #endregion

    #region 向量检索
    public async Task<List<KnowledgeSearchResult>> SearchAsync(
        string query,
        int topK = 5,
        long? categoryId = null,
        long? datasourceId = null,
        float minScore = 0.5f)
    {
        // 生成查询向量
        var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query);
        if (queryEmbedding.Length == 0)
            return new List<KnowledgeSearchResult>();

        var queryVector = new Vector(queryEmbedding);

        // 构建查询
        var chunksQuery = _context.KnowledgeChunks
            .Include(c => c.Document)
            .Where(c => c.Embedding != null && c.Document.Status == "completed");

        if (categoryId.HasValue)
            chunksQuery = chunksQuery.Where(c => c.Document.CategoryId == categoryId);
        if (datasourceId.HasValue)
            chunksQuery = chunksQuery.Where(c => c.Document.DatasourceId == datasourceId);

        // 使用余弦相似度进行向量检索
        var results = await chunksQuery
            .OrderBy(c => c.Embedding!.CosineDistance(queryVector))
            .Take(topK * 2)  // 多取一些用于过滤
            .Select(c => new
            {
                Chunk = c,
                Distance = c.Embedding!.CosineDistance(queryVector)
            })
            .ToListAsync();

        // 转换为结果并过滤低分
        return results
            .Select(r => new KnowledgeSearchResult
            {
                ChunkId = r.Chunk.Id,
                DocumentId = r.Chunk.DocumentId,
                DocumentTitle = r.Chunk.Document.Title,
                FileName = r.Chunk.Document.FileName,  // 添加文件名
                Content = r.Chunk.Content,
                Score = (float)(1 - r.Distance),  // 余弦距离转相似度
                PageNumber = r.Chunk.PageNumber,
                SectionTitle = r.Chunk.SectionTitle
            })
            .Where(r => r.Score >= minScore)
            .Take(topK)
            .ToList();
    }
    #endregion

    #region 私有方法
    /// <summary>
    /// 处理文档：分块并向量化
    /// </summary>
    private async Task ProcessDocumentAsync(long documentId, string content, List<PageContent>? pages = null)
    {
        var options = new ChunkOptions
        {
            ChunkSize = DefaultChunkSize,
            ChunkOverlap = DefaultChunkOverlap,
            Strategy = ChunkStrategy.Paragraph  // 默认按段落分块
        };

        // 根据是否有页面信息选择分块方式
        List<TextChunk> chunks;
        if (pages != null && pages.Count > 0)
        {
            chunks = _textChunker.ChunkPages(pages, options);
        }
        else
        {
            chunks = _textChunker.ChunkText(content, options);
        }

        var chunkEntities = new List<KnowledgeChunk>();

        foreach (var chunk in chunks)
        {
            var embedding = await _embeddingService.GetEmbeddingAsync(chunk.Content);

            chunkEntities.Add(new KnowledgeChunk
            {
                DocumentId = documentId,
                ChunkIndex = chunk.Index,
                Content = chunk.Content,
                ContentLength = chunk.Length,
                PageNumber = chunk.PageNumber,
                SectionTitle = chunk.SectionTitle,
                Embedding = embedding.Length > 0 ? new Vector(embedding) : null,
                CreatedAt = DateTime.UtcNow
            });
        }

        _context.KnowledgeChunks.AddRange(chunkEntities);

        // 更新文档分块数
        var document = await _context.KnowledgeDocuments.FindAsync(documentId);
        if (document != null)
            document.ChunkCount = chunkEntities.Count;

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// 计算内容哈希
    /// </summary>
    private static string ComputeHash(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
    #endregion
}


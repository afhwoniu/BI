using Bi.Domain.Entities;

namespace Bi.Application.Services;

/// <summary>
/// 知识库服务接口
/// 提供文档管理、分块、向量化和检索功能
/// </summary>
public interface IKnowledgeService
{
    #region 分类管理
    /// <summary>
    /// 获取所有分类（树形结构）
    /// </summary>
    Task<List<KnowledgeCategory>> GetCategoriesAsync();

    /// <summary>
    /// 创建分类
    /// </summary>
    Task<KnowledgeCategory> CreateCategoryAsync(string name, long? parentId = null, string? description = null);

    /// <summary>
    /// 更新分类
    /// </summary>
    Task<KnowledgeCategory> UpdateCategoryAsync(long id, string name, string? description = null);

    /// <summary>
    /// 删除分类
    /// </summary>
    Task DeleteCategoryAsync(long id);
    #endregion

    #region 文档管理
    /// <summary>
    /// 获取文档列表
    /// </summary>
    Task<(List<KnowledgeDocument> Items, int Total)> GetDocumentsAsync(
        long? categoryId = null,
        string? status = null,
        string? keyword = null,
        int page = 1,
        int pageSize = 20);

    /// <summary>
    /// 获取文档详情
    /// </summary>
    Task<KnowledgeDocument?> GetDocumentAsync(long id);

    /// <summary>
    /// 上传并创建文档
    /// </summary>
    Task<KnowledgeDocument> CreateDocumentAsync(
        string title,
        string fileName,
        string fileType,
        long fileSize,
        Stream fileStream,
        long? categoryId = null,
        long? datasourceId = null,
        long? createdBy = null);

    /// <summary>
    /// 删除文档（同时删除分块和向量）
    /// </summary>
    Task DeleteDocumentAsync(long id);

    /// <summary>
    /// 重新处理文档（重新分块和向量化）
    /// </summary>
    Task ReprocessDocumentAsync(long id);
    #endregion

    #region 向量检索
    /// <summary>
    /// 语义检索相关文档分块
    /// </summary>
    /// <param name="query">查询文本</param>
    /// <param name="topK">返回数量</param>
    /// <param name="categoryId">限定分类</param>
    /// <param name="datasourceId">限定数据源</param>
    /// <param name="minScore">最小相似度阈值</param>
    Task<List<KnowledgeSearchResult>> SearchAsync(
        string query,
        int topK = 5,
        long? categoryId = null,
        long? datasourceId = null,
        float minScore = 0.5f);
    #endregion
}

/// <summary>
/// 知识库检索结果
/// </summary>
public class KnowledgeSearchResult
{
    /// <summary>
    /// 分块ID
    /// </summary>
    public long ChunkId { get; set; }

    /// <summary>
    /// 文档ID
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// 文档标题
    /// </summary>
    public string DocumentTitle { get; set; } = string.Empty;

    /// <summary>
    /// 文件名
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// 分块内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 相似度分数（0-1）
    /// </summary>
    public float Score { get; set; }

    /// <summary>
    /// 页码（如果有）
    /// </summary>
    public int? PageNumber { get; set; }

    /// <summary>
    /// 章节标题（如果有）
    /// </summary>
    public string? SectionTitle { get; set; }
}


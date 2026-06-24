namespace Bi.Application.Services;

/// <summary>
/// 统一检索服务接口
/// 整合KPI指标检索和知识库文档检索
/// </summary>
public interface IUnifiedSearchService
{
    /// <summary>
    /// 统一语义检索
    /// </summary>
    /// <param name="query">查询文本</param>
    /// <param name="options">检索选项</param>
    /// <returns>检索结果列表</returns>
    Task<List<UnifiedSearchResult>> SearchAsync(string query, UnifiedSearchOptions? options = null);
    
    /// <summary>
    /// 获取RAG上下文
    /// 用于AI分析时注入Prompt
    /// </summary>
    /// <param name="query">用户问题</param>
    /// <param name="datasourceId">数据源ID（用于过滤相关知识）</param>
    /// <param name="topK">返回的最大结果数</param>
    /// <param name="minScore">最低相似度阈值</param>
    /// <returns>格式化的上下文文本</returns>
    Task<string> GetRagContextAsync(string query, long? datasourceId = null, int topK = 5, float minScore = 0.5f);
}

/// <summary>
/// 统一检索选项
/// </summary>
public class UnifiedSearchOptions
{
    /// <summary>
    /// 返回数量
    /// </summary>
    public int TopK { get; set; } = 5;
    
    /// <summary>
    /// 最小相似度阈值
    /// </summary>
    public float MinScore { get; set; } = 0.5f;
    
    /// <summary>
    /// 是否包含KPI指标
    /// </summary>
    public bool IncludeKpi { get; set; } = true;
    
    /// <summary>
    /// 是否包含知识库文档
    /// </summary>
    public bool IncludeKnowledge { get; set; } = true;
    
    /// <summary>
    /// 限定数据源ID
    /// </summary>
    public long? DatasourceId { get; set; }
    
    /// <summary>
    /// 限定知识库分类ID
    /// </summary>
    public long? CategoryId { get; set; }
}

/// <summary>
/// 统一检索结果
/// </summary>
public class UnifiedSearchResult
{
    /// <summary>
    /// 结果类型：kpi/knowledge
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// 来源ID
    /// </summary>
    public long SourceId { get; set; }
    
    /// <summary>
    /// 标题
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// 内容摘要
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// 相似度分数
    /// </summary>
    public float Score { get; set; }
    
    /// <summary>
    /// 元数据（KPI的SQL模板、知识库的页码等）
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}


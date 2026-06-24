using Bi.Domain.Entities;

namespace Bi.Application.Services;

/// <summary>
/// 指标检索结果
/// </summary>
public class KpiSearchResult
{
    /// <summary>
    /// 指标定义
    /// </summary>
    public KpiDefinition Kpi { get; set; } = null!;
    
    /// <summary>
    /// 相似度得分（0-1，越高越相似）
    /// </summary>
    public double Score { get; set; }
}

/// <summary>
/// 指标检索服务接口
/// </summary>
public interface IKpiRetrieverService
{
    /// <summary>
    /// 根据自然语言查询检索相关指标
    /// </summary>
    /// <param name="query">查询文本</param>
    /// <param name="topK">返回结果数量</param>
    /// <param name="minScore">最小相似度阈值</param>
    /// <returns>相关指标列表</returns>
    Task<List<KpiSearchResult>> SearchAsync(string query, int topK = 5, double minScore = 0.5);
    
    /// <summary>
    /// 为指标生成向量嵌入
    /// </summary>
    /// <param name="kpiId">指标ID</param>
    Task GenerateEmbeddingAsync(long kpiId);
    
    /// <summary>
    /// 批量为指标生成向量嵌入
    /// </summary>
    /// <param name="kpiIds">指标ID列表，为空则处理所有未生成向量的指标</param>
    Task GenerateEmbeddingsAsync(List<long>? kpiIds = null);
}


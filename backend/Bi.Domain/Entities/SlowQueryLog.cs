namespace Bi.Domain.Entities;

/// <summary>
/// 慢查询日志实体 - 对应bi_slow_query_log表
/// </summary>
public class SlowQueryLog : BaseEntity
{
    /// <summary>
    /// 数据源ID
    /// </summary>
    public long DatasourceId { get; set; }

    /// <summary>
    /// 图表ID（可选）
    /// </summary>
    public long? ChartId { get; set; }

    /// <summary>
    /// 执行的SQL语句
    /// </summary>
    public string SqlText { get; set; } = string.Empty;

    /// <summary>
    /// 执行时间（毫秒）
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// 慢查询阈值（毫秒）
    /// </summary>
    public long ThresholdMs { get; set; }

    /// <summary>
    /// 执行用户
    /// </summary>
    public string? ExecutedBy { get; set; }

    /// <summary>
    /// 执行时间
    /// </summary>
    public DateTime ExecutedAt { get; set; }

    /// <summary>
    /// EXPLAIN分析结果（JSON）
    /// </summary>
    public string? ExplainResult { get; set; }

    /// <summary>
    /// 优化建议
    /// </summary>
    public string? Suggestion { get; set; }

    /// <summary>
    /// 是否已处理
    /// </summary>
    public bool IsResolved { get; set; }

    /// <summary>
    /// 关联数据源
    /// </summary>
    public virtual Datasource? Datasource { get; set; }

    /// <summary>
    /// 关联图表
    /// </summary>
    public virtual Chart? Chart { get; set; }
}


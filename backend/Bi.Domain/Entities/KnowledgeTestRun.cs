using System.ComponentModel.DataAnnotations;

namespace Bi.Domain.Entities;

/// <summary>
/// 知识库测试运行记录
/// 记录每次测试的结果和指标
/// </summary>
public class KnowledgeTestRun
{
    /// <summary>
    /// 测试运行ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 运行名称/标签
    /// </summary>
    [MaxLength(200)]
    public string? Name { get; set; }

    /// <summary>
    /// 运行状态：pending-待运行, running-运行中, completed-已完成, failed-失败
    /// </summary>
    [MaxLength(50)]
    public string Status { get; set; } = "pending";

    /// <summary>
    /// 测试用例总数
    /// </summary>
    public int TotalCases { get; set; }

    /// <summary>
    /// 已完成用例数
    /// </summary>
    public int CompletedCases { get; set; }

    /// <summary>
    /// TopK参数（检索返回的最大结果数）
    /// </summary>
    public int TopK { get; set; } = 5;

    /// <summary>
    /// 最小相似度阈值
    /// </summary>
    public float MinScore { get; set; } = 0.5f;

    /// <summary>
    /// 命中率（Hit Rate）：至少命中一个期望结果的比例
    /// </summary>
    public float HitRate { get; set; }

    /// <summary>
    /// 平均倒数排名（MRR - Mean Reciprocal Rank）
    /// </summary>
    public float Mrr { get; set; }

    /// <summary>
    /// 平均精确率（Precision@K）
    /// </summary>
    public float AvgPrecision { get; set; }

    /// <summary>
    /// 平均召回率（Recall@K）
    /// </summary>
    public float AvgRecall { get; set; }

    /// <summary>
    /// 平均检索耗时（毫秒）
    /// </summary>
    public float AvgLatencyMs { get; set; }

    /// <summary>
    /// 详细结果（JSON数组，每个用例的结果）
    /// </summary>
    public string? DetailResults { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}


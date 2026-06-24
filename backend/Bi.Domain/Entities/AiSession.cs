using System.ComponentModel.DataAnnotations;

namespace Bi.Domain.Entities;

/// <summary>
/// AI会话
/// </summary>
public class AiSession
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 会话唯一标识（用于前端）
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string SessionKey { get; set; } = string.Empty;

    /// <summary>
    /// 会话标题（取自第一条消息）
    /// </summary>
    [MaxLength(200)]
    public string? Title { get; set; }

    /// <summary>
    /// 对话模式：bi-指标统计, hz360-患者360, internetsearch-通用问答
    /// </summary>
    [MaxLength(20)]
    public string Mode { get; set; } = "bi";

    /// <summary>
    /// 关联的数据源ID
    /// </summary>
    public long? DatasourceId { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后活动时间
    /// </summary>
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    // 导航属性
    public virtual Datasource? Datasource { get; set; }
    public virtual SysUser? User { get; set; }
    public virtual ICollection<AiMessage> Messages { get; set; } = new List<AiMessage>();
}

/// <summary>
/// AI消息
/// </summary>
public class AiMessage
{
    /// <summary>
    /// 消息ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 所属会话ID
    /// </summary>
    public long SessionId { get; set; }

    /// <summary>
    /// 消息角色（user/assistant）
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = "user";

    /// <summary>
    /// 对话模式：bi-指标统计, hz360-患者360, internetsearch-通用问答
    /// </summary>
    [MaxLength(20)]
    public string? Mode { get; set; }

    /// <summary>
    /// 消息内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 生成的SQL（仅assistant消息，兼容旧版）
    /// </summary>
    public string? Sql { get; set; }

    /// <summary>
    /// 明细SQL（核心！用于下钻聚合）
    /// </summary>
    public string? DetailSql { get; set; }

    /// <summary>
    /// 医院字段名（用于医共体筛选）
    /// </summary>
    [MaxLength(100)]
    public string? HospitalField { get; set; }

    /// <summary>
    /// 日期字段名（用于同比环比计算和时间参数替换）
    /// </summary>
    [MaxLength(100)]
    public string? DateField { get; set; }

    /// <summary>
    /// 可用维度字段JSON（如：["就诊日期","科室名称","医生"]）
    /// </summary>
    public string? DimensionFields { get; set; }

    /// <summary>
    /// 可用度量字段JSON（如：[{"field":"费用","alias":"总费用","agg":"SUM"}]）
    /// </summary>
    public string? MeasureFields { get; set; }

    /// <summary>
    /// KPI配置JSON
    /// </summary>
    public string? KpiConfig { get; set; }

    /// <summary>
    /// 原始图表配置JSON（用于刷新时恢复图表结构）
    /// </summary>
    public string? DefaultChartsConfig { get; set; }

    /// <summary>
    /// 完整提示词（用于调试和复现，旧版兼容）
    /// </summary>
    public string? PromptText { get; set; }

    /// <summary>
    /// 分阶段提示词JSON（用于保存完整的prompts列表）
    /// </summary>
    public string? PromptsJson { get; set; }

    /// <summary>
    /// 推荐的图表类型
    /// </summary>
    [MaxLength(50)]
    public string? ChartType { get; set; }

    /// <summary>
    /// 图表配置JSON
    /// </summary>
    public string? ChartConfig { get; set; }

    /// <summary>
    /// 图表截图路径JSON（如：["/uploads/charts/123_0.png", "/uploads/charts/123_1.png"]）
    /// </summary>
    public string? ChartImages { get; set; }

    /// <summary>
    /// Token消耗
    /// </summary>
    public int TokensUsed { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 导航属性
    public virtual AiSession? Session { get; set; }
}


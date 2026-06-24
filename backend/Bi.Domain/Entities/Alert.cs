namespace Bi.Domain.Entities;

/// <summary>
/// 预警规则实体
/// </summary>
public class AlertRule : BaseEntity
{
    /// <summary>
    /// 规则编码（唯一）
    /// </summary>
    public string RuleCode { get; set; } = string.Empty;

    /// <summary>
    /// 规则名称
    /// </summary>
    public string RuleName { get; set; } = string.Empty;

    /// <summary>
    /// 规则类型：threshold/mom_change/yoy_change/continuous/custom_sql
    /// </summary>
    public string RuleType { get; set; } = AlertRuleTypes.Threshold;

    /// <summary>
    /// 默认严重级别：info/warning/critical/emergency
    /// </summary>
    public string SeverityLevel { get; set; } = AlertSeverityLevels.Warning;

    /// <summary>
    /// 规则状态：enabled/disabled
    /// </summary>
    public string RuleStatus { get; set; } = AlertRuleStatuses.Enabled;

    /// <summary>
    /// 关联数据源ID
    /// </summary>
    public long? DatasourceId { get; set; }

    /// <summary>
    /// 关联数据集ID
    /// </summary>
    public long? DatasetId { get; set; }

    /// <summary>
    /// 关联图表ID
    /// </summary>
    public long? ChartId { get; set; }

    /// <summary>
    /// 关联指标定义ID
    /// </summary>
    public long? KpiId { get; set; }

    /// <summary>
    /// 指标字段名
    /// </summary>
    public string? MetricField { get; set; }

    /// <summary>
    /// 维度字段名
    /// </summary>
    public string? DimensionField { get; set; }

    /// <summary>
    /// 时间字段名
    /// </summary>
    public string? TimeField { get; set; }

    /// <summary>
    /// 统计粒度：minute/hour/day/week/month
    /// </summary>
    public string StatGranularity { get; set; } = "day";

    /// <summary>
    /// 触发条件JSON
    /// </summary>
    public string ConditionJson { get; set; } = "{}";

    /// <summary>
    /// 计算SQL（自定义规则时使用）
    /// </summary>
    public string? CalcSql { get; set; }

    /// <summary>
    /// 调度类型：interval/cron
    /// </summary>
    public string ScheduleType { get; set; } = "interval";

    /// <summary>
    /// Cron表达式
    /// </summary>
    public string? CronExpr { get; set; }

    /// <summary>
    /// 执行间隔（秒）
    /// </summary>
    public int IntervalSeconds { get; set; } = 300;

    /// <summary>
    /// 执行时区
    /// </summary>
    public string Timezone { get; set; } = "Asia/Shanghai";

    /// <summary>
    /// 去重窗口（分钟）
    /// </summary>
    public int DedupMinutes { get; set; } = 60;

    /// <summary>
    /// 冷却时间（分钟）
    /// </summary>
    public int CooldownMinutes { get; set; } = 30;

    /// <summary>
    /// 规则负责人
    /// </summary>
    public long? OwnerUserId { get; set; }

    /// <summary>
    /// 通知渠道JSON数组
    /// </summary>
    public string NotifyChannels { get; set; } = "[]";

    /// <summary>
    /// 通知模板
    /// </summary>
    public string? NotifyTemplate { get; set; }

    /// <summary>
    /// 上次检测时间
    /// </summary>
    public DateTime? LastCheckAt { get; set; }

    /// <summary>
    /// 下次检测时间
    /// </summary>
    public DateTime? NextCheckAt { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    public virtual Datasource? Datasource { get; set; }
    public virtual Dataset? Dataset { get; set; }
    public virtual Chart? Chart { get; set; }
    public virtual KpiDefinition? Kpi { get; set; }
    public virtual SysUser? OwnerUser { get; set; }
    public virtual ICollection<AlertEvent> Events { get; set; } = new List<AlertEvent>();
    public virtual ICollection<AlertSubscription> Subscriptions { get; set; } = new List<AlertSubscription>();
    public virtual ICollection<AlertMetricSnapshot> MetricSnapshots { get; set; } = new List<AlertMetricSnapshot>();
}

/// <summary>
/// 预警事件实体
/// </summary>
public class AlertEvent : BaseEntity
{
    /// <summary>
    /// 事件编号
    /// </summary>
    public string EventNo { get; set; } = string.Empty;

    /// <summary>
    /// 规则ID
    /// </summary>
    public long RuleId { get; set; }

    /// <summary>
    /// 规则快照JSON
    /// </summary>
    public string RuleSnapshotJson { get; set; } = "{}";

    /// <summary>
    /// 事件状态：open/acknowledged/resolved/ignored/closed
    /// </summary>
    public string EventStatus { get; set; } = AlertEventStatuses.Open;

    /// <summary>
    /// 严重级别
    /// </summary>
    public string SeverityLevel { get; set; } = AlertSeverityLevels.Warning;

    /// <summary>
    /// 本次触发时间
    /// </summary>
    public DateTime TriggerTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 首次触发时间
    /// </summary>
    public DateTime FirstTriggeredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最近触发时间
    /// </summary>
    public DateTime LastTriggeredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 累计触发次数
    /// </summary>
    public int TriggerCount { get; set; } = 1;

    /// <summary>
    /// 当前值
    /// </summary>
    public decimal? CurrentValue { get; set; }

    /// <summary>
    /// 基线值
    /// </summary>
    public decimal? BaselineValue { get; set; }

    /// <summary>
    /// 对比值
    /// </summary>
    public decimal? CompareValue { get; set; }

    /// <summary>
    /// 变化百分比
    /// </summary>
    public decimal? ChangePct { get; set; }

    /// <summary>
    /// 阈值说明
    /// </summary>
    public string? ThresholdDesc { get; set; }

    /// <summary>
    /// 维度值JSON
    /// </summary>
    public string DimensionValueJson { get; set; } = "{}";

    /// <summary>
    /// 证据JSON
    /// </summary>
    public string EvidenceJson { get; set; } = "{}";

    /// <summary>
    /// 处置建议
    /// </summary>
    public string? SuggestionText { get; set; }

    /// <summary>
    /// 确认人
    /// </summary>
    public long? AckBy { get; set; }

    /// <summary>
    /// 确认时间
    /// </summary>
    public DateTime? AckAt { get; set; }

    /// <summary>
    /// 解决人
    /// </summary>
    public long? ResolvedBy { get; set; }

    /// <summary>
    /// 解决时间
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// 处理说明
    /// </summary>
    public string? ResolutionNote { get; set; }

    /// <summary>
    /// 是否已通知
    /// </summary>
    public bool IsNotified { get; set; }

    /// <summary>
    /// 通知时间
    /// </summary>
    public DateTime? NotifiedAt { get; set; }

    public virtual AlertRule? Rule { get; set; }
    public virtual SysUser? AckUser { get; set; }
    public virtual SysUser? ResolvedUser { get; set; }
    public virtual ICollection<AlertEventAction> Actions { get; set; } = new List<AlertEventAction>();
    public virtual ICollection<AlertNotificationLog> NotificationLogs { get; set; } = new List<AlertNotificationLog>();
}

/// <summary>
/// 预警事件动作实体
/// </summary>
public class AlertEventAction : BaseEntity
{
    public long EventId { get; set; }
    public string ActionType { get; set; } = "comment";
    public long? ActionUserId { get; set; }
    public string? ActionNote { get; set; }
    public string ActionPayload { get; set; } = "{}";

    public virtual AlertEvent? Event { get; set; }
    public virtual SysUser? ActionUser { get; set; }
}

/// <summary>
/// 预警订阅实体
/// </summary>
public class AlertSubscription : BaseEntity
{
    public long? RuleId { get; set; }
    public string SubscriberType { get; set; } = "user";
    public long? SubscriberId { get; set; }
    public string ChannelType { get; set; } = "inapp";
    public string? ChannelTarget { get; set; }
    public string SeverityFilter { get; set; } = "[]";
    public bool IsEnabled { get; set; } = true;

    public virtual AlertRule? Rule { get; set; }
}

/// <summary>
/// 通知发送日志实体
/// </summary>
public class AlertNotificationLog : BaseEntity
{
    public long EventId { get; set; }
    public long RuleId { get; set; }
    public long? SubscriptionId { get; set; }
    public string ChannelType { get; set; } = "inapp";
    public string? SendTo { get; set; }
    public string SendStatus { get; set; } = "pending";
    public string? SendContent { get; set; }
    public string? ResponseText { get; set; }
    public int RetryCount { get; set; }
    public DateTime? SentAt { get; set; }

    public virtual AlertEvent? Event { get; set; }
    public virtual AlertRule? Rule { get; set; }
    public virtual AlertSubscription? Subscription { get; set; }
}

/// <summary>
/// 指标快照实体
/// </summary>
public class AlertMetricSnapshot : BaseEntity
{
    public long RuleId { get; set; }
    public DateTime SnapshotTime { get; set; } = DateTime.UtcNow;
    public decimal? CurrentValue { get; set; }
    public decimal? BaselineValue { get; set; }
    public decimal? CompareValue { get; set; }
    public decimal? ChangePct { get; set; }
    public string DimensionValueJson { get; set; } = "{}";
    public string CalcContextJson { get; set; } = "{}";

    public virtual AlertRule? Rule { get; set; }
}

public static class AlertRuleTypes
{
    public const string Threshold = "threshold";
    public const string MomChange = "mom_change";
    public const string YoyChange = "yoy_change";
    public const string Continuous = "continuous";
    public const string CustomSql = "custom_sql";
}

public static class AlertRuleStatuses
{
    public const string Enabled = "enabled";
    public const string Disabled = "disabled";
}

public static class AlertSeverityLevels
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Critical = "critical";
    public const string Emergency = "emergency";
}

public static class AlertEventStatuses
{
    public const string Open = "open";
    public const string Acknowledged = "acknowledged";
    public const string Resolved = "resolved";
    public const string Ignored = "ignored";
    public const string Closed = "closed";
}


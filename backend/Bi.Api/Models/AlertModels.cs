using System.Text.Json;

namespace Bi.Api.Models;

/// <summary>
/// 预警规则列表项
/// </summary>
public class AlertRuleListDto
{
    public long Id { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string SeverityLevel { get; set; } = string.Empty;
    public string RuleStatus { get; set; } = string.Empty;
    public long? DatasourceId { get; set; }
    public long? ChartId { get; set; }
    public long? KpiId { get; set; }
    public DateTime? LastCheckAt { get; set; }
    public DateTime? NextCheckAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 预警规则详情
/// </summary>
public class AlertRuleDetailDto : AlertRuleListDto
{
    public long? DatasetId { get; set; }
    public string? MetricField { get; set; }
    public string? DimensionField { get; set; }
    public string? TimeField { get; set; }
    public string StatGranularity { get; set; } = "day";
    public JsonElement ConditionJson { get; set; }
    public string? CalcSql { get; set; }
    public string ScheduleType { get; set; } = "interval";
    public string? CronExpr { get; set; }
    public int IntervalSeconds { get; set; }
    public string Timezone { get; set; } = "Asia/Shanghai";
    public int DedupMinutes { get; set; }
    public int CooldownMinutes { get; set; }
    public long? OwnerUserId { get; set; }
    public JsonElement NotifyChannels { get; set; }
    public string? NotifyTemplate { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 新增/更新预警规则请求
/// </summary>
public class AlertRuleUpsertDto
{
    public string? RuleCode { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string RuleType { get; set; } = "threshold";
    public string SeverityLevel { get; set; } = "warning";
    public string RuleStatus { get; set; } = "enabled";
    public long? DatasourceId { get; set; }
    public long? DatasetId { get; set; }
    public long? ChartId { get; set; }
    public long? KpiId { get; set; }
    public string? MetricField { get; set; }
    public string? DimensionField { get; set; }
    public string? TimeField { get; set; }
    public string StatGranularity { get; set; } = "day";
    public JsonElement? ConditionJson { get; set; }
    public string? CalcSql { get; set; }
    public string ScheduleType { get; set; } = "interval";
    public string? CronExpr { get; set; }
    public int IntervalSeconds { get; set; } = 300;
    public string Timezone { get; set; } = "Asia/Shanghai";
    public int DedupMinutes { get; set; } = 60;
    public int CooldownMinutes { get; set; } = 30;
    public long? OwnerUserId { get; set; }
    public JsonElement? NotifyChannels { get; set; }
    public string? NotifyTemplate { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 手工执行规则结果
/// </summary>
public class AlertRuleRunResultDto
{
    public bool Triggered { get; set; }
    public long? EventId { get; set; }
    public string Message { get; set; } = string.Empty;
    public decimal? CurrentValue { get; set; }
    public decimal? BaselineValue { get; set; }
    public decimal? CompareValue { get; set; }
    public decimal? ChangePct { get; set; }
}

/// <summary>
/// 预警事件列表项
/// </summary>
public class AlertEventListDto
{
    public long Id { get; set; }
    public string EventNo { get; set; } = string.Empty;
    public long RuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string EventStatus { get; set; } = string.Empty;
    public string SeverityLevel { get; set; } = string.Empty;
    public DateTime TriggerTime { get; set; }
    public DateTime FirstTriggeredAt { get; set; }
    public DateTime LastTriggeredAt { get; set; }
    public int TriggerCount { get; set; }
    public decimal? CurrentValue { get; set; }
    public decimal? BaselineValue { get; set; }
    public decimal? CompareValue { get; set; }
    public decimal? ChangePct { get; set; }
    public string? ThresholdDesc { get; set; }
    public JsonElement DimensionValueJson { get; set; }
    public string? SuggestionText { get; set; }
    public long? AckBy { get; set; }
    public DateTime? AckAt { get; set; }
    public long? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

/// <summary>
/// 预警事件详情
/// </summary>
public class AlertEventDetailDto : AlertEventListDto
{
    public JsonElement RuleSnapshotJson { get; set; }
    public JsonElement EvidenceJson { get; set; }
    public string? ResolutionNote { get; set; }
    public bool IsNotified { get; set; }
    public DateTime? NotifiedAt { get; set; }
}

/// <summary>
/// 事件动作记录
/// </summary>
public class AlertEventActionDto
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public long? ActionUserId { get; set; }
    public string? ActionNote { get; set; }
    public JsonElement ActionPayload { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 事件处理请求
/// </summary>
public class AlertEventHandleDto
{
    public string? Note { get; set; }
}

/// <summary>
/// 事件追加动作请求
/// </summary>
public class AlertEventActionCreateDto
{
    public string ActionType { get; set; } = "comment";
    public string? ActionNote { get; set; }
    public JsonElement? ActionPayload { get; set; }
}

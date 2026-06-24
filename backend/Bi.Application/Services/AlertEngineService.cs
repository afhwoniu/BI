using System.Data.Common;
using System.Text.Json;
using Bi.Domain.Entities;
using Bi.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Npgsql;

namespace Bi.Application.Services;

public interface IAlertEngineService
{
    Task<AlertRuleRunResult> RunRuleAsync(long ruleId, bool fromScheduler = false, CancellationToken ct = default);
    Task<int> RunDueRulesAsync(CancellationToken ct = default);
}

public class AlertRuleRunResult
{
    public bool Triggered { get; set; }
    public long? EventId { get; set; }
    public string Message { get; set; } = string.Empty;
    public decimal? CurrentValue { get; set; }
    public decimal? BaselineValue { get; set; }
    public decimal? CompareValue { get; set; }
    public decimal? ChangePct { get; set; }
}

internal class AlertMeasureResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public decimal? CurrentValue { get; set; }
    public decimal? BaselineValue { get; set; }
    public decimal? CompareValue { get; set; }
    public decimal? ChangePct { get; set; }
    public string DimensionValueJson { get; set; } = "{}";
    public string EvidenceJson { get; set; } = "{}";
}

public class AlertEngineService : IAlertEngineService
{
    private readonly BiDbContext _db;
    private readonly IConfigService _configService;
    private readonly ILogger<AlertEngineService> _logger;

    public AlertEngineService(BiDbContext db, IConfigService configService, ILogger<AlertEngineService> logger)
    {
        _db = db;
        _configService = configService;
        _logger = logger;
    }

    public async Task<int> RunDueRulesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var dueRuleIds = await _db.AlertRules
            .Where(r => r.RuleStatus == AlertRuleStatuses.Enabled)
            .Where(r => r.NextCheckAt == null || r.NextCheckAt <= now)
            .OrderBy(r => r.NextCheckAt)
            .Select(r => r.Id)
            .ToListAsync(ct);

        var count = 0;
        foreach (var ruleId in dueRuleIds)
        {
            try
            {
                await RunRuleAsync(ruleId, true, ct);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行到期预警规则失败: RuleId={RuleId}", ruleId);
            }
        }

        return count;
    }

    public async Task<AlertRuleRunResult> RunRuleAsync(long ruleId, bool fromScheduler = false, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var rule = await _db.AlertRules
            .Include(r => r.Datasource)
            .Include(r => r.Dataset)
            .Include(r => r.Chart)
                .ThenInclude(c => c!.Dataset)
                    .ThenInclude(d => d!.Datasource)
            .FirstOrDefaultAsync(r => r.Id == ruleId, ct);

        if (rule == null)
        {
            return new AlertRuleRunResult { Message = "规则不存在" };
        }

        if (rule.RuleStatus != AlertRuleStatuses.Enabled)
        {
            return new AlertRuleRunResult { Message = "规则已禁用，不执行" };
        }

        var measure = await MeasureAsync(rule, ct);
        if (!measure.Success)
        {
            rule.LastCheckAt = now;
            rule.NextCheckAt = ComputeNextCheckAt(rule, now);
            rule.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);

            return new AlertRuleRunResult
            {
                Triggered = false,
                Message = measure.Message
            };
        }

        _db.AlertMetricSnapshots.Add(new AlertMetricSnapshot
        {
            RuleId = rule.Id,
            SnapshotTime = now,
            CurrentValue = measure.CurrentValue,
            BaselineValue = measure.BaselineValue,
            CompareValue = measure.CompareValue,
            ChangePct = measure.ChangePct,
            DimensionValueJson = measure.DimensionValueJson,
            CalcContextJson = measure.EvidenceJson
        });

        var (triggered, thresholdDesc) = await EvaluateTriggerAsync(rule, measure, ct);
        AlertEvent? eventEntity = null;

        if (triggered)
        {
            var upsert = await UpsertEventAsync(rule, measure, thresholdDesc, now, ct);
            eventEntity = upsert.Event;

            _db.AlertEventActions.Add(new AlertEventAction
            {
                Event = eventEntity,
                ActionType = upsert.IsNew ? "trigger" : "retrigger",
                ActionNote = upsert.IsNew ? "规则命中，创建预警事件" : "规则再次命中，事件已更新",
                ActionPayload = JsonSerializer.Serialize(new
                {
                    source = fromScheduler ? "scheduler" : "manual",
                    currentValue = measure.CurrentValue,
                    baselineValue = measure.BaselineValue,
                    compareValue = measure.CompareValue,
                    changePct = measure.ChangePct
                })
            });

            await CreateNotificationLogsAsync(rule, eventEntity, now, ct);
        }

        rule.LastCheckAt = now;
        rule.NextCheckAt = ComputeNextCheckAt(rule, now);
        rule.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        return new AlertRuleRunResult
        {
            Triggered = triggered,
            EventId = eventEntity?.Id,
            Message = triggered ? "规则命中并生成预警事件" : "规则未命中",
            CurrentValue = measure.CurrentValue,
            BaselineValue = measure.BaselineValue,
            CompareValue = measure.CompareValue,
            ChangePct = measure.ChangePct
        };
    }

    private async Task<AlertMeasureResult> MeasureAsync(AlertRule rule, CancellationToken ct)
    {
        try
        {
            decimal? currentValue;
            var evidence = new Dictionary<string, object?>
            {
                ["ruleType"] = rule.RuleType
            };

            if (!string.IsNullOrWhiteSpace(rule.CalcSql))
            {
                var datasource = rule.Datasource;
                if (datasource == null)
                {
                    return new AlertMeasureResult { Success = false, Message = "未关联数据源，无法执行 calcSql" };
                }

                if (!ValidateReadOnlySql(rule.CalcSql, out var sqlMessage))
                {
                    return new AlertMeasureResult { Success = false, Message = sqlMessage };
                }

                currentValue = await ExecuteScalarAsync(datasource.Type, datasource.ConnString, rule.CalcSql, ct);
                evidence["source"] = "calc_sql";
            }
            else if (rule.ChartId.HasValue)
            {
                var chart = await _db.Charts
                    .Include(c => c.Dataset)
                        .ThenInclude(d => d!.Datasource)
                    .FirstOrDefaultAsync(c => c.Id == rule.ChartId.Value, ct);

                if (chart?.Dataset?.Datasource == null)
                {
                    return new AlertMeasureResult { Success = false, Message = "关联图表或数据源无效" };
                }

                currentValue = await ExecuteChartMeasureAsync(chart, ct);
                evidence["source"] = "chart";
                evidence["chartId"] = chart.Id;
            }
            else
            {
                return new AlertMeasureResult { Success = false, Message = "请配置 calcSql 或关联 chartId" };
            }

            var lastSnapshot = await _db.AlertMetricSnapshots
                .Where(s => s.RuleId == rule.Id)
                .OrderByDescending(s => s.SnapshotTime)
                .FirstOrDefaultAsync(ct);

            var compareValue = lastSnapshot?.CurrentValue;
            decimal? changePct = null;
            if (currentValue.HasValue && compareValue.HasValue && compareValue.Value != 0)
            {
                changePct = (currentValue.Value - compareValue.Value) / Math.Abs(compareValue.Value);
            }

            evidence["currentValue"] = currentValue;
            evidence["compareValue"] = compareValue;
            evidence["changePct"] = changePct;

            return new AlertMeasureResult
            {
                Success = true,
                Message = "计算成功",
                CurrentValue = currentValue,
                BaselineValue = compareValue,
                CompareValue = compareValue,
                ChangePct = changePct,
                DimensionValueJson = "{}",
                EvidenceJson = JsonSerializer.Serialize(evidence)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "规则计算失败: RuleId={RuleId}", rule.Id);
            return new AlertMeasureResult
            {
                Success = false,
                Message = $"计算失败: {ex.Message}"
            };
        }
    }

    private async Task<(bool Triggered, string ThresholdDesc)> EvaluateTriggerAsync(AlertRule rule, AlertMeasureResult measure, CancellationToken ct)
    {
        if (!measure.CurrentValue.HasValue)
            return (false, "当前值为空");

        var condition = ParseJsonObject(rule.ConditionJson);
        var op = GetString(condition, "operator", ">=");
        var threshold = GetDecimal(condition, "threshold", 0m);

        if (rule.RuleType == AlertRuleTypes.MomChange || rule.RuleType == AlertRuleTypes.YoyChange)
        {
            if (!measure.ChangePct.HasValue)
                return (false, "缺少变化率，无法判断环比/同比规则");

            var changeThreshold = GetDecimal(condition, "changePct", 0m);
            if (changeThreshold > 1) changeThreshold /= 100m;

            var hit = Compare(measure.ChangePct.Value, op, changeThreshold);
            return (hit, $"变化率 {op} {changeThreshold:P2}");
        }

        if (rule.RuleType == AlertRuleTypes.Continuous)
        {
            var currentHit = Compare(measure.CurrentValue.Value, op, threshold);
            if (!currentHit) return (false, $"{measure.CurrentValue.Value} 未命中阈值");

            var requiredCount = (int)GetDecimal(condition, "consecutiveCount", 3m);
            requiredCount = Math.Max(requiredCount, 2);

            var history = await _db.AlertMetricSnapshots
                .Where(s => s.RuleId == rule.Id)
                .OrderByDescending(s => s.SnapshotTime)
                .Take(requiredCount - 1)
                .ToListAsync(ct);

            if (history.Count < requiredCount - 1)
                return (false, $"历史样本不足，需连续 {requiredCount} 次");

            var historyAllHit = history.All(h => h.CurrentValue.HasValue && Compare(h.CurrentValue.Value, op, threshold));
            return (historyAllHit, $"连续 {requiredCount} 次满足：值 {op} {threshold}");
        }

        var triggered = Compare(measure.CurrentValue.Value, op, threshold);
        return (triggered, $"当前值 {op} {threshold}");
    }

    private async Task<(AlertEvent Event, bool IsNew)> UpsertEventAsync(
        AlertRule rule,
        AlertMeasureResult measure,
        string thresholdDesc,
        DateTime now,
        CancellationToken ct)
    {
        var dedupFrom = now.AddMinutes(-Math.Max(rule.DedupMinutes, 0));
        var activeStatuses = new[] { AlertEventStatuses.Open, AlertEventStatuses.Acknowledged };

        var existing = await _db.AlertEvents
            .Where(e => e.RuleId == rule.Id)
            .Where(e => activeStatuses.Contains(e.EventStatus))
            .Where(e => e.LastTriggeredAt >= dedupFrom)
            .Where(e => e.DimensionValueJson == measure.DimensionValueJson)
            .OrderByDescending(e => e.LastTriggeredAt)
            .FirstOrDefaultAsync(ct);

        if (existing != null)
        {
            existing.TriggerTime = now;
            existing.LastTriggeredAt = now;
            existing.TriggerCount += 1;
            existing.CurrentValue = measure.CurrentValue;
            existing.BaselineValue = measure.BaselineValue;
            existing.CompareValue = measure.CompareValue;
            existing.ChangePct = measure.ChangePct;
            existing.ThresholdDesc = thresholdDesc;
            existing.SeverityLevel = rule.SeverityLevel;
            existing.EvidenceJson = measure.EvidenceJson;
            existing.SuggestionText ??= $"请关注指标 {rule.RuleName} 的异常变化。";
            existing.UpdatedAt = now;
            return (existing, false);
        }

        var evt = new AlertEvent
        {
            EventNo = BuildEventNo(),
            RuleId = rule.Id,
            RuleSnapshotJson = BuildRuleSnapshot(rule),
            EventStatus = AlertEventStatuses.Open,
            SeverityLevel = rule.SeverityLevel,
            TriggerTime = now,
            FirstTriggeredAt = now,
            LastTriggeredAt = now,
            TriggerCount = 1,
            CurrentValue = measure.CurrentValue,
            BaselineValue = measure.BaselineValue,
            CompareValue = measure.CompareValue,
            ChangePct = measure.ChangePct,
            ThresholdDesc = thresholdDesc,
            DimensionValueJson = measure.DimensionValueJson,
            EvidenceJson = measure.EvidenceJson,
            SuggestionText = $"请及时核查“{rule.RuleName}”并记录处置措施。"
        };
        _db.AlertEvents.Add(evt);
        return (evt, true);
    }

    private async Task CreateNotificationLogsAsync(AlertRule rule, AlertEvent evt, DateTime now, CancellationToken ct)
    {
        var subscriptions = await _db.AlertSubscriptions
            .Where(s => s.IsEnabled)
            .Where(s => s.RuleId == rule.Id || s.RuleId == null)
            .ToListAsync(ct);

        var channels = ParseStringArray(rule.NotifyChannels);
        if (channels.Count == 0)
            channels.Add("inapp");

        if (subscriptions.Count > 0)
        {
            foreach (var sub in subscriptions)
            {
                _db.AlertNotificationLogs.Add(new AlertNotificationLog
                {
                    Event = evt,
                    RuleId = rule.Id,
                    SubscriptionId = sub.Id,
                    ChannelType = sub.ChannelType,
                    SendTo = sub.ChannelTarget,
                    SendStatus = sub.ChannelType == "inapp" ? "success" : "pending",
                    SendContent = BuildNotifyContent(rule, evt),
                    ResponseText = sub.ChannelType == "inapp" ? "站内消息已入队" : null,
                    SentAt = sub.ChannelType == "inapp" ? now : null
                });
            }
        }
        else
        {
            foreach (var channel in channels)
            {
                var sendTo = await ResolveDefaultChannelTargetAsync(channel);
                _db.AlertNotificationLogs.Add(new AlertNotificationLog
                {
                    Event = evt,
                    RuleId = rule.Id,
                    ChannelType = channel,
                    SendTo = sendTo,
                    SendStatus = channel == "inapp" ? "success" : "pending",
                    SendContent = BuildNotifyContent(rule, evt),
                    ResponseText = channel == "inapp" ? "站内消息已入队" : null,
                    SentAt = channel == "inapp" ? now : null
                });
            }
        }

        evt.IsNotified = true;
        evt.NotifiedAt = now;
    }

    private async Task<string?> ResolveDefaultChannelTargetAsync(string channel)
    {
        if (string.Equals(channel, "wecom", StringComparison.OrdinalIgnoreCase))
            return await _configService.GetAsync(ConfigKeys.AlertNotifyWecomWebhook, null);
        if (string.Equals(channel, "webhook", StringComparison.OrdinalIgnoreCase))
            return await _configService.GetAsync(ConfigKeys.AlertNotifyWebhookUrl, null);
        return null;
    }

    private DateTime ComputeNextCheckAt(AlertRule rule, DateTime now)
    {
        if (rule.ScheduleType == "interval")
        {
            var seconds = rule.IntervalSeconds <= 0 ? 300 : rule.IntervalSeconds;
            return now.AddSeconds(seconds);
        }

        // V1先兼容cron字段：未解析cron时回落到5分钟轮询
        return now.AddSeconds(300);
    }

    private static string BuildEventNo()
    {
        return $"AE{DateTime.UtcNow:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";
    }

    private static string BuildRuleSnapshot(AlertRule rule)
    {
        return JsonSerializer.Serialize(new
        {
            rule.Id,
            rule.RuleCode,
            rule.RuleName,
            rule.RuleType,
            rule.SeverityLevel,
            rule.ConditionJson,
            rule.CalcSql,
            rule.DatasourceId,
            rule.DatasetId,
            rule.ChartId,
            rule.KpiId
        });
    }

    private static string BuildNotifyContent(AlertRule rule, AlertEvent evt)
    {
        return $"[{evt.SeverityLevel}] 预警触发：{rule.RuleName}，当前值={evt.CurrentValue}，规则={evt.ThresholdDesc}";
    }

    private async Task<decimal?> ExecuteChartMeasureAsync(Chart chart, CancellationToken ct)
    {
        var dataset = chart.Dataset;
        var datasource = dataset?.Datasource;
        if (dataset == null || datasource == null)
            return null;

        var baseSql = dataset.SqlText.Trim().TrimEnd(';');
        using var doc = ParseJsonObject(chart.ConfigJson);
        var root = doc.RootElement;

        string? metricField = null;
        var aggType = "sum";

        if (root.TryGetProperty("measures", out var measuresEl) && measuresEl.ValueKind == JsonValueKind.Array)
        {
            var firstMeasure = measuresEl.EnumerateArray().FirstOrDefault();
            if (firstMeasure.ValueKind == JsonValueKind.Object)
            {
                if (firstMeasure.TryGetProperty("field", out var fieldEl))
                    metricField = fieldEl.GetString();
                if (firstMeasure.TryGetProperty("aggType", out var aggEl))
                    aggType = aggEl.GetString() ?? "sum";
            }
        }

        if (string.IsNullOrWhiteSpace(metricField))
            return null;

        if (string.IsNullOrWhiteSpace(aggType) || aggType == "none")
            aggType = "sum";

        var sql = $"SELECT {aggType.ToUpper()}({metricField}) AS metric_value FROM ({baseSql}) t";
        return await ExecuteScalarAsync(datasource.Type, datasource.ConnString, sql, ct);
    }

    private async Task<decimal?> ExecuteScalarAsync(string dbType, string connString, string sql, CancellationToken ct)
    {
        using var conn = CreateConnection(dbType, connString);
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 60;
        var value = await cmd.ExecuteScalarAsync(ct);
        return ToDecimal(value);
    }

    private static DbConnection CreateConnection(string type, string connString)
    {
        return type.ToLower() switch
        {
            "postgres" or "postgresql" => new NpgsqlConnection(connString),
            "sqlserver" or "mssql" => new SqlConnection(connString),
            "mysql" => new MySqlConnection(EnsureMySqlConnStringParams(connString)),
            _ => throw new ArgumentException($"不支持的数据源类型: {type}")
        };
    }

    private static string EnsureMySqlConnStringParams(string connString)
    {
        if (connString.Contains("ConnectionReset", StringComparison.OrdinalIgnoreCase))
            return connString;

        return connString.TrimEnd(';') + ";ConnectionReset=false;AllowUserVariables=true;CharSet=utf8mb4";
    }

    private static decimal? ToDecimal(object? value)
    {
        if (value == null || value == DBNull.Value) return null;
        if (value is decimal d) return d;
        if (value is double db) return (decimal)db;
        if (value is float f) return (decimal)f;
        if (value is int i) return i;
        if (value is long l) return l;
        if (decimal.TryParse(value.ToString(), out var parsed)) return parsed;
        return null;
    }

    private static bool Compare(decimal value, string op, decimal target)
    {
        return op switch
        {
            ">" => value > target,
            ">=" => value >= target,
            "<" => value < target,
            "<=" => value <= target,
            "=" or "==" => value == target,
            "!=" or "<>" => value != target,
            _ => value >= target
        };
    }

    private static bool ValidateReadOnlySql(string sql, out string message)
    {
        var trimmed = sql.TrimStart();
        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            message = "仅允许执行 SELECT/WITH 查询";
            return false;
        }

        var forbidden = new[] { "INSERT ", "UPDATE ", "DELETE ", "DROP ", "ALTER ", "TRUNCATE ", "CREATE " };
        var upper = trimmed.ToUpperInvariant();
        if (forbidden.Any(f => upper.Contains(f)))
        {
            message = "检测到非查询语句，已拒绝执行";
            return false;
        }

        message = "ok";
        return true;
    }

    private static JsonDocument ParseJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return JsonDocument.Parse("{}");

        try
        {
            return JsonDocument.Parse(json);
        }
        catch
        {
            return JsonDocument.Parse("{}");
        }
    }

    private static string GetString(JsonDocument doc, string prop, string defaultValue)
    {
        if (doc.RootElement.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String)
            return el.GetString() ?? defaultValue;
        return defaultValue;
    }

    private static decimal GetDecimal(JsonDocument doc, string prop, decimal defaultValue)
    {
        if (!doc.RootElement.TryGetProperty(prop, out var el))
            return defaultValue;

        if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var num))
            return num;

        if (el.ValueKind == JsonValueKind.String && decimal.TryParse(el.GetString(), out var strNum))
            return strNum;

        return defaultValue;
    }

    private static List<string> ParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return new List<string>();

            return doc.RootElement
                .EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString()))
                .Select(x => x.GetString()!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }
}

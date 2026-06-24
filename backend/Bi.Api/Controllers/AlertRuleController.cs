using System.Text.Json;
using Bi.Api.Models;
using Bi.Application.Services;
using Bi.Domain.Entities;
using Bi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bi.Api.Controllers;

/// <summary>
/// 预警规则管理
/// </summary>
[ApiController]
[Route("api/v1/alert-rules")]
[Authorize]
public class AlertRuleController : ControllerBase
{
    private readonly BiDbContext _db;
    private readonly IAlertEngineService _alertEngine;

    public AlertRuleController(BiDbContext db, IAlertEngineService alertEngine)
    {
        _db = db;
        _alertEngine = alertEngine;
    }

    /// <summary>
    /// 获取规则列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AlertRuleListDto>>>> GetList(
        [FromQuery] string? keyword = null,
        [FromQuery] string? ruleStatus = null,
        [FromQuery] string? ruleType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.AlertRules.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.RuleCode.Contains(keyword) || x.RuleName.Contains(keyword));
        if (!string.IsNullOrWhiteSpace(ruleStatus))
            query = query.Where(x => x.RuleStatus == ruleStatus);
        if (!string.IsNullOrWhiteSpace(ruleType))
            query = query.Where(x => x.RuleType == ruleType);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(x => x.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AlertRuleListDto
            {
                Id = x.Id,
                RuleCode = x.RuleCode,
                RuleName = x.RuleName,
                RuleType = x.RuleType,
                SeverityLevel = x.SeverityLevel,
                RuleStatus = x.RuleStatus,
                DatasourceId = x.DatasourceId,
                ChartId = x.ChartId,
                KpiId = x.KpiId,
                LastCheckAt = x.LastCheckAt,
                NextCheckAt = x.NextCheckAt,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<PagedResult<AlertRuleListDto>>.Success(new PagedResult<AlertRuleListDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        }));
    }

    /// <summary>
    /// 获取规则详情
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<AlertRuleDetailDto>>> GetById(long id)
    {
        var entity = await _db.AlertRules.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
            return Ok(ApiResponse<AlertRuleDetailDto>.Fail("规则不存在", 404));

        var dto = new AlertRuleDetailDto
        {
            Id = entity.Id,
            RuleCode = entity.RuleCode,
            RuleName = entity.RuleName,
            RuleType = entity.RuleType,
            SeverityLevel = entity.SeverityLevel,
            RuleStatus = entity.RuleStatus,
            DatasourceId = entity.DatasourceId,
            DatasetId = entity.DatasetId,
            ChartId = entity.ChartId,
            KpiId = entity.KpiId,
            MetricField = entity.MetricField,
            DimensionField = entity.DimensionField,
            TimeField = entity.TimeField,
            StatGranularity = entity.StatGranularity,
            ConditionJson = ParseJsonElement(entity.ConditionJson, "{}"),
            CalcSql = entity.CalcSql,
            ScheduleType = entity.ScheduleType,
            CronExpr = entity.CronExpr,
            IntervalSeconds = entity.IntervalSeconds,
            Timezone = entity.Timezone,
            DedupMinutes = entity.DedupMinutes,
            CooldownMinutes = entity.CooldownMinutes,
            OwnerUserId = entity.OwnerUserId,
            NotifyChannels = ParseJsonElement(entity.NotifyChannels, "[]"),
            NotifyTemplate = entity.NotifyTemplate,
            LastCheckAt = entity.LastCheckAt,
            NextCheckAt = entity.NextCheckAt,
            Remark = entity.Remark,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };

        return Ok(ApiResponse<AlertRuleDetailDto>.Success(dto));
    }

    /// <summary>
    /// 新增规则
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<long>>> Create([FromBody] AlertRuleUpsertDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.RuleName))
            return Ok(ApiResponse<long>.Fail("规则名称不能为空"));

        var ruleCode = string.IsNullOrWhiteSpace(dto.RuleCode) ? BuildRuleCode() : dto.RuleCode.Trim();
        var exists = await _db.AlertRules.AnyAsync(x => x.RuleCode == ruleCode);
        if (exists)
            return Ok(ApiResponse<long>.Fail("规则编码已存在"));

        var now = DateTime.UtcNow;
        var entity = new AlertRule
        {
            RuleCode = ruleCode,
            RuleName = dto.RuleName.Trim(),
            RuleType = dto.RuleType,
            SeverityLevel = dto.SeverityLevel,
            RuleStatus = dto.RuleStatus,
            DatasourceId = dto.DatasourceId,
            DatasetId = dto.DatasetId,
            ChartId = dto.ChartId,
            KpiId = dto.KpiId,
            MetricField = dto.MetricField,
            DimensionField = dto.DimensionField,
            TimeField = dto.TimeField,
            StatGranularity = dto.StatGranularity,
            ConditionJson = dto.ConditionJson?.GetRawText() ?? "{}",
            CalcSql = dto.CalcSql,
            ScheduleType = dto.ScheduleType,
            CronExpr = dto.CronExpr,
            IntervalSeconds = dto.IntervalSeconds <= 0 ? 300 : dto.IntervalSeconds,
            Timezone = string.IsNullOrWhiteSpace(dto.Timezone) ? "Asia/Shanghai" : dto.Timezone,
            DedupMinutes = dto.DedupMinutes < 0 ? 0 : dto.DedupMinutes,
            CooldownMinutes = dto.CooldownMinutes < 0 ? 0 : dto.CooldownMinutes,
            OwnerUserId = dto.OwnerUserId,
            NotifyChannels = dto.NotifyChannels?.GetRawText() ?? "[]",
            NotifyTemplate = dto.NotifyTemplate,
            NextCheckAt = now,
            Remark = dto.Remark,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.AlertRules.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<long>.Success(entity.Id));
    }

    /// <summary>
    /// 更新规则
    /// </summary>
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<bool>>> Update(long id, [FromBody] AlertRuleUpsertDto dto)
    {
        var entity = await _db.AlertRules.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
            return Ok(ApiResponse<bool>.Fail("规则不存在", 404));

        if (string.IsNullOrWhiteSpace(dto.RuleName))
            return Ok(ApiResponse<bool>.Fail("规则名称不能为空"));

        if (!string.IsNullOrWhiteSpace(dto.RuleCode))
        {
            var code = dto.RuleCode.Trim();
            var exists = await _db.AlertRules.AnyAsync(x => x.RuleCode == code && x.Id != id);
            if (exists)
                return Ok(ApiResponse<bool>.Fail("规则编码已存在"));
            entity.RuleCode = code;
        }

        entity.RuleName = dto.RuleName.Trim();
        entity.RuleType = dto.RuleType;
        entity.SeverityLevel = dto.SeverityLevel;
        entity.RuleStatus = dto.RuleStatus;
        entity.DatasourceId = dto.DatasourceId;
        entity.DatasetId = dto.DatasetId;
        entity.ChartId = dto.ChartId;
        entity.KpiId = dto.KpiId;
        entity.MetricField = dto.MetricField;
        entity.DimensionField = dto.DimensionField;
        entity.TimeField = dto.TimeField;
        entity.StatGranularity = dto.StatGranularity;
        entity.ConditionJson = dto.ConditionJson?.GetRawText() ?? entity.ConditionJson;
        entity.CalcSql = dto.CalcSql;
        entity.ScheduleType = dto.ScheduleType;
        entity.CronExpr = dto.CronExpr;
        entity.IntervalSeconds = dto.IntervalSeconds <= 0 ? 300 : dto.IntervalSeconds;
        entity.Timezone = string.IsNullOrWhiteSpace(dto.Timezone) ? "Asia/Shanghai" : dto.Timezone;
        entity.DedupMinutes = dto.DedupMinutes < 0 ? 0 : dto.DedupMinutes;
        entity.CooldownMinutes = dto.CooldownMinutes < 0 ? 0 : dto.CooldownMinutes;
        entity.OwnerUserId = dto.OwnerUserId;
        entity.NotifyChannels = dto.NotifyChannels?.GetRawText() ?? entity.NotifyChannels;
        entity.NotifyTemplate = dto.NotifyTemplate;
        entity.Remark = dto.Remark;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<bool>.Success(true));
    }

    /// <summary>
    /// 删除规则
    /// </summary>
    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(long id)
    {
        var entity = await _db.AlertRules.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
            return Ok(ApiResponse<bool>.Fail("规则不存在", 404));

        _db.AlertRules.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<bool>.Success(true));
    }

    /// <summary>
    /// 启用规则
    /// </summary>
    [HttpPost("{id:long}/enable")]
    public async Task<ActionResult<ApiResponse<bool>>> Enable(long id)
    {
        var entity = await _db.AlertRules.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
            return Ok(ApiResponse<bool>.Fail("规则不存在", 404));

        entity.RuleStatus = AlertRuleStatuses.Enabled;
        entity.NextCheckAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<bool>.Success(true));
    }

    /// <summary>
    /// 停用规则
    /// </summary>
    [HttpPost("{id:long}/disable")]
    public async Task<ActionResult<ApiResponse<bool>>> Disable(long id)
    {
        var entity = await _db.AlertRules.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
            return Ok(ApiResponse<bool>.Fail("规则不存在", 404));

        entity.RuleStatus = AlertRuleStatuses.Disabled;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<bool>.Success(true));
    }

    /// <summary>
    /// 手工执行规则
    /// </summary>
    [HttpPost("{id:long}/run")]
    public async Task<ActionResult<ApiResponse<AlertRuleRunResultDto>>> RunNow(long id)
    {
        var result = await _alertEngine.RunRuleAsync(id, false);
        return Ok(ApiResponse<AlertRuleRunResultDto>.Success(new AlertRuleRunResultDto
        {
            Triggered = result.Triggered,
            EventId = result.EventId,
            Message = result.Message,
            CurrentValue = result.CurrentValue,
            BaselineValue = result.BaselineValue,
            CompareValue = result.CompareValue,
            ChangePct = result.ChangePct
        }));
    }

    private static string BuildRuleCode()
    {
        return $"AR{DateTime.UtcNow:yyyyMMddHHmmssfff}";
    }

    private static JsonElement ParseJsonElement(string? json, string defaultJson)
    {
        if (string.IsNullOrWhiteSpace(json))
            return JsonDocument.Parse(defaultJson).RootElement.Clone();

        try
        {
            return JsonDocument.Parse(json).RootElement.Clone();
        }
        catch
        {
            return JsonDocument.Parse(defaultJson).RootElement.Clone();
        }
    }
}


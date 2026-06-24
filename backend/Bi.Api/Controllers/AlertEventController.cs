using System.Security.Claims;
using System.Text.Json;
using Bi.Api.Models;
using Bi.Domain.Entities;
using Bi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bi.Api.Controllers;

/// <summary>
/// 预警事件中心
/// </summary>
[ApiController]
[Route("api/v1/alert-events")]
[Authorize]
public class AlertEventController : ControllerBase
{
    private readonly BiDbContext _db;

    public AlertEventController(BiDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 获取预警事件列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AlertEventListDto>>>> GetList(
        [FromQuery] string? status = null,
        [FromQuery] string? severity = null,
        [FromQuery] long? ruleId = null,
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.AlertEvents
            .Include(e => e.Rule)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.EventStatus == status);
        if (!string.IsNullOrWhiteSpace(severity))
            query = query.Where(e => e.SeverityLevel == severity);
        if (ruleId.HasValue)
            query = query.Where(e => e.RuleId == ruleId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(e =>
                e.EventNo.Contains(keyword) ||
                (e.Rule != null && e.Rule.RuleName.Contains(keyword)));

        var total = await query.CountAsync();
        var list = await query
            .OrderByDescending(e => e.TriggerTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new AlertEventListDto
            {
                Id = e.Id,
                EventNo = e.EventNo,
                RuleId = e.RuleId,
                RuleName = e.Rule != null ? e.Rule.RuleName : string.Empty,
                EventStatus = e.EventStatus,
                SeverityLevel = e.SeverityLevel,
                TriggerTime = e.TriggerTime,
                FirstTriggeredAt = e.FirstTriggeredAt,
                LastTriggeredAt = e.LastTriggeredAt,
                TriggerCount = e.TriggerCount,
                CurrentValue = e.CurrentValue,
                BaselineValue = e.BaselineValue,
                CompareValue = e.CompareValue,
                ChangePct = e.ChangePct,
                ThresholdDesc = e.ThresholdDesc,
                DimensionValueJson = ParseJsonElement(e.DimensionValueJson, "{}"),
                SuggestionText = e.SuggestionText,
                AckBy = e.AckBy,
                AckAt = e.AckAt,
                ResolvedBy = e.ResolvedBy,
                ResolvedAt = e.ResolvedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<PagedResult<AlertEventListDto>>.Success(new PagedResult<AlertEventListDto>
        {
            Items = list,
            Total = total,
            Page = page,
            PageSize = pageSize
        }));
    }

    /// <summary>
    /// 获取事件详情
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<AlertEventDetailDto>>> GetById(long id)
    {
        var entity = await _db.AlertEvents
            .Include(e => e.Rule)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (entity == null)
            return Ok(ApiResponse<AlertEventDetailDto>.Fail("事件不存在", 404));

        var dto = new AlertEventDetailDto
        {
            Id = entity.Id,
            EventNo = entity.EventNo,
            RuleId = entity.RuleId,
            RuleName = entity.Rule?.RuleName ?? string.Empty,
            EventStatus = entity.EventStatus,
            SeverityLevel = entity.SeverityLevel,
            TriggerTime = entity.TriggerTime,
            FirstTriggeredAt = entity.FirstTriggeredAt,
            LastTriggeredAt = entity.LastTriggeredAt,
            TriggerCount = entity.TriggerCount,
            CurrentValue = entity.CurrentValue,
            BaselineValue = entity.BaselineValue,
            CompareValue = entity.CompareValue,
            ChangePct = entity.ChangePct,
            ThresholdDesc = entity.ThresholdDesc,
            DimensionValueJson = ParseJsonElement(entity.DimensionValueJson, "{}"),
            SuggestionText = entity.SuggestionText,
            AckBy = entity.AckBy,
            AckAt = entity.AckAt,
            ResolvedBy = entity.ResolvedBy,
            ResolvedAt = entity.ResolvedAt,
            RuleSnapshotJson = ParseJsonElement(entity.RuleSnapshotJson, "{}"),
            EvidenceJson = ParseJsonElement(entity.EvidenceJson, "{}"),
            ResolutionNote = entity.ResolutionNote,
            IsNotified = entity.IsNotified,
            NotifiedAt = entity.NotifiedAt
        };

        return Ok(ApiResponse<AlertEventDetailDto>.Success(dto));
    }

    /// <summary>
    /// 确认事件
    /// </summary>
    [HttpPost("{id:long}/ack")]
    public async Task<ActionResult<ApiResponse<bool>>> Ack(long id, [FromBody] AlertEventHandleDto? dto)
    {
        var entity = await _db.AlertEvents.FirstOrDefaultAsync(e => e.Id == id);
        if (entity == null)
            return Ok(ApiResponse<bool>.Fail("事件不存在", 404));
        if (!CanTransition(entity.EventStatus, AlertEventStatuses.Acknowledged))
            return Ok(ApiResponse<bool>.Fail($"当前状态[{entity.EventStatus}]不允许确认"));

        var userId = GetCurrentUserId();
        entity.EventStatus = AlertEventStatuses.Acknowledged;
        entity.AckBy = userId;
        entity.AckAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        AddAction(entity.Id, "ack", userId, dto?.Note, null);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<bool>.Success(true));
    }

    /// <summary>
    /// 解决事件
    /// </summary>
    [HttpPost("{id:long}/resolve")]
    public async Task<ActionResult<ApiResponse<bool>>> Resolve(long id, [FromBody] AlertEventHandleDto? dto)
    {
        var entity = await _db.AlertEvents.FirstOrDefaultAsync(e => e.Id == id);
        if (entity == null)
            return Ok(ApiResponse<bool>.Fail("事件不存在", 404));
        if (!CanTransition(entity.EventStatus, AlertEventStatuses.Resolved))
            return Ok(ApiResponse<bool>.Fail($"当前状态[{entity.EventStatus}]不允许解决"));

        var userId = GetCurrentUserId();
        entity.EventStatus = AlertEventStatuses.Resolved;
        entity.ResolvedBy = userId;
        entity.ResolvedAt = DateTime.UtcNow;
        entity.ResolutionNote = dto?.Note;
        entity.UpdatedAt = DateTime.UtcNow;

        AddAction(entity.Id, "resolve", userId, dto?.Note, null);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<bool>.Success(true));
    }

    /// <summary>
    /// 忽略事件
    /// </summary>
    [HttpPost("{id:long}/ignore")]
    public async Task<ActionResult<ApiResponse<bool>>> Ignore(long id, [FromBody] AlertEventHandleDto? dto)
    {
        var entity = await _db.AlertEvents.FirstOrDefaultAsync(e => e.Id == id);
        if (entity == null)
            return Ok(ApiResponse<bool>.Fail("事件不存在", 404));
        if (!CanTransition(entity.EventStatus, AlertEventStatuses.Ignored))
            return Ok(ApiResponse<bool>.Fail($"当前状态[{entity.EventStatus}]不允许忽略"));

        var userId = GetCurrentUserId();
        entity.EventStatus = AlertEventStatuses.Ignored;
        entity.ResolvedBy = userId;
        entity.ResolvedAt = DateTime.UtcNow;
        entity.ResolutionNote = dto?.Note;
        entity.UpdatedAt = DateTime.UtcNow;

        AddAction(entity.Id, "ignore", userId, dto?.Note, null);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<bool>.Success(true));
    }

    /// <summary>
    /// 关闭事件
    /// </summary>
    [HttpPost("{id:long}/close")]
    public async Task<ActionResult<ApiResponse<bool>>> Close(long id, [FromBody] AlertEventHandleDto? dto)
    {
        var entity = await _db.AlertEvents.FirstOrDefaultAsync(e => e.Id == id);
        if (entity == null)
            return Ok(ApiResponse<bool>.Fail("事件不存在", 404));
        if (!CanTransition(entity.EventStatus, AlertEventStatuses.Closed))
            return Ok(ApiResponse<bool>.Fail($"当前状态[{entity.EventStatus}]不允许关闭"));

        var userId = GetCurrentUserId();
        entity.EventStatus = AlertEventStatuses.Closed;
        entity.UpdatedAt = DateTime.UtcNow;

        AddAction(entity.Id, "close", userId, dto?.Note, null);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<bool>.Success(true));
    }

    /// <summary>
    /// 追加事件动作
    /// </summary>
    [HttpPost("{id:long}/actions")]
    public async Task<ActionResult<ApiResponse<bool>>> AddEventAction(long id, [FromBody] AlertEventActionCreateDto dto)
    {
        var exists = await _db.AlertEvents.AnyAsync(e => e.Id == id);
        if (!exists)
            return Ok(ApiResponse<bool>.Fail("事件不存在", 404));

        var userId = GetCurrentUserId();
        AddAction(id, dto.ActionType, userId, dto.ActionNote, dto.ActionPayload?.GetRawText());
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<bool>.Success(true));
    }

    /// <summary>
    /// 获取事件动作轨迹
    /// </summary>
    [HttpGet("{id:long}/actions")]
    public async Task<ActionResult<ApiResponse<List<AlertEventActionDto>>>> GetEventActions(long id)
    {
        var exists = await _db.AlertEvents.AnyAsync(e => e.Id == id);
        if (!exists)
            return Ok(ApiResponse<List<AlertEventActionDto>>.Fail("事件不存在", 404));

        var actions = await _db.AlertEventActions
            .Where(a => a.EventId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AlertEventActionDto
            {
                Id = a.Id,
                EventId = a.EventId,
                ActionType = a.ActionType,
                ActionUserId = a.ActionUserId,
                ActionNote = a.ActionNote,
                ActionPayload = ParseJsonElement(a.ActionPayload, "{}"),
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<List<AlertEventActionDto>>.Success(actions));
    }

    private void AddAction(long eventId, string actionType, long? userId, string? note, string? payloadJson)
    {
        _db.AlertEventActions.Add(new AlertEventAction
        {
            EventId = eventId,
            ActionType = string.IsNullOrWhiteSpace(actionType) ? "comment" : actionType,
            ActionUserId = userId,
            ActionNote = note,
            ActionPayload = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }

    private long? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (long.TryParse(userIdClaim, out var userId))
            return userId;
        return null;
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

    private static bool CanTransition(string currentStatus, string targetStatus)
    {
        if (currentStatus == targetStatus)
            return false;

        return (currentStatus, targetStatus) switch
        {
            (AlertEventStatuses.Open, AlertEventStatuses.Acknowledged) => true,
            (AlertEventStatuses.Open, AlertEventStatuses.Resolved) => true,
            (AlertEventStatuses.Open, AlertEventStatuses.Ignored) => true,
            (AlertEventStatuses.Open, AlertEventStatuses.Closed) => true,
            (AlertEventStatuses.Acknowledged, AlertEventStatuses.Resolved) => true,
            (AlertEventStatuses.Acknowledged, AlertEventStatuses.Ignored) => true,
            (AlertEventStatuses.Acknowledged, AlertEventStatuses.Closed) => true,
            (AlertEventStatuses.Resolved, AlertEventStatuses.Closed) => true,
            (AlertEventStatuses.Ignored, AlertEventStatuses.Closed) => true,
            _ => false
        };
    }
}

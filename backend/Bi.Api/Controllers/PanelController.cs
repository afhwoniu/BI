using Bi.Api.Models;
using Bi.Domain.Entities;
using Bi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bi.Api.Controllers;

/// <summary>
/// 分析面板管理控制器
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class PanelController : ControllerBase
{
    private readonly BiDbContext _db;

    public PanelController(BiDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 获取面板列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<PanelDto>>>> GetList([FromQuery] string? panelType = null)
    {
        var query = _db.Panels.Include(p => p.Items).AsQueryable();
        if (!string.IsNullOrEmpty(panelType))
            query = query.Where(p => p.PanelType == panelType);

        var list = await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PanelDto
            {
                Id = p.Id,
                Name = p.Name,
                PanelType = p.PanelType,
                Remark = p.Remark,
                ItemCount = p.Items.Count,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            })
            .ToListAsync();
        return Ok(ApiResponse<List<PanelDto>>.Success(list));
    }

    /// <summary>
    /// 获取面板详情（含子项和图表信息）
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<PanelDetailDto>>> GetById(long id)
    {
        var panel = await _db.Panels
            .Include(p => p.Items)
                .ThenInclude(i => i.Chart)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (panel == null)
            return Ok(ApiResponse<PanelDetailDto>.Fail("面板不存在", 404));

        return Ok(ApiResponse<PanelDetailDto>.Success(new PanelDetailDto
        {
            Id = panel.Id,
            Name = panel.Name,
            PanelType = panel.PanelType,
            ConfigJson = panel.ConfigJson,
            Remark = panel.Remark,
            Items = panel.Items.OrderBy(i => i.SortOrder).Select(i => new PanelItemDto
            {
                Id = i.Id,
                ChartId = i.ChartId,
                ChartName = i.Chart?.Name,
                ChartType = i.Chart?.ChartType,
                LayoutJson = i.LayoutJson,
                SortOrder = i.SortOrder
            }).ToList()
        }));
    }

    /// <summary>
    /// 新增面板
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<long>>> Create([FromBody] PanelCreateDto dto)
    {
        var entity = new Panel
        {
            Name = dto.Name,
            PanelType = dto.PanelType,
            ConfigJson = dto.ConfigJson,
            Remark = dto.Remark,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Panels.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<long>.Success(entity.Id));
    }

    /// <summary>
    /// 更新面板（含子项）
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> Update(long id, [FromBody] PanelUpdateDto dto)
    {
        var entity = await _db.Panels.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null)
            return Ok(ApiResponse<bool>.Fail("面板不存在", 404));

        entity.Name = dto.Name;
        entity.PanelType = dto.PanelType;
        entity.ConfigJson = dto.ConfigJson;
        entity.Remark = dto.Remark;
        entity.UpdatedAt = DateTime.UtcNow;

        // 更新子项：先删后加
        if (dto.Items != null)
        {
            _db.PanelItems.RemoveRange(entity.Items);
            var items = dto.Items.Select((item, i) => new PanelItem
            {
                PanelId = entity.Id,
                ChartId = item.ChartId,
                LayoutJson = item.LayoutJson,
                SortOrder = item.SortOrder > 0 ? item.SortOrder : i,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();
            _db.PanelItems.AddRange(items);
        }

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<bool>.Success(true));
    }

    /// <summary>
    /// 删除面板
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(long id)
    {
        var entity = await _db.Panels.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null)
            return Ok(ApiResponse<bool>.Fail("面板不存在", 404));

        _db.PanelItems.RemoveRange(entity.Items);
        _db.Panels.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<bool>.Success(true));
    }
}


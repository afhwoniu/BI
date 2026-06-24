using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bi.Infrastructure.Data;
using System.Security.Claims;

namespace Bi.Api.Controllers;

/// <summary>
/// 门户访问控制器（公开访问发布内容）
/// </summary>
[ApiController]
[Route("api/v1/portal")]
public class PortalController : ControllerBase
{
    private readonly BiDbContext _db;

    public PortalController(BiDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 获取公开菜单树
    /// </summary>
    [HttpGet("menus")]
    public async Task<IActionResult> GetPublicMenus()
    {
        var allMenus = await _db.Menus
            .Include(m => m.Publish)
            .Where(m => m.IsVisible)
            .OrderBy(m => m.SortOrder)
            .ToListAsync();

        // 过滤掉关联了已禁用发布的菜单
        var validMenus = allMenus.Where(m =>
            m.PublishId == null ||
            (m.Publish != null && m.Publish.IsEnabled && m.Publish.AccessScope == "public")
        ).ToList();

        var tree = BuildTree(validMenus, 0);
        return Ok(new { code = 0, message = "success", data = tree });
    }

    /// <summary>
    /// 获取当前用户可访问的菜单树（基于角色权限）
    /// </summary>
    [HttpGet("user-menus")]
    [Authorize]
    public async Task<IActionResult> GetUserMenus()
    {
        var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        // 获取用户角色
        var roleIds = await _db.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync();

        // 获取角色对应的菜单ID
        var menuIds = await _db.RoleMenus.Where(rm => roleIds.Contains(rm.RoleId)).Select(rm => rm.MenuId).Distinct().ToListAsync();

        // 获取菜单
        var allMenus = await _db.Menus
            .Include(m => m.Publish)
            .Where(m => m.IsVisible && menuIds.Contains(m.Id))
            .OrderBy(m => m.SortOrder)
            .ToListAsync();

        // 过滤掉关联了已禁用发布的菜单
        var validMenus = allMenus.Where(m =>
            m.PublishId == null ||
            (m.Publish != null && m.Publish.IsEnabled)
        ).ToList();

        var tree = BuildTree(validMenus, 0);
        return Ok(new { code = 0, message = "success", data = tree });
    }

    /// <summary>
    /// 通过Token访问发布内容
    /// </summary>
    [HttpGet("view/{token}")]
    public async Task<IActionResult> ViewByToken(string token, [FromQuery] string? password = null)
    {
        var publish = await _db.Publishes.FirstOrDefaultAsync(p => p.AccessToken == token);
        if (publish == null)
            return NotFound(new { code = 404, message = "内容不存在或链接已失效" });

        if (!publish.IsEnabled)
            return BadRequest(new { code = 400, message = "该内容已被禁用" });

        if (publish.ExpireAt.HasValue && publish.ExpireAt < DateTime.UtcNow)
            return BadRequest(new { code = 400, message = "该链接已过期" });

        if (!string.IsNullOrEmpty(publish.AccessPassword) && publish.AccessPassword != password)
            return Unauthorized(new { code = 401, message = "访问密码错误", needPassword = true });

        // 更新访问统计
        publish.ViewCount++;
        publish.LastViewedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // 根据对象类型返回不同内容
        object? content = null;
        switch (publish.ObjectType)
        {
            case "report":
                content = await GetReportContent(publish.ObjectId);
                break;
            case "panel":
                content = await GetPanelContent(publish.ObjectId);
                break;
            case "chart":
                content = await GetChartContent(publish.ObjectId);
                break;
        }

        return Ok(new { code = 0, message = "success", data = new
        {
            publish.Id, publish.Title, publish.ObjectType, publish.ObjectId,
            Content = content
        }});
    }

    private async Task<object?> GetReportContent(long reportId)
    {
        var report = await _db.Reports
            .Include(r => r.Pages)
            .ThenInclude(p => p.Items)
            .FirstOrDefaultAsync(r => r.Id == reportId);

        if (report == null) return null;

        return new
        {
            report.Id, report.Name, report.ReportType, report.CoverImage,
            Pages = report.Pages.OrderBy(p => p.SortOrder).Select(p => new
            {
                p.Id, p.Title, p.SortOrder,
                Items = p.Items.OrderBy(i => i.SortOrder).Select(i => new
                {
                    i.Id, i.ItemType, i.ChartId, i.PanelId, i.TextContent,
                    i.ImageUrl, i.LayoutJson, i.StyleJson
                })
            })
        };
    }

    private async Task<object?> GetPanelContent(long panelId)
    {
        var panel = await _db.Panels
            .Include(p => p.Items)
            .ThenInclude(i => i.Chart)
            .FirstOrDefaultAsync(p => p.Id == panelId);

        if (panel == null) return null;

        return new
        {
            panel.Id, panel.Name, panel.PanelType, panel.ConfigJson,
            Items = panel.Items.OrderBy(i => i.SortOrder).Select(i => new
            {
                i.Id, i.ChartId, i.LayoutJson,
                ChartName = i.Chart?.Name,
                ChartType = i.Chart?.ChartType
            })
        };
    }

    private async Task<object?> GetChartContent(long chartId)
    {
        var chart = await _db.Charts.FindAsync(chartId);
        if (chart == null) return null;

        return new { chart.Id, chart.Name, chart.ChartType, chart.ConfigJson };
    }

    private List<object> BuildTree(List<Bi.Domain.Entities.SysMenu> menus, long parentId)
    {
        return menus
            .Where(m => m.ParentId == parentId)
            .Select(m => new
            {
                m.Id, m.Name, m.MenuType, m.Icon, m.LinkUrl,
                m.PublishId, PublishToken = m.Publish?.AccessToken,
                ObjectType = m.Publish?.ObjectType,
                Children = BuildTree(menus, m.Id)
            })
            .Cast<object>()
            .ToList();
    }
}


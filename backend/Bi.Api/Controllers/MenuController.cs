using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bi.Domain.Entities;
using Bi.Infrastructure.Data;

namespace Bi.Api.Controllers;

/// <summary>
/// 菜单管理控制器
/// </summary>
[ApiController]
[Route("api/v1/menus")]
[Authorize]
public class MenuController : ControllerBase
{
    private readonly BiDbContext _db;

    public MenuController(BiDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 获取菜单树
    /// </summary>
    [HttpGet("tree")]
    public async Task<IActionResult> GetMenuTree()
    {
        var allMenus = await _db.Menus
            .Include(m => m.Publish)
            .OrderBy(m => m.SortOrder)
            .ToListAsync();

        var tree = BuildTree(allMenus, 0);
        return Ok(new { code = 0, message = "success", data = tree });
    }

    /// <summary>
    /// 获取菜单列表（平铺）
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMenuList()
    {
        var menus = await _db.Menus
            .OrderBy(m => m.SortOrder)
            .Select(m => new
            {
                m.Id,
                m.Name,
                m.ParentId,
                m.MenuType,
                m.Icon,
                m.LinkUrl,
                m.PublishId,
                m.SortOrder,
                m.IsVisible,
                m.Remark
            })
            .ToListAsync();

        return Ok(new { code = 0, message = "success", data = menus });
    }

    /// <summary>
    /// 创建菜单
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateMenu([FromBody] MenuCreateDto dto)
    {
        var menu = new SysMenu
        {
            Name = dto.Name,
            ParentId = dto.ParentId,
            MenuType = dto.MenuType ?? "folder",
            Icon = dto.Icon,
            LinkUrl = dto.LinkUrl,
            PublishId = dto.PublishId,
            SortOrder = dto.SortOrder,
            IsVisible = dto.IsVisible ?? true,
            Remark = dto.Remark,
            CreatedAt = DateTime.UtcNow
        };

        _db.Menus.Add(menu);
        await _db.SaveChangesAsync();

        return Ok(new { code = 0, message = "创建成功", data = new { id = menu.Id } });
    }

    /// <summary>
    /// 更新菜单
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMenu(long id, [FromBody] MenuCreateDto dto)
    {
        var menu = await _db.Menus.FindAsync(id);
        if (menu == null)
            return NotFound(new { code = 404, message = "菜单不存在" });

        menu.Name = dto.Name;
        menu.ParentId = dto.ParentId;
        menu.MenuType = dto.MenuType ?? menu.MenuType;
        menu.Icon = dto.Icon;
        menu.LinkUrl = dto.LinkUrl;
        menu.PublishId = dto.PublishId;
        menu.SortOrder = dto.SortOrder;
        menu.IsVisible = dto.IsVisible ?? menu.IsVisible;
        menu.Remark = dto.Remark;
        menu.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "更新成功" });
    }

    /// <summary>
    /// 删除菜单
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMenu(long id)
    {
        var menu = await _db.Menus.FindAsync(id);
        if (menu == null)
            return NotFound(new { code = 404, message = "菜单不存在" });

        // 检查是否有子菜单
        var hasChildren = await _db.Menus.AnyAsync(m => m.ParentId == id);
        if (hasChildren)
            return BadRequest(new { code = 400, message = "请先删除子菜单" });

        _db.Menus.Remove(menu);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "删除成功" });
    }

    private List<object> BuildTree(List<SysMenu> menus, long parentId)
    {
        return menus
            .Where(m => m.ParentId == parentId)
            .Select(m => new
            {
                m.Id, m.Name, m.ParentId, m.MenuType, m.Icon, m.LinkUrl,
                m.PublishId, m.SortOrder, m.IsVisible, m.Remark,
                PublishTitle = m.Publish?.Title,
                Children = BuildTree(menus, m.Id)
            })
            .Cast<object>()
            .ToList();
    }
}

public class MenuCreateDto
{
    public string Name { get; set; } = string.Empty;
    public long ParentId { get; set; }
    public string? MenuType { get; set; }
    public string? Icon { get; set; }
    public string? LinkUrl { get; set; }
    public long? PublishId { get; set; }
    public int SortOrder { get; set; }
    public bool? IsVisible { get; set; }
    public string? Remark { get; set; }
}


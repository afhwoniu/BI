using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bi.Domain.Entities;
using Bi.Infrastructure.Data;

namespace Bi.Api.Controllers;

/// <summary>
/// 角色管理控制器
/// </summary>
[ApiController]
[Route("api/v1/roles")]
[Authorize]
public class RoleController : ControllerBase
{
    private readonly BiDbContext _db;

    public RoleController(BiDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 获取角色列表
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRoleList()
    {
        var list = await _db.Roles
            .OrderBy(r => r.SortOrder)
            .Select(r => new { r.Id, r.RoleCode, r.RoleName, r.Remark, r.SortOrder, r.IsEnabled, r.CreatedAt })
            .ToListAsync();
        return Ok(new { code = 0, message = "success", data = list });
    }

    /// <summary>
    /// 创建角色
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateRole([FromBody] RoleCreateDto dto)
    {
        if (await _db.Roles.AnyAsync(r => r.RoleCode == dto.RoleCode))
            return BadRequest(new { code = 400, message = "角色编码已存在" });

        var role = new SysRole
        {
            RoleCode = dto.RoleCode,
            RoleName = dto.RoleName,
            Remark = dto.Remark,
            SortOrder = dto.SortOrder,
            IsEnabled = dto.IsEnabled ?? true,
            CreatedAt = DateTime.UtcNow
        };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "创建成功", data = new { id = role.Id } });
    }

    /// <summary>
    /// 更新角色
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRole(long id, [FromBody] RoleCreateDto dto)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role == null) return NotFound(new { code = 404, message = "角色不存在" });

        role.RoleName = dto.RoleName;
        role.Remark = dto.Remark;
        role.SortOrder = dto.SortOrder;
        role.IsEnabled = dto.IsEnabled ?? role.IsEnabled;
        role.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "更新成功" });
    }

    /// <summary>
    /// 删除角色
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRole(long id)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role == null) return NotFound(new { code = 404, message = "角色不存在" });

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "删除成功" });
    }

    /// <summary>
    /// 获取角色菜单
    /// </summary>
    [HttpGet("{id}/menus")]
    public async Task<IActionResult> GetRoleMenus(long id)
    {
        var menuIds = await _db.RoleMenus.Where(rm => rm.RoleId == id).Select(rm => rm.MenuId).ToListAsync();
        return Ok(new { code = 0, message = "success", data = menuIds });
    }

    /// <summary>
    /// 设置角色菜单
    /// </summary>
    [HttpPut("{id}/menus")]
    public async Task<IActionResult> SetRoleMenus(long id, [FromBody] List<long> menuIds)
    {
        var existing = await _db.RoleMenus.Where(rm => rm.RoleId == id).ToListAsync();
        _db.RoleMenus.RemoveRange(existing);

        foreach (var menuId in menuIds)
        {
            _db.RoleMenus.Add(new SysRoleMenu { RoleId = id, MenuId = menuId });
        }
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "设置成功" });
    }
}

public class RoleCreateDto
{
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public int SortOrder { get; set; }
    public bool? IsEnabled { get; set; }
}


using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bi.Domain.Entities;
using Bi.Infrastructure.Data;
using System.Security.Cryptography;
using System.Text;

namespace Bi.Api.Controllers;

/// <summary>
/// 用户管理控制器
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly BiDbContext _db;

    public UserController(BiDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 获取用户列表
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUserList([FromQuery] long? orgId = null)
    {
        var query = _db.SysUsers.Include(u => u.Org).AsQueryable();
        if (orgId.HasValue)
            query = query.Where(u => u.OrgId == orgId);

        var list = await query
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new
            {
                u.Id, u.Username, u.RealName, u.Email, u.Phone, u.Avatar,
                u.IsEnabled, u.LastLoginAt, u.OrgId, OrgName = u.Org != null ? u.Org.OrgName : null, u.CreatedAt
            })
            .ToListAsync();
        return Ok(new { code = 0, message = "success", data = list });
    }

    /// <summary>
    /// 创建用户
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] UserCreateDto dto)
    {
        if (await _db.SysUsers.AnyAsync(u => u.Username == dto.Username))
            return BadRequest(new { code = 400, message = "用户名已存在" });

        var user = new SysUser
        {
            Username = dto.Username,
            PasswordHash = HashPassword(dto.Password ?? "123456"),
            RealName = dto.RealName,
            Email = dto.Email,
            Phone = dto.Phone,
            OrgId = dto.OrgId,
            IsEnabled = dto.IsEnabled ?? true,
            CreatedAt = DateTime.UtcNow
        };
        _db.SysUsers.Add(user);
        await _db.SaveChangesAsync();

        // 设置角色
        if (dto.RoleIds?.Any() == true)
        {
            foreach (var roleId in dto.RoleIds)
            {
                _db.UserRoles.Add(new SysUserRole { UserId = user.Id, RoleId = roleId });
            }
            await _db.SaveChangesAsync();
        }

        return Ok(new { code = 0, message = "创建成功", data = new { id = user.Id } });
    }

    /// <summary>
    /// 更新用户
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(long id, [FromBody] UserCreateDto dto)
    {
        var user = await _db.SysUsers.FindAsync(id);
        if (user == null) return NotFound(new { code = 404, message = "用户不存在" });

        user.RealName = dto.RealName;
        user.Email = dto.Email;
        user.Phone = dto.Phone;
        user.OrgId = dto.OrgId;
        user.IsEnabled = dto.IsEnabled ?? user.IsEnabled;
        user.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(dto.Password))
            user.PasswordHash = HashPassword(dto.Password);

        await _db.SaveChangesAsync();

        // 更新角色
        if (dto.RoleIds != null)
        {
            var existing = await _db.UserRoles.Where(ur => ur.UserId == id).ToListAsync();
            _db.UserRoles.RemoveRange(existing);
            foreach (var roleId in dto.RoleIds)
            {
                _db.UserRoles.Add(new SysUserRole { UserId = id, RoleId = roleId });
            }
            await _db.SaveChangesAsync();
        }

        return Ok(new { code = 0, message = "更新成功" });
    }

    /// <summary>
    /// 删除用户
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(long id)
    {
        var user = await _db.SysUsers.FindAsync(id);
        if (user == null) return NotFound(new { code = 404, message = "用户不存在" });

        _db.SysUsers.Remove(user);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "删除成功" });
    }

    /// <summary>
    /// 获取用户角色
    /// </summary>
    [HttpGet("{id}/roles")]
    public async Task<IActionResult> GetUserRoles(long id)
    {
        var roleIds = await _db.UserRoles.Where(ur => ur.UserId == id).Select(ur => ur.RoleId).ToListAsync();
        return Ok(new { code = 0, message = "success", data = roleIds });
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}

public class UserCreateDto
{
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? RealName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public long? OrgId { get; set; }
    public bool? IsEnabled { get; set; }
    public List<long>? RoleIds { get; set; }
}


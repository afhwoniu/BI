using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bi.Domain.Entities;
using Bi.Infrastructure.Data;
using System.Security.Claims;

namespace Bi.Api.Controllers;

/// <summary>
/// 发布管理控制器
/// </summary>
[ApiController]
[Route("api/v1/publishes")]
[Authorize]
public class PublishController : ControllerBase
{
    private readonly BiDbContext _db;

    public PublishController(BiDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 获取发布列表
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPublishList([FromQuery] string? objectType = null)
    {
        var query = _db.Publishes.AsQueryable();
        if (!string.IsNullOrEmpty(objectType))
            query = query.Where(p => p.ObjectType == objectType);

        var list = await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id, p.Title, p.ObjectType, p.ObjectId, p.AccessScope,
                p.AccessToken, p.IsEnabled, p.ViewCount, p.LastViewedAt,
                p.ExpireAt, p.CreatedAt, p.Remark
            })
            .ToListAsync();

        return Ok(new { code = 0, message = "success", data = list });
    }

    /// <summary>
    /// 获取发布详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetPublish(long id)
    {
        var publish = await _db.Publishes.FindAsync(id);
        if (publish == null)
            return NotFound(new { code = 404, message = "发布记录不存在" });

        return Ok(new { code = 0, message = "success", data = publish });
    }

    /// <summary>
    /// 创建发布
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreatePublish([FromBody] PublishCreateDto dto)
    {
        var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        var publish = new BiPublish
        {
            Title = dto.Title,
            ObjectType = dto.ObjectType,
            ObjectId = dto.ObjectId,
            AccessScope = dto.AccessScope ?? "private",
            AccessToken = GenerateToken(),
            AccessPassword = dto.AccessPassword,
            ExpireAt = dto.ExpireAt,
            IsEnabled = true,
            PublishedBy = userId,
            AllowedRoles = dto.AllowedRoles,
            Remark = dto.Remark,
            CreatedAt = DateTime.UtcNow
        };

        _db.Publishes.Add(publish);
        await _db.SaveChangesAsync();

        return Ok(new { code = 0, message = "发布成功", data = new { id = publish.Id, token = publish.AccessToken } });
    }

    /// <summary>
    /// 更新发布
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePublish(long id, [FromBody] PublishCreateDto dto)
    {
        var publish = await _db.Publishes.FindAsync(id);
        if (publish == null)
            return NotFound(new { code = 404, message = "发布记录不存在" });

        publish.Title = dto.Title;
        publish.AccessScope = dto.AccessScope ?? publish.AccessScope;
        publish.AccessPassword = dto.AccessPassword;
        publish.ExpireAt = dto.ExpireAt;
        publish.AllowedRoles = dto.AllowedRoles;
        publish.Remark = dto.Remark;
        publish.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "更新成功" });
    }

    /// <summary>
    /// 删除发布（取消发布）
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePublish(long id)
    {
        var publish = await _db.Publishes.FindAsync(id);
        if (publish == null)
            return NotFound(new { code = 404, message = "发布记录不存在" });

        _db.Publishes.Remove(publish);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "取消发布成功" });
    }

    /// <summary>
    /// 启用/禁用发布
    /// </summary>
    [HttpPut("{id}/toggle")]
    public async Task<IActionResult> TogglePublish(long id)
    {
        var publish = await _db.Publishes.FindAsync(id);
        if (publish == null)
            return NotFound(new { code = 404, message = "发布记录不存在" });

        publish.IsEnabled = !publish.IsEnabled;
        publish.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { code = 0, message = publish.IsEnabled ? "已启用" : "已禁用" });
    }

    /// <summary>
    /// 重新生成访问Token
    /// </summary>
    [HttpPost("{id}/regenerate-token")]
    public async Task<IActionResult> RegenerateToken(long id)
    {
        var publish = await _db.Publishes.FindAsync(id);
        if (publish == null)
            return NotFound(new { code = 404, message = "发布记录不存在" });

        publish.AccessToken = GenerateToken();
        publish.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { code = 0, message = "Token已更新", data = new { token = publish.AccessToken } });
    }

    private static string GenerateToken() => Guid.NewGuid().ToString("N")[..16];
}

public class PublishCreateDto
{
    public string Title { get; set; } = string.Empty;
    public string ObjectType { get; set; } = "report";
    public long ObjectId { get; set; }
    public string? AccessScope { get; set; }
    public string? AccessPassword { get; set; }
    public DateTime? ExpireAt { get; set; }
    public string? AllowedRoles { get; set; }
    public string? Remark { get; set; }
}


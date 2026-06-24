using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bi.Domain.Entities;
using Bi.Infrastructure.Data;

namespace Bi.Api.Controllers;

/// <summary>
/// 组织架构管理控制器
/// </summary>
[ApiController]
[Route("api/v1/orgs")]
[Authorize]
public class OrgController : ControllerBase
{
    private readonly BiDbContext _db;

    public OrgController(BiDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 获取组织树
    /// </summary>
    [HttpGet("tree")]
    public async Task<IActionResult> GetOrgTree()
    {
        var allOrgs = await _db.Orgs.OrderBy(o => o.SortOrder).ToListAsync();
        var tree = BuildTree(allOrgs, 0);
        return Ok(new { code = 0, message = "success", data = tree });
    }

    /// <summary>
    /// 获取组织列表（平铺）
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetOrgList()
    {
        var list = await _db.Orgs
            .OrderBy(o => o.SortOrder)
            .Select(o => new { o.Id, o.OrgCode, o.OrgName, o.ParentId, o.OrgType, o.SortOrder, o.IsEnabled, o.Remark })
            .ToListAsync();
        return Ok(new { code = 0, message = "success", data = list });
    }

    /// <summary>
    /// 创建组织
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateOrg([FromBody] OrgCreateDto dto)
    {
        if (await _db.Orgs.AnyAsync(o => o.OrgCode == dto.OrgCode))
            return BadRequest(new { code = 400, message = "组织编码已存在" });

        var org = new SysOrg
        {
            OrgCode = dto.OrgCode,
            OrgName = dto.OrgName,
            ParentId = dto.ParentId,
            OrgType = dto.OrgType ?? "dept",
            SortOrder = dto.SortOrder,
            IsEnabled = dto.IsEnabled ?? true,
            Remark = dto.Remark,
            CreatedAt = DateTime.UtcNow
        };
        _db.Orgs.Add(org);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "创建成功", data = new { id = org.Id } });
    }

    /// <summary>
    /// 更新组织
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateOrg(long id, [FromBody] OrgCreateDto dto)
    {
        var org = await _db.Orgs.FindAsync(id);
        if (org == null) return NotFound(new { code = 404, message = "组织不存在" });

        org.OrgName = dto.OrgName;
        org.ParentId = dto.ParentId;
        org.OrgType = dto.OrgType ?? org.OrgType;
        org.SortOrder = dto.SortOrder;
        org.IsEnabled = dto.IsEnabled ?? org.IsEnabled;
        org.Remark = dto.Remark;
        org.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "更新成功" });
    }

    /// <summary>
    /// 删除组织
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteOrg(long id)
    {
        var org = await _db.Orgs.FindAsync(id);
        if (org == null) return NotFound(new { code = 404, message = "组织不存在" });

        if (await _db.Orgs.AnyAsync(o => o.ParentId == id))
            return BadRequest(new { code = 400, message = "请先删除子组织" });

        if (await _db.SysUsers.AnyAsync(u => u.OrgId == id))
            return BadRequest(new { code = 400, message = "该组织下还有用户" });

        _db.Orgs.Remove(org);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "删除成功" });
    }

    private List<object> BuildTree(List<SysOrg> orgs, long parentId)
    {
        return orgs.Where(o => o.ParentId == parentId)
            .Select(o => new
            {
                o.Id, o.OrgCode, o.OrgName, o.ParentId, o.OrgType, o.SortOrder, o.IsEnabled, o.Remark,
                Children = BuildTree(orgs, o.Id)
            })
            .Cast<object>()
            .ToList();
    }
}

public class OrgCreateDto
{
    public string OrgCode { get; set; } = string.Empty;
    public string OrgName { get; set; } = string.Empty;
    public long ParentId { get; set; }
    public string? OrgType { get; set; }
    public int SortOrder { get; set; }
    public bool? IsEnabled { get; set; }
    public string? Remark { get; set; }
}


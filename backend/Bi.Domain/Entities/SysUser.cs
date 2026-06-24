namespace Bi.Domain.Entities;

/// <summary>
/// 系统用户实体 - 对应sys_user表
/// </summary>
public class SysUser : BaseEntity
{
    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密码哈希
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// 真实姓名
    /// </summary>
    public string? RealName { get; set; }

    /// <summary>
    /// 邮箱
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 手机号
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// 头像URL
    /// </summary>
    public string? Avatar { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 最后登录时间
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// 所属组织ID
    /// </summary>
    public long? OrgId { get; set; }

    /// <summary>
    /// 关联组织
    /// </summary>
    public virtual SysOrg? Org { get; set; }

    /// <summary>
    /// 用户角色关联
    /// </summary>
    public virtual ICollection<SysUserRole> UserRoles { get; set; } = new List<SysUserRole>();
}


namespace Bi.Domain.Entities;

/// <summary>
/// 系统角色表 - 对应sys_role表
/// </summary>
public class SysRole : BaseEntity
{
    /// <summary>
    /// 角色编码（唯一标识）
    /// </summary>
    public string RoleCode { get; set; } = string.Empty;

    /// <summary>
    /// 角色名称
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// 角色描述
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 排序顺序
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 角色菜单关联
    /// </summary>
    public virtual ICollection<SysRoleMenu> RoleMenus { get; set; } = new List<SysRoleMenu>();

    /// <summary>
    /// 用户角色关联
    /// </summary>
    public virtual ICollection<SysUserRole> UserRoles { get; set; } = new List<SysUserRole>();
}

/// <summary>
/// 系统组织架构表 - 对应sys_org表
/// </summary>
public class SysOrg : BaseEntity
{
    /// <summary>
    /// 组织编码
    /// </summary>
    public string OrgCode { get; set; } = string.Empty;

    /// <summary>
    /// 组织名称
    /// </summary>
    public string OrgName { get; set; } = string.Empty;

    /// <summary>
    /// 父组织ID（0表示顶级组织）
    /// </summary>
    public long ParentId { get; set; }

    /// <summary>
    /// 组织类型：company(公司)/dept(部门)/group(小组)
    /// </summary>
    public string OrgType { get; set; } = "dept";

    /// <summary>
    /// 排序顺序
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 子组织列表
    /// </summary>
    public List<SysOrg> Children { get; set; } = new();
}

/// <summary>
/// 角色菜单关联表 - 对应sys_role_menu表
/// </summary>
public class SysRoleMenu
{
    /// <summary>
    /// 主键ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 角色ID
    /// </summary>
    public long RoleId { get; set; }

    /// <summary>
    /// 菜单ID
    /// </summary>
    public long MenuId { get; set; }

    /// <summary>
    /// 关联角色
    /// </summary>
    public virtual SysRole? Role { get; set; }

    /// <summary>
    /// 关联菜单
    /// </summary>
    public virtual SysMenu? Menu { get; set; }
}

/// <summary>
/// 用户角色关联表 - 对应sys_user_role表
/// </summary>
public class SysUserRole
{
    /// <summary>
    /// 主键ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 角色ID
    /// </summary>
    public long RoleId { get; set; }

    /// <summary>
    /// 关联用户
    /// </summary>
    public virtual SysUser? User { get; set; }

    /// <summary>
    /// 关联角色
    /// </summary>
    public virtual SysRole? Role { get; set; }
}


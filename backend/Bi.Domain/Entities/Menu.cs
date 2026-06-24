namespace Bi.Domain.Entities;

/// <summary>
/// 系统菜单表
/// 树形结构，用于展示平台导航
/// </summary>
public class SysMenu : BaseEntity
{
    /// <summary>
    /// 菜单名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 父菜单ID（0表示顶级菜单）
    /// </summary>
    public long ParentId { get; set; }
    
    /// <summary>
    /// 菜单类型：folder(目录)/link(链接)/publish(发布对象)
    /// </summary>
    public string MenuType { get; set; } = "folder";
    
    /// <summary>
    /// 图标名称
    /// </summary>
    public string? Icon { get; set; }
    
    /// <summary>
    /// 链接地址（当MenuType=link时）
    /// </summary>
    public string? LinkUrl { get; set; }
    
    /// <summary>
    /// 关联发布ID（当MenuType=publish时）
    /// </summary>
    public long? PublishId { get; set; }
    
    /// <summary>
    /// 排序顺序
    /// </summary>
    public int SortOrder { get; set; }
    
    /// <summary>
    /// 是否可见
    /// </summary>
    public bool IsVisible { get; set; } = true;
    
    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
    
    /// <summary>
    /// 子菜单列表
    /// </summary>
    public List<SysMenu> Children { get; set; } = new();
    
    /// <summary>
    /// 关联的发布对象
    /// </summary>
    public BiPublish? Publish { get; set; }
}

/// <summary>
/// 发布记录表
/// 将报表/面板/图表发布为可访问的对象
/// </summary>
public class BiPublish : BaseEntity
{
    /// <summary>
    /// 发布标题
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// 对象类型：report/panel/chart
    /// </summary>
    public string ObjectType { get; set; } = "report";
    
    /// <summary>
    /// 对象ID
    /// </summary>
    public long ObjectId { get; set; }
    
    /// <summary>
    /// 访问范围：public(公开)/private(私有)/role(角色)
    /// </summary>
    public string AccessScope { get; set; } = "private";
    
    /// <summary>
    /// 访问Token（用于分享链接）
    /// </summary>
    public string? AccessToken { get; set; }
    
    /// <summary>
    /// 访问密码（可选）
    /// </summary>
    public string? AccessPassword { get; set; }
    
    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime? ExpireAt { get; set; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// 访问次数统计
    /// </summary>
    public int ViewCount { get; set; }
    
    /// <summary>
    /// 最后访问时间
    /// </summary>
    public DateTime? LastViewedAt { get; set; }
    
    /// <summary>
    /// 发布人ID
    /// </summary>
    public long PublishedBy { get; set; }
    
    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
    
    /// <summary>
    /// 允许访问的角色ID列表（JSON数组，当AccessScope=role时）
    /// </summary>
    public string? AllowedRoles { get; set; }
}


namespace Bi.Domain.Entities;

/// <summary>
/// 分析面板实体 - 对应bi_panel表
/// </summary>
public class Panel : BaseEntity
{
    /// <summary>
    /// 面板名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 面板类型：pc_dashboard/big_screen/mobile
    /// </summary>
    public string PanelType { get; set; } = "pc_dashboard";

    /// <summary>
    /// 面板级配置JSON（主题、全局筛选等）
    /// </summary>
    public string ConfigJson { get; set; } = "{}";

    /// <summary>
    /// 备注说明
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 面板子项
    /// </summary>
    public virtual ICollection<PanelItem> Items { get; set; } = new List<PanelItem>();
}

/// <summary>
/// 面板子项实体 - 对应bi_panel_item表
/// </summary>
public class PanelItem : BaseEntity
{
    /// <summary>
    /// 所属面板ID
    /// </summary>
    public long PanelId { get; set; }

    /// <summary>
    /// 关联图表ID
    /// </summary>
    public long? ChartId { get; set; }

    /// <summary>
    /// PC端布局信息JSON {x, y, w, h}
    /// </summary>
    public string LayoutJson { get; set; } = "{}";

    /// <summary>
    /// 大屏端布局信息JSON {x, y, w, h}（可选，为空时使用LayoutJson）
    /// </summary>
    public string? ScreenLayoutJson { get; set; }

    /// <summary>
    /// 移动端布局信息JSON {x, y, w, h}（可选，为空时使用LayoutJson）
    /// </summary>
    public string? MobileLayoutJson { get; set; }

    /// <summary>
    /// 排序顺序
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 关联面板
    /// </summary>
    public virtual Panel? Panel { get; set; }

    /// <summary>
    /// 关联图表
    /// </summary>
    public virtual Chart? Chart { get; set; }
}


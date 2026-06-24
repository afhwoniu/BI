namespace Bi.Domain.Entities;

/// <summary>
/// 报表/报告主表
/// 支持多页报告结构，可包含图表、面板、文字、图片等元素
/// </summary>
public class BiReport : BaseEntity
{
    /// <summary>
    /// 报表名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 报表类型：report(报告)/dashboard(仪表板)
    /// </summary>
    public string ReportType { get; set; } = "report";
    
    /// <summary>
    /// 封面图URL
    /// </summary>
    public string? CoverImage { get; set; }
    
    /// <summary>
    /// 全局配置JSON（主题、页面尺寸等）
    /// </summary>
    public string ConfigJson { get; set; } = "{}";
    
    /// <summary>
    /// 备注说明
    /// </summary>
    public string? Remark { get; set; }
    
    /// <summary>
    /// 是否已发布
    /// </summary>
    public bool IsPublished { get; set; }
    
    /// <summary>
    /// 发布时间
    /// </summary>
    public DateTime? PublishedAt { get; set; }
    
    /// <summary>
    /// 创建人ID
    /// </summary>
    public long CreatedBy { get; set; }
    
    /// <summary>
    /// 报表页列表
    /// </summary>
    public List<BiReportPage> Pages { get; set; } = new();
}

/// <summary>
/// 报表页面
/// 每个报表可包含多个页面，类似PPT结构
/// </summary>
public class BiReportPage : BaseEntity
{
    /// <summary>
    /// 所属报表ID
    /// </summary>
    public long ReportId { get; set; }
    
    /// <summary>
    /// 页面标题
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// 页面排序
    /// </summary>
    public int SortOrder { get; set; }
    
    /// <summary>
    /// 页面配置JSON（背景、布局等）
    /// </summary>
    public string ConfigJson { get; set; } = "{}";
    
    /// <summary>
    /// 所属报表
    /// </summary>
    public BiReport Report { get; set; } = null!;
    
    /// <summary>
    /// 页面元素列表
    /// </summary>
    public List<BiReportItem> Items { get; set; } = new();
}

/// <summary>
/// 报表元素/组件
/// 支持多种类型：图表、面板、文本、图片、表格等
/// </summary>
public class BiReportItem : BaseEntity
{
    /// <summary>
    /// 所属页面ID
    /// </summary>
    public long PageId { get; set; }
    
    /// <summary>
    /// 元素类型：chart/panel/text/image/table/shape
    /// </summary>
    public string ItemType { get; set; } = "chart";
    
    /// <summary>
    /// 关联图表ID（当ItemType=chart时）
    /// </summary>
    public long? ChartId { get; set; }
    
    /// <summary>
    /// 关联面板ID（当ItemType=panel时）
    /// </summary>
    public long? PanelId { get; set; }
    
    /// <summary>
    /// 文本内容（当ItemType=text时）
    /// </summary>
    public string? TextContent { get; set; }
    
    /// <summary>
    /// 图片URL（当ItemType=image时）
    /// </summary>
    public string? ImageUrl { get; set; }
    
    /// <summary>
    /// 布局配置JSON（x, y, width, height, zIndex等）
    /// </summary>
    public string LayoutJson { get; set; } = "{}";
    
    /// <summary>
    /// 样式配置JSON（字体、颜色、边框等）
    /// </summary>
    public string StyleJson { get; set; } = "{}";
    
    /// <summary>
    /// 排序顺序
    /// </summary>
    public int SortOrder { get; set; }
    
    /// <summary>
    /// 所属页面
    /// </summary>
    public BiReportPage Page { get; set; } = null!;
    
    /// <summary>
    /// 关联图表
    /// </summary>
    public Chart? Chart { get; set; }

    /// <summary>
    /// 关联面板
    /// </summary>
    public Panel? Panel { get; set; }
}


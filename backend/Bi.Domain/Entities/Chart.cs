namespace Bi.Domain.Entities;

/// <summary>
/// 图表实体 - 对应bi_chart表
/// </summary>
public class Chart : BaseEntity
{
    /// <summary>
    /// 图表名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 关联数据集ID
    /// </summary>
    public long DatasetId { get; set; }

    /// <summary>
    /// 图表类型：bar/line/pie/table/kpi等
    /// </summary>
    public string ChartType { get; set; } = "bar";

    /// <summary>
    /// 图表配置JSON
    /// </summary>
    public string ConfigJson { get; set; } = "{}";

    /// <summary>
    /// 备注说明
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 关联数据集
    /// </summary>
    public virtual Dataset? Dataset { get; set; }

    /// <summary>
    /// 关联面板项
    /// </summary>
    public virtual ICollection<PanelItem> PanelItems { get; set; } = new List<PanelItem>();
}


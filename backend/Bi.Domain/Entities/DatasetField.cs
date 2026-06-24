namespace Bi.Domain.Entities;

/// <summary>
/// 数据集字段实体 - 对应bi_dataset_field表
/// </summary>
public class DatasetField : BaseEntity
{
    /// <summary>
    /// 所属数据集ID
    /// </summary>
    public long DatasetId { get; set; }

    /// <summary>
    /// 字段名
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// 字段别名（显示名）
    /// </summary>
    public string? FieldAlias { get; set; }

    /// <summary>
    /// 数据类型
    /// </summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// 字段角色：dim=维度, measure=度量
    /// </summary>
    public string Role { get; set; } = "dim";

    /// <summary>
    /// 聚合类型：sum/count/avg/max/min/none
    /// </summary>
    public string AggType { get; set; } = "none";

    /// <summary>
    /// 排序顺序
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 关联数据集
    /// </summary>
    public virtual Dataset? Dataset { get; set; }
}


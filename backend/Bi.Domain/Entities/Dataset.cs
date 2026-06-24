namespace Bi.Domain.Entities;

/// <summary>
/// 数据集实体 - 对应bi_dataset表
/// </summary>
public class Dataset : BaseEntity
{
    /// <summary>
    /// 数据集名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 所属数据源ID
    /// </summary>
    public long DatasourceId { get; set; }

    /// <summary>
    /// SQL语句
    /// </summary>
    public string SqlText { get; set; } = string.Empty;

    /// <summary>
    /// 参数定义JSON
    /// </summary>
    public string? ParamSchema { get; set; }

    /// <summary>
    /// 备注说明
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 关联数据源
    /// </summary>
    public virtual Datasource? Datasource { get; set; }

    /// <summary>
    /// 关联字段列表
    /// </summary>
    public virtual ICollection<DatasetField> Fields { get; set; } = new List<DatasetField>();

    /// <summary>
    /// 关联图表列表
    /// </summary>
    public virtual ICollection<Chart> Charts { get; set; } = new List<Chart>();
}


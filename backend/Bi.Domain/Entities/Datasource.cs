namespace Bi.Domain.Entities;

/// <summary>
/// 数据源实体 - 对应bi_datasource表
/// </summary>
public class Datasource : BaseEntity
{
    /// <summary>
    /// 数据源名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 数据源类型：sqlserver/postgres/mysql
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 连接字符串（加密存储）
    /// </summary>
    public string ConnString { get; set; } = string.Empty;

    /// <summary>
    /// 备注说明
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 关联的数据集
    /// </summary>
    public virtual ICollection<Dataset> Datasets { get; set; } = new List<Dataset>();
}


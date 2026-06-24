using System.ComponentModel.DataAnnotations;

namespace Bi.Domain.Entities;

/// <summary>
/// 指标定义
/// </summary>
public class KpiDefinition
{
    /// <summary>
    /// 指标ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 指标编码（唯一标识）
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 指标名称
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 所属分类ID
    /// </summary>
    public long CategoryId { get; set; }

    /// <summary>
    /// 指标口径/定义说明
    /// </summary>
    [MaxLength(2000)]
    public string? Definition { get; set; }

    /// <summary>
    /// 计算公式说明
    /// </summary>
    [MaxLength(1000)]
    public string? Formula { get; set; }

    /// <summary>
    /// SQL模板（可包含参数占位符）
    /// </summary>
    public string? SqlTemplate { get; set; }

    /// <summary>
    /// 数据源ID（指标关联的数据源）
    /// </summary>
    public long? DatasourceId { get; set; }

    /// <summary>
    /// 单位
    /// </summary>
    [MaxLength(50)]
    public string? Unit { get; set; }

    /// <summary>
    /// 数据类型（number/percent/currency等）
    /// </summary>
    [MaxLength(50)]
    public string DataType { get; set; } = "number";

    /// <summary>
    /// 向量嵌入JSON（用于语义检索，存储float数组的JSON格式）
    /// 注意：如果数据库支持pgvector，可以改用vector类型以获得更好的性能
    /// </summary>
    public string? EmbeddingJson { get; set; }

    /// <summary>
    /// 向量生成时间
    /// </summary>
    public DateTime? EmbeddingUpdatedAt { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 导航属性
    public virtual KpiCategory? Category { get; set; }
    public virtual Datasource? Datasource { get; set; }
}


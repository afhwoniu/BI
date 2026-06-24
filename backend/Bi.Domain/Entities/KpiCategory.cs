using System.ComponentModel.DataAnnotations;

namespace Bi.Domain.Entities;

/// <summary>
/// 指标分类
/// </summary>
public class KpiCategory
{
    /// <summary>
    /// 分类ID
    /// </summary>
    public long Id { get; set; }
    
    /// <summary>
    /// 分类名称
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 父分类ID（为空表示顶级分类）
    /// </summary>
    public long? ParentId { get; set; }
    
    /// <summary>
    /// 分类描述
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }
    
    /// <summary>
    /// 排序号
    /// </summary>
    public int SortOrder { get; set; } = 0;
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // 导航属性
    public virtual KpiCategory? Parent { get; set; }
    public virtual ICollection<KpiCategory> Children { get; set; } = new List<KpiCategory>();
    public virtual ICollection<KpiDefinition> Kpis { get; set; } = new List<KpiDefinition>();
}


using System.ComponentModel.DataAnnotations;

namespace Bi.Domain.Entities;

/// <summary>
/// AI查询收藏
/// </summary>
public class AiFavorite
{
    /// <summary>
    /// 收藏ID
    /// </summary>
    public long Id { get; set; }
    
    /// <summary>
    /// 用户ID
    /// </summary>
    public long UserId { get; set; }
    
    /// <summary>
    /// 收藏标题
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// 原始问题
    /// </summary>
    public string Question { get; set; } = string.Empty;
    
    /// <summary>
    /// SQL语句
    /// </summary>
    public string? Sql { get; set; }
    
    /// <summary>
    /// 图表类型
    /// </summary>
    [MaxLength(50)]
    public string? ChartType { get; set; }
    
    /// <summary>
    /// 图表配置JSON
    /// </summary>
    public string? ChartConfig { get; set; }
    
    /// <summary>
    /// 关联的数据源ID
    /// </summary>
    public long? DatasourceId { get; set; }
    
    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(500)]
    public string? Remark { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // 导航属性
    public virtual SysUser? User { get; set; }
    public virtual Datasource? Datasource { get; set; }
}


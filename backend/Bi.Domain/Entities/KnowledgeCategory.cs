using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Bi.Domain.Entities;

/// <summary>
/// 知识库分类
/// </summary>
public class KnowledgeCategory
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
    /// 父分类ID
    /// </summary>
    public long? ParentId { get; set; }

    /// <summary>
    /// 排序号
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 父分类导航属性
    /// </summary>
    [JsonIgnore]
    public virtual KnowledgeCategory? Parent { get; set; }

    /// <summary>
    /// 子分类
    /// </summary>
    public virtual ICollection<KnowledgeCategory> Children { get; set; } = new List<KnowledgeCategory>();

    /// <summary>
    /// 该分类下的文档
    /// </summary>
    [JsonIgnore]
    public virtual ICollection<KnowledgeDocument> Documents { get; set; } = new List<KnowledgeDocument>();
}


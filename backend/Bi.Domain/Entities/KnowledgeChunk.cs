using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Pgvector;

namespace Bi.Domain.Entities;

/// <summary>
/// 知识分块（存储向量）
/// </summary>
public class KnowledgeChunk
{
    /// <summary>
    /// 分块ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 所属文档ID
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// 块在文档中的序号
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// 块内容（纯文本）
    /// </summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 内容长度
    /// </summary>
    public int? ContentLength { get; set; }

    /// <summary>
    /// 向量嵌入（pgvector类型，1024维 for BGE-M3）
    /// </summary>
    public Vector? Embedding { get; set; }

    /// <summary>
    /// 页码（PDF文档）
    /// </summary>
    public int? PageNumber { get; set; }

    /// <summary>
    /// 章节标题
    /// </summary>
    [MaxLength(500)]
    public string? SectionTitle { get; set; }

    /// <summary>
    /// 扩展元信息（JSON格式）
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 所属文档导航属性
    /// </summary>
    [JsonIgnore]
    public virtual KnowledgeDocument Document { get; set; } = null!;
}


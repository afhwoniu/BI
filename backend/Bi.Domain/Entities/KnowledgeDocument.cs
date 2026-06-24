using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Bi.Domain.Entities;

/// <summary>
/// 知识库文档
/// </summary>
public class KnowledgeDocument
{
    /// <summary>
    /// 文档ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 分类ID
    /// </summary>
    public long? CategoryId { get; set; }

    /// <summary>
    /// 文档标题
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 原始文件名
    /// </summary>
    [MaxLength(255)]
    public string? FileName { get; set; }

    /// <summary>
    /// 文件类型 (pdf/docx/xlsx/txt/md)
    /// </summary>
    [MaxLength(50)]
    public string? FileType { get; set; }

    /// <summary>
    /// 文件大小(bytes)
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// 文件存储路径
    /// </summary>
    [MaxLength(500)]
    public string? FilePath { get; set; }

    /// <summary>
    /// 内容Hash（用于去重）
    /// </summary>
    [MaxLength(64)]
    public string? ContentHash { get; set; }

    /// <summary>
    /// 处理状态：pending-待处理, processing-处理中, completed-已完成, failed-失败
    /// </summary>
    [MaxLength(50)]
    public string Status { get; set; } = "pending";

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 分块数量
    /// </summary>
    public int ChunkCount { get; set; }

    /// <summary>
    /// 已处理分块数（用于显示进度）
    /// </summary>
    public int ProcessedChunkCount { get; set; }

    /// <summary>
    /// 处理进度百分比（0-100）
    /// </summary>
    public int ProcessProgress { get; set; }

    /// <summary>
    /// 原始文档内容（临时存储，处理完后可清空）
    /// </summary>
    public string? RawContent { get; set; }

    /// <summary>
    /// 关联数据源ID
    /// </summary>
    public long? DatasourceId { get; set; }

    /// <summary>
    /// 扩展元信息（JSON格式）
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// 创建人ID
    /// </summary>
    public long? CreatedBy { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 所属分类导航属性（序列化时忽略避免循环）
    /// </summary>
    public virtual KnowledgeCategory? Category { get; set; }

    /// <summary>
    /// 文档分块集合（序列化时忽略避免循环）
    /// </summary>
    [JsonIgnore]
    public virtual ICollection<KnowledgeChunk> Chunks { get; set; } = new List<KnowledgeChunk>();
}


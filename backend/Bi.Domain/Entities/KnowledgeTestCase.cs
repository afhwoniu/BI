using System.ComponentModel.DataAnnotations;

namespace Bi.Domain.Entities;

/// <summary>
/// 知识库测试用例
/// 用于评估检索效果（命中率、MRR等）
/// </summary>
public class KnowledgeTestCase
{
    /// <summary>
    /// 测试用例ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 用例名称
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 测试查询文本
    /// </summary>
    [Required]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// 期望命中的文档ID列表（JSON数组）
    /// </summary>
    public string? ExpectedDocumentIds { get; set; }

    /// <summary>
    /// 期望命中的分块ID列表（JSON数组，更精确的评估）
    /// </summary>
    public string? ExpectedChunkIds { get; set; }

    /// <summary>
    /// 期望命中的关键词列表（JSON数组，用于模糊匹配评估）
    /// </summary>
    public string? ExpectedKeywords { get; set; }

    /// <summary>
    /// 限定分类ID（可选）
    /// </summary>
    public long? CategoryId { get; set; }

    /// <summary>
    /// 备注说明
    /// </summary>
    public string? Remark { get; set; }

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
}


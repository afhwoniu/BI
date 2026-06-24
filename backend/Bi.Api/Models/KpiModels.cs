namespace Bi.Api.Models;

/// <summary>
/// 指标分类DTO（树形结构）
/// </summary>
public class KpiCategoryDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long? ParentId { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public List<KpiCategoryDto> Children { get; set; } = new();
}

/// <summary>
/// 创建/更新分类请求
/// </summary>
public class KpiCategoryCreateDto
{
    public string Name { get; set; } = string.Empty;
    public long? ParentId { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// 指标定义DTO
/// </summary>
public class KpiDefinitionDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? Definition { get; set; }
    public string? Formula { get; set; }
    public string? Unit { get; set; }
    public string DataType { get; set; } = "number";
    public bool HasEmbedding { get; set; }
    public DateTime? EmbeddingUpdatedAt { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 创建/更新指标请求
/// </summary>
public class KpiDefinitionCreateDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long CategoryId { get; set; }
    public string? Definition { get; set; }
    public string? Formula { get; set; }
    public string? SqlTemplate { get; set; }
    public long? DatasourceId { get; set; }
    public string? Unit { get; set; }
    public string? DataType { get; set; }
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// 指标检索请求
/// </summary>
public class KpiSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; } = 5;
    public double MinScore { get; set; } = 0.5;
}

/// <summary>
/// 指标检索结果DTO
/// </summary>
public class KpiSearchResultDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Definition { get; set; }
    public string? Formula { get; set; }
    public string? SqlTemplate { get; set; }
    public string? Unit { get; set; }
    public double Score { get; set; }
}

/// <summary>
/// 分页结果
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}


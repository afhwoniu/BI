namespace Bi.Api.Models;

/// <summary>
/// 数据集列表DTO
/// </summary>
public class DatasetDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long DatasourceId { get; set; }
    public string? DatasourceName { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 数据集详情DTO
/// </summary>
public class DatasetDetailDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long DatasourceId { get; set; }
    public string SqlText { get; set; } = string.Empty;
    public string? ParamSchema { get; set; }
    public string? Remark { get; set; }
    public List<DatasetFieldDto> Fields { get; set; } = new();
}

/// <summary>
/// 数据集字段DTO
/// </summary>
public class DatasetFieldDto
{
    public long Id { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? FieldAlias { get; set; }
    public string DataType { get; set; } = string.Empty;
    public string Role { get; set; } = "dim";
    public string AggType { get; set; } = "none";
    public int SortOrder { get; set; }
}

/// <summary>
/// 创建数据集请求
/// </summary>
public class DatasetCreateDto
{
    public string Name { get; set; } = string.Empty;
    public long DatasourceId { get; set; }
    public string SqlText { get; set; } = string.Empty;
    public string? ParamSchema { get; set; }
    public string? Remark { get; set; }
    public List<DatasetFieldDto>? Fields { get; set; }
}

/// <summary>
/// 更新数据集请求
/// </summary>
public class DatasetUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public long DatasourceId { get; set; }
    public string SqlText { get; set; } = string.Empty;
    public string? ParamSchema { get; set; }
    public string? Remark { get; set; }
    public List<DatasetFieldDto>? Fields { get; set; }
}

/// <summary>
/// 预览请求
/// </summary>
public class DatasetPreviewDto
{
    public long DatasourceId { get; set; }
    public string SqlText { get; set; } = string.Empty;
    public int MaxRows { get; set; } = 100;
}

/// <summary>
/// 预览结果
/// </summary>
public class DatasetPreviewResult
{
    public List<ColumnInfo> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public int TotalRows { get; set; }
}

/// <summary>
/// 列信息
/// </summary>
public class ColumnInfo
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
}


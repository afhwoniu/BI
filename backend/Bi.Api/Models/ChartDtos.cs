namespace Bi.Api.Models;

/// <summary>
/// 图表列表DTO
/// </summary>
public class ChartDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long DatasetId { get; set; }
    public string? DatasetName { get; set; }
    public string ChartType { get; set; } = "bar";
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 图表详情DTO
/// </summary>
public class ChartDetailDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long DatasetId { get; set; }
    public string? DatasetName { get; set; }
    public string ChartType { get; set; } = "bar";
    public string ConfigJson { get; set; } = "{}";
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>
    /// 数据集字段列表
    /// </summary>
    public List<DatasetFieldDto> Fields { get; set; } = new();
}

/// <summary>
/// 创建图表请求
/// </summary>
public class ChartCreateDto
{
    public string Name { get; set; } = string.Empty;
    public long DatasetId { get; set; }
    public string ChartType { get; set; } = "bar";
    public string ConfigJson { get; set; } = "{}";
    public string? Remark { get; set; }
}

/// <summary>
/// 更新图表请求
/// </summary>
public class ChartUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public long DatasetId { get; set; }
    public string ChartType { get; set; } = "bar";
    public string ConfigJson { get; set; } = "{}";
    public string? Remark { get; set; }
}

/// <summary>
/// 图表查询请求
/// </summary>
public class ChartQueryDto
{
    /// <summary>
    /// 额外筛选条件（可选）
    /// </summary>
    public List<FilterCondition>? Filters { get; set; }

    /// <summary>
    /// 是否跳过缓存（强制刷新）
    /// </summary>
    public bool? SkipCache { get; set; }
}

/// <summary>
/// 筛选条件
/// </summary>
public class FilterCondition
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "="; // =, !=, >, <, >=, <=, like, in
    public object? Value { get; set; }
}

/// <summary>
/// 图表查询结果
/// </summary>
public class ChartQueryResult
{
    public List<string> Categories { get; set; } = new();
    public List<SeriesData> Series { get; set; } = new();
    public List<Dictionary<string, object?>> RawData { get; set; } = new();

    /// <summary>
    /// 同比数据（去年同期）
    /// </summary>
    public List<SeriesData>? YoySeries { get; set; }

    /// <summary>
    /// 环比数据（上期）
    /// </summary>
    public List<SeriesData>? MomSeries { get; set; }
}

/// <summary>
/// 系列数据
/// </summary>
public class SeriesData
{
    public string Name { get; set; } = string.Empty;
    public List<object?> Data { get; set; } = new();
}


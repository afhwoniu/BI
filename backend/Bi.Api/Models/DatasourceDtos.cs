namespace Bi.Api.Models;

/// <summary>
/// 数据源列表DTO
/// </summary>
public class DatasourceDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 数据源详情DTO（含连接字符串）
/// </summary>
public class DatasourceDetailDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ConnString { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 新增数据源请求
/// </summary>
public class DatasourceCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "postgres";
    public string ConnString { get; set; } = string.Empty;
    public string? Remark { get; set; }
}

/// <summary>
/// 更新数据源请求
/// </summary>
public class DatasourceUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "postgres";
    public string? ConnString { get; set; } // 为空则不更新
    public bool IsEnabled { get; set; } = true;
    public string? Remark { get; set; }
}

/// <summary>
/// 测试连接请求
/// </summary>
public class DatasourceTestDto
{
    public string Type { get; set; } = "postgres";
    public string ConnString { get; set; } = string.Empty;
}


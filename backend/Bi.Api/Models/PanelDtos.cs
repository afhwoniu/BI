namespace Bi.Api.Models;

/// <summary>
/// 面板列表DTO
/// </summary>
public class PanelDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PanelType { get; set; } = "pc_dashboard";
    public string? Remark { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 面板详情DTO
/// </summary>
public class PanelDetailDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PanelType { get; set; } = "pc_dashboard";
    public string ConfigJson { get; set; } = "{}";
    public string? Remark { get; set; }
    public List<PanelItemDto> Items { get; set; } = new();
}

/// <summary>
/// 面板子项DTO
/// </summary>
public class PanelItemDto
{
    public long Id { get; set; }
    public long? ChartId { get; set; }
    public string? ChartName { get; set; }
    public string? ChartType { get; set; }
    public string LayoutJson { get; set; } = "{}";
    public int SortOrder { get; set; }
}

/// <summary>
/// 创建面板请求
/// </summary>
public class PanelCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string PanelType { get; set; } = "pc_dashboard";
    public string ConfigJson { get; set; } = "{}";
    public string? Remark { get; set; }
}

/// <summary>
/// 更新面板请求
/// </summary>
public class PanelUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string PanelType { get; set; } = "pc_dashboard";
    public string ConfigJson { get; set; } = "{}";
    public string? Remark { get; set; }
    public List<PanelItemUpdateDto>? Items { get; set; }
}

/// <summary>
/// 面板子项更新DTO
/// </summary>
public class PanelItemUpdateDto
{
    public long? ChartId { get; set; }
    public string LayoutJson { get; set; } = "{}";
    public int SortOrder { get; set; }
}


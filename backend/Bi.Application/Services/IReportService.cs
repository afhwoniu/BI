using Bi.Domain.Entities;

namespace Bi.Application.Services;

/// <summary>
/// 报表服务接口
/// </summary>
public interface IReportService
{
    /// <summary>
    /// 获取报表列表
    /// </summary>
    Task<List<ReportListDto>> GetListAsync();

    /// <summary>
    /// 获取报表详情（包含所有页面和元素）
    /// </summary>
    Task<ReportDetailDto?> GetByIdAsync(long id);

    /// <summary>
    /// 创建报表
    /// </summary>
    Task<BiReport> CreateAsync(ReportCreateDto dto);

    /// <summary>
    /// 更新报表
    /// </summary>
    Task<bool> UpdateAsync(long id, ReportUpdateDto dto);

    /// <summary>
    /// 删除报表
    /// </summary>
    Task<bool> DeleteAsync(long id);

    /// <summary>
    /// 保存报表页面
    /// </summary>
    Task<BiReportPage> SavePageAsync(long reportId, ReportPageDto dto);

    /// <summary>
    /// 删除页面
    /// </summary>
    Task<bool> DeletePageAsync(long pageId);

    /// <summary>
    /// 保存页面元素
    /// </summary>
    Task<BiReportItem> SaveItemAsync(long pageId, ReportItemDto dto);

    /// <summary>
    /// 删除元素
    /// </summary>
    Task<bool> DeleteItemAsync(long itemId);

    /// <summary>
    /// 获取报表渲染数据（包含图表数据）
    /// </summary>
    Task<ReportRenderDto?> GetRenderDataAsync(long id);
}

#region DTOs

public class ReportListDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ReportType { get; set; } = "report";
    public string? CoverImage { get; set; }
    public bool IsPublished { get; set; }
    public int PageCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ReportDetailDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ReportType { get; set; } = "report";
    public string? CoverImage { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public string? Remark { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<ReportPageDetailDto> Pages { get; set; } = new();
}

public class ReportPageDetailDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public List<ReportItemDetailDto> Items { get; set; } = new();
}

public class ReportItemDetailDto
{
    public long Id { get; set; }
    public string ItemType { get; set; } = "chart";
    public long? ChartId { get; set; }
    public string? ChartName { get; set; }
    public long? PanelId { get; set; }
    public string? PanelName { get; set; }
    public string? TextContent { get; set; }
    public string? ImageUrl { get; set; }
    public string LayoutJson { get; set; } = "{}";
    public string StyleJson { get; set; } = "{}";
    public int SortOrder { get; set; }
}

public class ReportCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string ReportType { get; set; } = "report";
    public string? CoverImage { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public string? Remark { get; set; }
}

public class ReportUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string ReportType { get; set; } = "report";
    public string? CoverImage { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public string? Remark { get; set; }
}

public class ReportPageDto
{
    public long? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string ConfigJson { get; set; } = "{}";
}

public class ReportItemDto
{
    public long? Id { get; set; }
    public string ItemType { get; set; } = "chart";
    public long? ChartId { get; set; }
    public long? PanelId { get; set; }
    public string? TextContent { get; set; }
    public string? ImageUrl { get; set; }
    public string LayoutJson { get; set; } = "{}";
    public string StyleJson { get; set; } = "{}";
    public int SortOrder { get; set; }
}

public class ReportRenderDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = "{}";
    public List<ReportPageRenderDto> Pages { get; set; } = new();
}

public class ReportPageRenderDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = "{}";
    public List<ReportItemRenderDto> Items { get; set; } = new();
}

public class ReportItemRenderDto
{
    public long Id { get; set; }
    public string ItemType { get; set; } = "chart";
    public string LayoutJson { get; set; } = "{}";
    public string StyleJson { get; set; } = "{}";
    // 图表渲染数据
    public object? ChartData { get; set; }
    public string? ChartType { get; set; }
    public string? ChartConfig { get; set; }
    // 文本/图片
    public string? TextContent { get; set; }
    public string? ImageUrl { get; set; }
}

#endregion


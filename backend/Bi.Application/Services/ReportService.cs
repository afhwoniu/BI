using Bi.Domain.Entities;
using Bi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bi.Application.Services;

/// <summary>
/// 报表服务实现
/// </summary>
public class ReportService : IReportService
{
    private readonly BiDbContext _db;
    private readonly ILogger<ReportService> _logger;

    public ReportService(BiDbContext db, ILogger<ReportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<ReportListDto>> GetListAsync()
    {
        return await _db.Reports
            .OrderByDescending(r => r.UpdatedAt)
            .Select(r => new ReportListDto
            {
                Id = r.Id,
                Name = r.Name,
                ReportType = r.ReportType,
                CoverImage = r.CoverImage,
                IsPublished = r.IsPublished,
                PageCount = r.Pages.Count,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            })
            .ToListAsync();
    }

    public async Task<ReportDetailDto?> GetByIdAsync(long id)
    {
        var report = await _db.Reports
            .Include(r => r.Pages.OrderBy(p => p.SortOrder))
                .ThenInclude(p => p.Items.OrderBy(i => i.SortOrder))
                    .ThenInclude(i => i.Chart)
            .Include(r => r.Pages)
                .ThenInclude(p => p.Items)
                    .ThenInclude(i => i.Panel)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null) return null;

        return new ReportDetailDto
        {
            Id = report.Id,
            Name = report.Name,
            ReportType = report.ReportType,
            CoverImage = report.CoverImage,
            ConfigJson = report.ConfigJson,
            Remark = report.Remark,
            IsPublished = report.IsPublished,
            PublishedAt = report.PublishedAt,
            CreatedAt = report.CreatedAt,
            UpdatedAt = report.UpdatedAt,
            Pages = report.Pages.OrderBy(p => p.SortOrder).Select(p => new ReportPageDetailDto
            {
                Id = p.Id,
                Title = p.Title,
                SortOrder = p.SortOrder,
                ConfigJson = p.ConfigJson,
                Items = p.Items.OrderBy(i => i.SortOrder).Select(i => new ReportItemDetailDto
                {
                    Id = i.Id,
                    ItemType = i.ItemType,
                    ChartId = i.ChartId,
                    ChartName = i.Chart?.Name,
                    PanelId = i.PanelId,
                    PanelName = i.Panel?.Name,
                    TextContent = i.TextContent,
                    ImageUrl = i.ImageUrl,
                    LayoutJson = i.LayoutJson,
                    StyleJson = i.StyleJson,
                    SortOrder = i.SortOrder
                }).ToList()
            }).ToList()
        };
    }

    public async Task<BiReport> CreateAsync(ReportCreateDto dto)
    {
        var report = new BiReport
        {
            Name = dto.Name,
            ReportType = dto.ReportType,
            CoverImage = dto.CoverImage,
            ConfigJson = dto.ConfigJson,
            Remark = dto.Remark,
            CreatedBy = 1, // TODO: 从当前用户获取
            CreatedAt = DateTime.UtcNow
        };

        // 创建默认第一页
        report.Pages.Add(new BiReportPage
        {
            Title = "第1页",
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow
        });

        _db.Reports.Add(report);
        await _db.SaveChangesAsync();
        
        _logger.LogInformation("创建报表: {Name}, ID: {Id}", report.Name, report.Id);
        return report;
    }

    public async Task<bool> UpdateAsync(long id, ReportUpdateDto dto)
    {
        var report = await _db.Reports.FindAsync(id);
        if (report == null) return false;

        report.Name = dto.Name;
        report.ReportType = dto.ReportType;
        report.CoverImage = dto.CoverImage;
        report.ConfigJson = dto.ConfigJson;
        report.Remark = dto.Remark;
        report.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var report = await _db.Reports.FindAsync(id);
        if (report == null) return false;

        _db.Reports.Remove(report);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<BiReportPage> SavePageAsync(long reportId, ReportPageDto dto)
    {
        BiReportPage page;
        
        if (dto.Id.HasValue && dto.Id > 0)
        {
            page = await _db.ReportPages.FindAsync(dto.Id.Value) 
                ?? throw new Exception("页面不存在");
            page.Title = dto.Title;
            page.SortOrder = dto.SortOrder;
            page.ConfigJson = dto.ConfigJson;
            page.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            page = new BiReportPage
            {
                ReportId = reportId,
                Title = dto.Title,
                SortOrder = dto.SortOrder,
                ConfigJson = dto.ConfigJson,
                CreatedAt = DateTime.UtcNow
            };
            _db.ReportPages.Add(page);
        }

        await _db.SaveChangesAsync();
        return page;
    }

    public async Task<bool> DeletePageAsync(long pageId)
    {
        var page = await _db.ReportPages.FindAsync(pageId);
        if (page == null) return false;

        _db.ReportPages.Remove(page);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<BiReportItem> SaveItemAsync(long pageId, ReportItemDto dto)
    {
        BiReportItem item;

        if (dto.Id.HasValue && dto.Id > 0)
        {
            item = await _db.ReportItems.FindAsync(dto.Id.Value)
                ?? throw new Exception("元素不存在");
            item.ItemType = dto.ItemType;
            item.ChartId = dto.ChartId;
            item.PanelId = dto.PanelId;
            item.TextContent = dto.TextContent;
            item.ImageUrl = dto.ImageUrl;
            item.LayoutJson = dto.LayoutJson;
            item.StyleJson = dto.StyleJson;
            item.SortOrder = dto.SortOrder;
            item.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            item = new BiReportItem
            {
                PageId = pageId,
                ItemType = dto.ItemType,
                ChartId = dto.ChartId,
                PanelId = dto.PanelId,
                TextContent = dto.TextContent,
                ImageUrl = dto.ImageUrl,
                LayoutJson = dto.LayoutJson,
                StyleJson = dto.StyleJson,
                SortOrder = dto.SortOrder,
                CreatedAt = DateTime.UtcNow
            };
            _db.ReportItems.Add(item);
        }

        await _db.SaveChangesAsync();
        return item;
    }

    public async Task<bool> DeleteItemAsync(long itemId)
    {
        var item = await _db.ReportItems.FindAsync(itemId);
        if (item == null) return false;

        _db.ReportItems.Remove(item);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<ReportRenderDto?> GetRenderDataAsync(long id)
    {
        var report = await _db.Reports
            .Include(r => r.Pages.OrderBy(p => p.SortOrder))
                .ThenInclude(p => p.Items.OrderBy(i => i.SortOrder))
                    .ThenInclude(i => i.Chart)
                        .ThenInclude(c => c!.Dataset)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null) return null;

        var result = new ReportRenderDto
        {
            Id = report.Id,
            Name = report.Name,
            ConfigJson = report.ConfigJson,
            Pages = new List<ReportPageRenderDto>()
        };

        foreach (var page in report.Pages.OrderBy(p => p.SortOrder))
        {
            var pageRender = new ReportPageRenderDto
            {
                Id = page.Id,
                Title = page.Title,
                ConfigJson = page.ConfigJson,
                Items = new List<ReportItemRenderDto>()
            };

            foreach (var item in page.Items.OrderBy(i => i.SortOrder))
            {
                var itemRender = new ReportItemRenderDto
                {
                    Id = item.Id,
                    ItemType = item.ItemType,
                    LayoutJson = item.LayoutJson,
                    StyleJson = item.StyleJson,
                    TextContent = item.TextContent,
                    ImageUrl = item.ImageUrl
                };

                // 如果是图表类型，返回图表配置（数据由前端单独请求）
                if (item.ItemType == "chart" && item.ChartId.HasValue && item.Chart != null)
                {
                    itemRender.ChartType = item.Chart.ChartType;
                    itemRender.ChartConfig = item.Chart.ConfigJson;
                    // ChartData 留空，前端通过 /chart/{id}/query 接口获取
                }

                pageRender.Items.Add(itemRender);
            }

            result.Pages.Add(pageRender);
        }

        return result;
    }
}

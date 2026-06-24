using System.Text.Json;
using Bi.Domain.Entities;
using Bi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bi.Application.Services;

/// <summary>
/// 备份还原服务接口
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// 导出所有配置
    /// </summary>
    Task<BackupData> ExportAsync();

    /// <summary>
    /// 导入配置
    /// </summary>
    Task<ImportResult> ImportAsync(BackupData data, bool overwrite = false);
}

/// <summary>
/// 备份数据结构
/// </summary>
public class BackupData
{
    public string Version { get; set; } = "1.0";
    public DateTime ExportedAt { get; set; } = DateTime.Now;
    public List<DatasourceBackup> Datasources { get; set; } = new();
    public List<DatasetBackup> Datasets { get; set; } = new();
    public List<ChartBackup> Charts { get; set; } = new();
    public List<PanelBackup> Panels { get; set; } = new();
    public List<MenuBackup> Menus { get; set; } = new();
    public List<KpiCategoryBackup> KpiCategories { get; set; } = new();
    public List<KpiDefinitionBackup> KpiDefinitions { get; set; } = new();
}

/// <summary>
/// 导入结果
/// </summary>
public class ImportResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int DatasourcesImported { get; set; }
    public int DatasetsImported { get; set; }
    public int ChartsImported { get; set; }
    public int PanelsImported { get; set; }
    public int MenusImported { get; set; }
    public int KpiCategoriesImported { get; set; }
    public int KpiDefinitionsImported { get; set; }
}

// 备份实体类
public class DatasourceBackup
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string ConnString { get; set; } = "";
    public string? Remark { get; set; }
    public bool IsEnabled { get; set; }
}

public class DatasetBackup
{
    public string Name { get; set; } = "";
    public string DatasourceName { get; set; } = "";
    public string SqlText { get; set; } = "";
    public string? ParamsJson { get; set; }
    public string? Remark { get; set; }
    public List<DatasetFieldBackup> Fields { get; set; } = new();
}

public class DatasetFieldBackup
{
    public string FieldName { get; set; } = "";
    public string? FieldAlias { get; set; }
    public string DataType { get; set; } = "";
    public string Role { get; set; } = "";
    public string? AggType { get; set; }
    public int SortOrder { get; set; }
}

public class ChartBackup
{
    public string Name { get; set; } = "";
    public string DatasetName { get; set; } = "";
    public string ChartType { get; set; } = "";
    public string ConfigJson { get; set; } = "{}";
    public string? Remark { get; set; }
}

public class PanelBackup
{
    public string Name { get; set; } = "";
    public string PanelType { get; set; } = "";
    public string? ConfigJson { get; set; }
    public string? Remark { get; set; }
    public List<PanelItemBackup> Items { get; set; } = new();
}

public class PanelItemBackup
{
    public string? ChartName { get; set; }
    public string LayoutJson { get; set; } = "{}";
    public int SortOrder { get; set; }
}

public class MenuBackup
{
    public string Name { get; set; } = "";
    public string? ParentName { get; set; }
    public string? Icon { get; set; }
    public string? Path { get; set; }
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; }
}

public class KpiCategoryBackup
{
    public string Name { get; set; } = "";
    public string? ParentName { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

public class KpiDefinitionBackup
{
    public string Name { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string? Description { get; set; }
    public string? Formula { get; set; }
    public string? SqlTemplate { get; set; }
    public string? Unit { get; set; }
    public string? Tags { get; set; }
}

/// <summary>
/// 备份还原服务实现
/// </summary>
public class BackupService : IBackupService
{
    private readonly BiDbContext _db;
    private readonly ILogger<BackupService> _logger;

    public BackupService(BiDbContext db, ILogger<BackupService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<BackupData> ExportAsync()
    {
        var data = new BackupData { ExportedAt = DateTime.Now };

        // 导出数据源
        var datasources = await _db.Datasources.ToListAsync();
        data.Datasources = datasources.Select(d => new DatasourceBackup
        {
            Name = d.Name,
            Type = d.Type,
            ConnString = d.ConnString,
            Remark = d.Remark,
            IsEnabled = d.IsEnabled
        }).ToList();

        // 导出数据集
        var datasets = await _db.Datasets.Include(d => d.Datasource).Include(d => d.Fields).ToListAsync();
        data.Datasets = datasets.Select(d => new DatasetBackup
        {
            Name = d.Name,
            DatasourceName = d.Datasource?.Name ?? "",
            SqlText = d.SqlText,
            ParamsJson = d.ParamSchema,
            Remark = d.Remark,
            Fields = d.Fields.Select(f => new DatasetFieldBackup
            {
                FieldName = f.FieldName,
                FieldAlias = f.FieldAlias,
                DataType = f.DataType,
                Role = f.Role,
                AggType = f.AggType,
                SortOrder = f.SortOrder
            }).ToList()
        }).ToList();

        // 导出图表
        var charts = await _db.Charts.Include(c => c.Dataset).ToListAsync();
        data.Charts = charts.Select(c => new ChartBackup
        {
            Name = c.Name,
            DatasetName = c.Dataset?.Name ?? "",
            ChartType = c.ChartType,
            ConfigJson = c.ConfigJson,
            Remark = c.Remark
        }).ToList();

        // 导出面板
        var panels = await _db.Panels.Include(p => p.Items).ThenInclude(i => i.Chart).ToListAsync();
        data.Panels = panels.Select(p => new PanelBackup
        {
            Name = p.Name,
            PanelType = p.PanelType,
            ConfigJson = p.ConfigJson,
            Remark = p.Remark,
            Items = p.Items.Select(i => new PanelItemBackup
            {
                ChartName = i.Chart?.Name,
                LayoutJson = i.LayoutJson,
                SortOrder = i.SortOrder
            }).ToList()
        }).ToList();

        // 导出菜单（SysMenu没有Parent导航属性，通过ParentId查找）
        var menus = await _db.Menus.ToListAsync();
        var menuDict = menus.ToDictionary(m => m.Id, m => m.Name);
        data.Menus = menus.Select(m => new MenuBackup
        {
            Name = m.Name,
            ParentName = m.ParentId > 0 && menuDict.ContainsKey(m.ParentId) ? menuDict[m.ParentId] : null,
            Icon = m.Icon,
            Path = m.LinkUrl,
            SortOrder = m.SortOrder,
            IsEnabled = m.IsVisible
        }).ToList();

        // 导出指标分类
        var categories = await _db.KpiCategories.Include(c => c.Parent).ToListAsync();
        data.KpiCategories = categories.Select(c => new KpiCategoryBackup
        {
            Name = c.Name,
            ParentName = c.Parent?.Name,
            Description = c.Description,
            SortOrder = c.SortOrder
        }).ToList();

        // 导出指标定义
        var kpis = await _db.KpiDefinitions.Include(k => k.Category).ToListAsync();
        data.KpiDefinitions = kpis.Select(k => new KpiDefinitionBackup
        {
            Name = k.Name,
            CategoryName = k.Category?.Name ?? "",
            Description = k.Definition,
            Formula = k.Formula,
            SqlTemplate = k.SqlTemplate,
            Unit = k.Unit,
            Tags = null // KpiDefinition没有Tags字段
        }).ToList();

        _logger.LogInformation("导出完成: {Datasources}数据源, {Datasets}数据集, {Charts}图表, {Panels}面板",
            data.Datasources.Count, data.Datasets.Count, data.Charts.Count, data.Panels.Count);

        return data;
    }

    public async Task<ImportResult> ImportAsync(BackupData data, bool overwrite = false)
    {
        var result = new ImportResult { Success = true };

        try
        {
            // 导入数据源
            foreach (var ds in data.Datasources)
            {
                var existing = await _db.Datasources.FirstOrDefaultAsync(d => d.Name == ds.Name);
                if (existing != null && !overwrite) continue;

                if (existing != null)
                {
                    existing.Type = ds.Type;
                    existing.ConnString = ds.ConnString;
                    existing.Remark = ds.Remark;
                    existing.IsEnabled = ds.IsEnabled;
                }
                else
                {
                    _db.Datasources.Add(new Datasource
                    {
                        Name = ds.Name,
                        Type = ds.Type,
                        ConnString = ds.ConnString,
                        Remark = ds.Remark,
                        IsEnabled = ds.IsEnabled
                    });
                }
                result.DatasourcesImported++;
            }
            await _db.SaveChangesAsync();

            // 导入数据集（需要先有数据源）
            foreach (var ds in data.Datasets)
            {
                var datasource = await _db.Datasources.FirstOrDefaultAsync(d => d.Name == ds.DatasourceName);
                if (datasource == null) continue;

                var existing = await _db.Datasets.Include(d => d.Fields).FirstOrDefaultAsync(d => d.Name == ds.Name);
                if (existing != null && !overwrite) continue;

                if (existing != null)
                {
                    existing.DatasourceId = datasource.Id;
                    existing.SqlText = ds.SqlText;
                    existing.ParamSchema = ds.ParamsJson;
                    existing.Remark = ds.Remark;
                    _db.DatasetFields.RemoveRange(existing.Fields);
                    foreach (var f in ds.Fields)
                    {
                        existing.Fields.Add(new DatasetField
                        {
                            DatasetId = existing.Id,
                            FieldName = f.FieldName,
                            FieldAlias = f.FieldAlias,
                            DataType = f.DataType,
                            Role = f.Role,
                            AggType = f.AggType,
                            SortOrder = f.SortOrder
                        });
                    }
                }
                else
                {
                    var dataset = new Dataset
                    {
                        Name = ds.Name,
                        DatasourceId = datasource.Id,
                        SqlText = ds.SqlText,
                        ParamSchema = ds.ParamsJson,
                        Remark = ds.Remark
                    };
                    _db.Datasets.Add(dataset);
                    await _db.SaveChangesAsync();

                    foreach (var f in ds.Fields)
                    {
                        _db.DatasetFields.Add(new DatasetField
                        {
                            DatasetId = dataset.Id,
                            FieldName = f.FieldName,
                            FieldAlias = f.FieldAlias,
                            DataType = f.DataType,
                            Role = f.Role,
                            AggType = f.AggType,
                            SortOrder = f.SortOrder
                        });
                    }
                }
                result.DatasetsImported++;
            }
            await _db.SaveChangesAsync();

            result.Message = "导入成功";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"导入失败: {ex.Message}";
            _logger.LogError(ex, "导入配置失败");
        }

        return result;
    }
}


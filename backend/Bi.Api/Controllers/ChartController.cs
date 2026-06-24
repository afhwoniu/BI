using Bi.Api.Models;
using Bi.Application.Services;
using Bi.Domain.Entities;
using Bi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Text.Json;
using Npgsql;
using Microsoft.Data.SqlClient;
using MySqlConnector;

namespace Bi.Api.Controllers;

/// <summary>
/// 图表管理控制器
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ChartController : ControllerBase
{
    private readonly BiDbContext _db;
    private readonly IChartCacheService _cacheService;

    public ChartController(BiDbContext db, IChartCacheService cacheService)
    {
        _db = db;
        _cacheService = cacheService;
    }

    /// <summary>
    /// 获取图表列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ChartDto>>>> GetList()
    {
        var list = await _db.Charts
            .Include(c => c.Dataset)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ChartDto
            {
                Id = c.Id,
                Name = c.Name,
                DatasetId = c.DatasetId,
                DatasetName = c.Dataset != null ? c.Dataset.Name : null,
                ChartType = c.ChartType,
                Remark = c.Remark,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync();
        return Ok(ApiResponse<List<ChartDto>>.Success(list));
    }

    /// <summary>
    /// 获取图表详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ChartDetailDto>>> GetById(long id)
    {
        var chart = await _db.Charts
            .Include(c => c.Dataset)
                .ThenInclude(d => d!.Fields)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (chart == null)
            return Ok(ApiResponse<ChartDetailDto>.Fail("图表不存在", 404));

        return Ok(ApiResponse<ChartDetailDto>.Success(new ChartDetailDto
        {
            Id = chart.Id,
            Name = chart.Name,
            DatasetId = chart.DatasetId,
            DatasetName = chart.Dataset?.Name,
            ChartType = chart.ChartType,
            ConfigJson = chart.ConfigJson,
            Remark = chart.Remark,
            CreatedAt = chart.CreatedAt,
            Fields = chart.Dataset?.Fields.OrderBy(f => f.SortOrder).Select(f => new DatasetFieldDto
            {
                Id = f.Id,
                FieldName = f.FieldName,
                FieldAlias = f.FieldAlias,
                DataType = f.DataType,
                Role = f.Role,
                AggType = f.AggType,
                SortOrder = f.SortOrder
            }).ToList() ?? new()
        }));
    }

    /// <summary>
    /// 新增图表
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<long>>> Create([FromBody] ChartCreateDto dto)
    {
        var entity = new Chart
        {
            Name = dto.Name,
            DatasetId = dto.DatasetId,
            ChartType = dto.ChartType,
            ConfigJson = dto.ConfigJson,
            Remark = dto.Remark,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Charts.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<long>.Success(entity.Id));
    }

    /// <summary>
    /// 更新图表
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> Update(long id, [FromBody] ChartUpdateDto dto)
    {
        var entity = await _db.Charts.FindAsync(id);
        if (entity == null)
            return Ok(ApiResponse<bool>.Fail("图表不存在", 404));

        entity.Name = dto.Name;
        entity.DatasetId = dto.DatasetId;
        entity.ChartType = dto.ChartType;
        entity.ConfigJson = dto.ConfigJson;
        entity.Remark = dto.Remark;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<bool>.Success(true));
    }

    /// <summary>
    /// 删除图表
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(long id)
    {
        var entity = await _db.Charts.FindAsync(id);
        if (entity == null)
            return Ok(ApiResponse<bool>.Fail("图表不存在", 404));

        // 检查是否被面板使用
        var usedInPanel = await _db.PanelItems.AnyAsync(p => p.ChartId == id);
        if (usedInPanel)
            return Ok(ApiResponse<bool>.Fail("该图表已被面板使用，无法删除"));

        _db.Charts.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<bool>.Success(true));
    }

    /// <summary>
    /// 图表查询 - 执行聚合SQL并返回数据
    /// </summary>
    [HttpPost("{id}/query")]
    public async Task<ActionResult<ApiResponse<ChartQueryResult>>> Query(long id, [FromBody] ChartQueryDto? dto)
    {
        var chart = await _db.Charts
            .Include(c => c.Dataset)
                .ThenInclude(d => d!.Datasource)
            .Include(c => c.Dataset)
                .ThenInclude(d => d!.Fields)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (chart?.Dataset?.Datasource == null)
            return Ok(ApiResponse<ChartQueryResult>.Fail("图表或关联数据无效"));

        // 检查是否跳过缓存
        var skipCache = dto?.SkipCache ?? false;
        var filterJson = dto?.Filters != null ? JsonSerializer.Serialize(dto.Filters) : null;

        try
        {
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var config = JsonSerializer.Deserialize<ChartConfig>(chart.ConfigJson, jsonOptions) ?? new ChartConfig();
            var ds = chart.Dataset;
            var datasource = ds.Datasource;

            // 尝试从缓存获取（命中后也要组装 categories/series，否则前端无法渲染柱状图/折线图）
            if (!skipCache)
            {
                var cachedData = await _cacheService.GetAsync(id, filterJson);
                if (cachedData != null)
                {
                    var cachedResult = new ChartQueryResult
                    {
                        RawData = cachedData.Select(d => d.ToDictionary(k => k.Key, k => (object?)k.Value)).ToList()
                    };
                    FillSeriesFromRawData(cachedResult, config);

                    if (config.Compare != null && !string.IsNullOrEmpty(config.Compare.DateField))
                    {
                        await CalculateCompareData(cachedResult, config, ds.SqlText, datasource, dto?.Filters);
                    }

                    return Ok(ApiResponse<ChartQueryResult>.Success(cachedResult, "来自缓存"));
                }
            }

            // 构建聚合SQL
            var sql = BuildAggregationSql(ds.SqlText, config, ds.Fields.ToList(), datasource.Type, dto?.Filters);

            using var conn = CreateConnection(datasource.Type, datasource.ConnString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 60;

            using var reader = await cmd.ExecuteReaderAsync();
            var result = new ChartQueryResult();
            var rawData = new List<Dictionary<string, object?>>();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rawData.Add(row);
            }

            result.RawData = rawData;
            FillSeriesFromRawData(result, config);

            // 同环比计算
            if (config.Compare != null && !string.IsNullOrEmpty(config.Compare.DateField))
            {
                await CalculateCompareData(result, config, ds.SqlText, datasource, dto?.Filters);
            }

            // 缓存结果（转换为可序列化格式）
            var cacheData = result.RawData.Select(d => d.ToDictionary(k => k.Key, k => k.Value ?? new object())).ToList();
            await _cacheService.SetAsync(id, filterJson, cacheData);

            return Ok(ApiResponse<ChartQueryResult>.Success(result));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<ChartQueryResult>.Fail($"查询失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 基于原始数据和图表配置，填充 categories 与 series
    /// </summary>
    private static void FillSeriesFromRawData(ChartQueryResult result, ChartConfig config)
    {
        result.Categories = new List<string>();
        result.Series = new List<SeriesData>();

        if (result.RawData.Count == 0) return;

        var dimensions = config.Dimensions;
        if (dimensions == null || dimensions.Count == 0)
        {
            // AI保存的图表无配置时，自动推断：第一列作为维度，其余数值列作为度量
            var columns = result.RawData[0].Keys.ToList();
            if (columns.Count == 0) return;

            // 第一列作为X轴类别
            var dimField = columns[0];
            result.Categories = result.RawData
                .Select(r => r.TryGetValue(dimField, out var v) ? v?.ToString() ?? "" : "")
                .ToList();

            // 剩余列中数值类型的自动作为系列
            foreach (var col in columns.Skip(1))
            {
                var sampleVal = result.RawData.FirstOrDefault(r => r.TryGetValue(col, out var v) && v != null)?[col];
                if (sampleVal != null && IsNumericType(sampleVal))
                {
                    result.Series.Add(new SeriesData
                    {
                        Name = col,
                        Data = result.RawData.Select(r => r.TryGetValue(col, out var v) ? v : null).ToList()
                    });
                }
            }
            return;
        }

        var dimField2 = dimensions[0].Field;
        result.Categories = result.RawData
            .Select(r => r.TryGetValue(dimField2, out var v) ? v?.ToString() ?? "" : "")
            .ToList();

        if (config.Measures == null) return;

        foreach (var m in config.Measures)
        {
            result.Series.Add(new SeriesData
            {
                Name = m.Alias ?? m.Field,
                Data = result.RawData.Select(r => r.TryGetValue(m.Field, out var v) ? v : null).ToList()
            });
        }
    }

    private static bool IsNumericType(object val)
    {
        return val is int or long or float or double or decimal or short or byte or uint or ulong or ushort or sbyte;
    }

    /// <summary>
    /// 构建聚合SQL - 使用新的维度/度量配置格式
    /// </summary>
    private static string BuildAggregationSql(string baseSql, ChartConfig config, List<DatasetField> fields, string dbType, List<FilterCondition>? filters)
    {
        var dims = config.Dimensions ?? new List<DimensionConfig>();
        var measures = config.Measures ?? new List<MeasureConfig>();

        if (dims.Count == 0 && measures.Count == 0)
        {
            // 无配置则返回原SQL限制行数
            return $"SELECT * FROM ({baseSql.Trim().TrimEnd(';')}) AS t LIMIT 1000";
        }

        var selectParts = new List<string>();
        var groupParts = new List<string>();

        // 维度字段
        foreach (var dim in dims)
        {
            selectParts.Add(dim.Field);
            groupParts.Add(dim.Field);
        }

        // 度量字段 - 使用配置中的聚合类型
        foreach (var m in measures)
        {
            var agg = m.AggType ?? "sum";
            if (string.IsNullOrEmpty(agg) || agg == "none") agg = "sum";
            selectParts.Add($"{agg.ToUpper()}({m.Field}) AS {m.Field}");
        }

        var sql = $"SELECT {string.Join(", ", selectParts)} FROM ({baseSql.Trim().TrimEnd(';')}) AS t";

        // 收集所有筛选条件
        var whereParts = new List<string>();

        // 1. 预筛选条件（图表内置）
        if (config.PreFilters != null)
        {
            foreach (var pf in config.PreFilters.Where(p => p.Enabled))
            {
                var condition = BuildFilterCondition(pf.Field, pf.Operator, pf.Value);
                if (!string.IsNullOrEmpty(condition))
                    whereParts.Add(condition);
            }
        }

        // 2. 外部传入筛选条件
        if (filters != null)
        {
            foreach (var f in filters)
            {
                var condition = BuildFilterCondition(f.Field, f.Operator, f.Value);
                if (!string.IsNullOrEmpty(condition))
                    whereParts.Add(condition);
            }
        }

        if (whereParts.Count > 0)
        {
            sql += $" WHERE {string.Join(" AND ", whereParts)}";
        }

        if (groupParts.Count > 0)
        {
            sql += $" GROUP BY {string.Join(", ", groupParts)}";
        }

        return sql;
    }

    /// <summary>
    /// 构建单个筛选条件SQL片段
    /// </summary>
    private static string BuildFilterCondition(string field, string op, object? value)
    {
        if (string.IsNullOrEmpty(field)) return "";

        var sqlOp = op.ToLower() switch
        {
            "=" => "=",
            "!=" or "<>" => "<>",
            ">" => ">",
            "<" => "<",
            ">=" => ">=",
            "<=" => "<=",
            "like" => "LIKE",
            "in" => "IN",
            "between" => "BETWEEN",
            "notnull" => "IS NOT NULL",
            "isnull" => "IS NULL",
            _ => "="
        };

        // 特殊处理无值操作符
        if (sqlOp == "IS NOT NULL" || sqlOp == "IS NULL")
        {
            return $"{field} {sqlOp}";
        }

        // IN操作符处理
        if (sqlOp == "IN")
        {
            if (value is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                var vals = new List<string>();
                foreach (var item in je.EnumerateArray())
                {
                    vals.Add(item.ValueKind == System.Text.Json.JsonValueKind.String
                        ? $"'{item.GetString()?.Replace("'", "''")}'"
                        : item.ToString());
                }
                return $"{field} IN ({string.Join(", ", vals)})";
            }
            else if (value is IEnumerable<object> list)
            {
                var vals = list.Select(v => v is string s ? $"'{s.Replace("'", "''")}'" : v.ToString()).ToList();
                return $"{field} IN ({string.Join(", ", vals)})";
            }
        }

        // BETWEEN操作符处理
        if (sqlOp == "BETWEEN")
        {
            if (value is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                var arr = je.EnumerateArray().ToList();
                if (arr.Count >= 2)
                {
                    var v1 = arr[0].ValueKind == System.Text.Json.JsonValueKind.String
                        ? $"'{arr[0].GetString()?.Replace("'", "''")}'"
                        : arr[0].ToString();
                    var v2 = arr[1].ValueKind == System.Text.Json.JsonValueKind.String
                        ? $"'{arr[1].GetString()?.Replace("'", "''")}'"
                        : arr[1].ToString();
                    return $"{field} BETWEEN {v1} AND {v2}";
                }
            }
        }

        // 普通值处理
        string sqlVal;
        if (value is System.Text.Json.JsonElement jsonEl)
        {
            sqlVal = jsonEl.ValueKind == System.Text.Json.JsonValueKind.String
                ? $"'{jsonEl.GetString()?.Replace("'", "''")}'"
                : jsonEl.ToString();
        }
        else if (value is string str)
        {
            sqlVal = sqlOp == "LIKE" ? $"'%{str.Replace("'", "''")}%'" : $"'{str.Replace("'", "''")}'";
        }
        else
        {
            sqlVal = value?.ToString() ?? "NULL";
        }

        return $"{field} {sqlOp} {sqlVal}";
    }

    private static DbConnection CreateConnection(string type, string connString)
    {
        return type.ToLower() switch
        {
            "postgres" or "postgresql" => new NpgsqlConnection(connString),
            "sqlserver" or "mssql" => new SqlConnection(connString),
            "mysql" => new MySqlConnection(EnsureMySqlConnStringParams(connString)),
            "doris" => new MySqlConnection(EnsureMySqlConnStringParams(connString)),
            _ => throw new ArgumentException($"不支持的数据源类型: {type}")
        };
    }

    /// <summary>
    /// 确保MySQL连接字符串包含必要的参数，避免COM_RESET_CONNECTION错误
    /// </summary>
    private static string EnsureMySqlConnStringParams(string connString)
    {
        // 如果已包含ConnectionReset参数，直接返回
        if (connString.Contains("ConnectionReset", StringComparison.OrdinalIgnoreCase))
            return connString;

        // 添加必要的参数
        var additionalParams = "ConnectionReset=false;AllowUserVariables=true;CharSet=utf8mb4";
        return connString.TrimEnd(';') + ";" + additionalParams;
    }

    /// <summary>
    /// 计算同环比数据
    /// </summary>
    private async Task CalculateCompareData(ChartQueryResult result, ChartConfig config, string baseSql, Datasource datasource, List<FilterCondition>? filters)
    {
        if (config.Compare == null || string.IsNullOrEmpty(config.Compare.DateField))
            return;

        var dateField = config.Compare.DateField;
        var granularity = config.Compare.DateGranularity ?? "month";

        // 从当前数据中获取日期范围
        if (result.RawData.Count == 0) return;

        // 尝试获取日期范围
        var dates = result.RawData
            .Select(r => r.TryGetValue(dateField, out var v) ? v : null)
            .Where(v => v != null)
            .Select(v => {
                if (v is DateTime dt) return dt;
                if (DateTime.TryParse(v?.ToString(), out var parsed)) return parsed;
                return (DateTime?)null;
            })
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .ToList();

        if (dates.Count == 0) return;

        var minDate = dates.Min();
        var maxDate = dates.Max();

        // 计算同比区间（去年同期）
        if (config.Compare.YoyEnabled)
        {
            var yoyFilters = new List<FilterCondition>(filters ?? new());
            var yoyMinDate = minDate.AddYears(-1);
            var yoyMaxDate = maxDate.AddYears(-1);
            yoyFilters.Add(new FilterCondition { Field = dateField, Operator = ">=", Value = yoyMinDate.ToString("yyyy-MM-dd") });
            yoyFilters.Add(new FilterCondition { Field = dateField, Operator = "<=", Value = yoyMaxDate.ToString("yyyy-MM-dd") });

            var yoyData = await ExecuteCompareQuery(baseSql, config, datasource, yoyFilters);
            if (yoyData.Count > 0 && config.Measures != null)
            {
                result.YoySeries = new List<SeriesData>();
                foreach (var m in config.Measures)
                {
                    result.YoySeries.Add(new SeriesData
                    {
                        Name = $"{m.Alias ?? m.Field}(同比)",
                        Data = yoyData.Select(r => r.TryGetValue(m.Field, out var v) ? v : null).ToList()
                    });
                }
            }
        }

        // 计算环比区间（上期）
        if (config.Compare.MomEnabled)
        {
            var momFilters = new List<FilterCondition>(filters ?? new());
            var span = maxDate - minDate;
            var momMinDate = minDate - span - TimeSpan.FromDays(1);
            var momMaxDate = minDate.AddDays(-1);
            momFilters.Add(new FilterCondition { Field = dateField, Operator = ">=", Value = momMinDate.ToString("yyyy-MM-dd") });
            momFilters.Add(new FilterCondition { Field = dateField, Operator = "<=", Value = momMaxDate.ToString("yyyy-MM-dd") });

            var momData = await ExecuteCompareQuery(baseSql, config, datasource, momFilters);
            if (momData.Count > 0 && config.Measures != null)
            {
                result.MomSeries = new List<SeriesData>();
                foreach (var m in config.Measures)
                {
                    result.MomSeries.Add(new SeriesData
                    {
                        Name = $"{m.Alias ?? m.Field}(环比)",
                        Data = momData.Select(r => r.TryGetValue(m.Field, out var v) ? v : null).ToList()
                    });
                }
            }
        }
    }

    /// <summary>
    /// 执行对比期查询
    /// </summary>
    private async Task<List<Dictionary<string, object?>>> ExecuteCompareQuery(string baseSql, ChartConfig config, Datasource datasource, List<FilterCondition> filters)
    {
        var sql = BuildAggregationSql(baseSql, config, new List<DatasetField>(), datasource.Type, filters);

        using var conn = CreateConnection(datasource.Type, datasource.ConnString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 60;

        using var reader = await cmd.ExecuteReaderAsync();
        var data = new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            data.Add(row);
        }

        return data;
    }
}

/// <summary>
/// 图表配置 - 新格式支持对象化的维度/度量配置
/// </summary>
public class ChartConfig
{
    /// <summary>
    /// 维度配置列表
    /// </summary>
    public List<DimensionConfig>? Dimensions { get; set; }

    /// <summary>
    /// 度量配置列表
    /// </summary>
    public List<MeasureConfig>? Measures { get; set; }

    /// <summary>
    /// 预筛选条件（图表内置筛选）
    /// </summary>
    public List<PreFilterConfig>? PreFilters { get; set; }

    /// <summary>
    /// 同环比配置
    /// </summary>
    public CompareConfig? Compare { get; set; }

    public string? Title { get; set; }
    public bool? ShowLegend { get; set; }
    public JsonElement? EchartsOption { get; set; }
}

/// <summary>
/// 预筛选配置
/// </summary>
public class PreFilterConfig
{
    /// <summary>
    /// 字段名
    /// </summary>
    public string Field { get; set; } = "";

    /// <summary>
    /// 操作符：=, !=, >, <, >=, <=, like, in, between
    /// </summary>
    public string Operator { get; set; } = "=";

    /// <summary>
    /// 筛选值（单值或数组）
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// 同环比配置
/// </summary>
public class CompareConfig
{
    /// <summary>
    /// 是否启用同比
    /// </summary>
    public bool YoyEnabled { get; set; }

    /// <summary>
    /// 是否启用环比
    /// </summary>
    public bool MomEnabled { get; set; }

    /// <summary>
    /// 日期字段名
    /// </summary>
    public string? DateField { get; set; }

    /// <summary>
    /// 日期粒度：day, week, month, quarter, year
    /// </summary>
    public string? DateGranularity { get; set; }
}

/// <summary>
/// 维度配置
/// </summary>
public class DimensionConfig
{
    /// <summary>
    /// 字段名
    /// </summary>
    public string Field { get; set; } = "";

    /// <summary>
    /// 显示别名
    /// </summary>
    public string? Alias { get; set; }
}

/// <summary>
/// 度量配置
/// </summary>
public class MeasureConfig
{
    /// <summary>
    /// 字段名
    /// </summary>
    public string Field { get; set; } = "";

    /// <summary>
    /// 显示别名
    /// </summary>
    public string? Alias { get; set; }

    /// <summary>
    /// 聚合类型：sum, count, avg, max, min
    /// </summary>
    public string AggType { get; set; } = "sum";
}

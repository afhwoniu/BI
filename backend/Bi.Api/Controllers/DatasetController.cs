using Bi.Api.Models;
using Bi.Domain.Entities;
using Bi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Text.RegularExpressions;
using Npgsql;
using Microsoft.Data.SqlClient;
using MySqlConnector;

namespace Bi.Api.Controllers;

/// <summary>
/// 数据集管理控制器
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public partial class DatasetController : ControllerBase
{
    private readonly BiDbContext _db;

    public DatasetController(BiDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 获取数据集列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<DatasetDto>>>> GetList()
    {
        var list = await _db.Datasets
            .Include(d => d.Datasource)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DatasetDto
            {
                Id = d.Id,
                Name = d.Name,
                DatasourceId = d.DatasourceId,
                DatasourceName = d.Datasource != null ? d.Datasource.Name : null,
                Remark = d.Remark,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            })
            .ToListAsync();
        return Ok(ApiResponse<List<DatasetDto>>.Success(list));
    }

    /// <summary>
    /// 获取数据集详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<DatasetDetailDto>>> GetById(long id)
    {
        var ds = await _db.Datasets
            .Include(d => d.Fields)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (ds == null)
            return Ok(ApiResponse<DatasetDetailDto>.Fail("数据集不存在", 404));

        return Ok(ApiResponse<DatasetDetailDto>.Success(new DatasetDetailDto
        {
            Id = ds.Id,
            Name = ds.Name,
            DatasourceId = ds.DatasourceId,
            SqlText = ds.SqlText,
            ParamSchema = ds.ParamSchema,
            Remark = ds.Remark,
            Fields = ds.Fields.OrderBy(f => f.SortOrder).Select(f => new DatasetFieldDto
            {
                Id = f.Id,
                FieldName = f.FieldName,
                FieldAlias = f.FieldAlias,
                DataType = f.DataType,
                Role = f.Role,
                AggType = f.AggType,
                SortOrder = f.SortOrder
            }).ToList()
        }));
    }

    /// <summary>
    /// 新增数据集
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<long>>> Create([FromBody] DatasetCreateDto dto)
    {
        // SQL安全校验
        if (!IsSafeSelectSql(dto.SqlText))
            return Ok(ApiResponse<long>.Fail("SQL语句仅允许SELECT查询"));

        var entity = new Dataset
        {
            Name = dto.Name,
            DatasourceId = dto.DatasourceId,
            SqlText = dto.SqlText,
            ParamSchema = dto.ParamSchema,
            Remark = dto.Remark,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Datasets.Add(entity);
        await _db.SaveChangesAsync();

        // 保存字段
        if (dto.Fields != null && dto.Fields.Count > 0)
        {
            var fields = dto.Fields.Select((f, i) => new DatasetField
            {
                DatasetId = entity.Id,
                FieldName = f.FieldName,
                FieldAlias = f.FieldAlias,
                DataType = f.DataType,
                Role = f.Role,
                AggType = f.AggType,
                SortOrder = i,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();
            _db.DatasetFields.AddRange(fields);
            await _db.SaveChangesAsync();
        }

        return Ok(ApiResponse<long>.Success(entity.Id));
    }

    /// <summary>
    /// 更新数据集
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> Update(long id, [FromBody] DatasetUpdateDto dto)
    {
        if (!IsSafeSelectSql(dto.SqlText))
            return Ok(ApiResponse<bool>.Fail("SQL语句仅允许SELECT查询"));

        var entity = await _db.Datasets.Include(d => d.Fields).FirstOrDefaultAsync(d => d.Id == id);
        if (entity == null)
            return Ok(ApiResponse<bool>.Fail("数据集不存在", 404));

        entity.Name = dto.Name;
        entity.DatasourceId = dto.DatasourceId;
        entity.SqlText = dto.SqlText;
        entity.ParamSchema = dto.ParamSchema;
        entity.Remark = dto.Remark;
        entity.UpdatedAt = DateTime.UtcNow;

        // 更新字段：先删后加
        _db.DatasetFields.RemoveRange(entity.Fields);
        if (dto.Fields != null && dto.Fields.Count > 0)
        {
            var fields = dto.Fields.Select((f, i) => new DatasetField
            {
                DatasetId = entity.Id,
                FieldName = f.FieldName,
                FieldAlias = f.FieldAlias,
                DataType = f.DataType,
                Role = f.Role,
                AggType = f.AggType,
                SortOrder = i,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();
            _db.DatasetFields.AddRange(fields);
        }

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<bool>.Success(true));
    }

    /// <summary>
    /// 删除数据集
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(long id)
    {
        var entity = await _db.Datasets.Include(d => d.Fields).FirstOrDefaultAsync(d => d.Id == id);
        if (entity == null)
            return Ok(ApiResponse<bool>.Fail("数据集不存在", 404));

        // 检查是否有关联图表
        var hasCharts = await _db.Charts.AnyAsync(c => c.DatasetId == id);
        if (hasCharts)
            return Ok(ApiResponse<bool>.Fail("该数据集下存在图表，无法删除"));

        _db.DatasetFields.RemoveRange(entity.Fields);
        _db.Datasets.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<bool>.Success(true));
    }

    /// <summary>
    /// 预览数据集
    /// </summary>
    [HttpPost("preview")]
    public async Task<ActionResult<ApiResponse<DatasetPreviewResult>>> Preview([FromBody] DatasetPreviewDto dto)
    {
        if (!IsSafeSelectSql(dto.SqlText))
            return Ok(ApiResponse<DatasetPreviewResult>.Fail("SQL语句仅允许SELECT查询"));

        var datasource = await _db.Datasources.FindAsync(dto.DatasourceId);
        if (datasource == null)
            return Ok(ApiResponse<DatasetPreviewResult>.Fail("数据源不存在"));

        try
        {
            using var conn = CreateConnection(datasource.Type, datasource.ConnString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = WrapSqlWithLimit(dto.SqlText, datasource.Type, dto.MaxRows);
            cmd.CommandTimeout = 30;

            using var reader = await cmd.ExecuteReaderAsync();
            var result = new DatasetPreviewResult();

            // 读取列信息
            for (int i = 0; i < reader.FieldCount; i++)
            {
                result.Columns.Add(new ColumnInfo
                {
                    Name = reader.GetName(i),
                    DataType = reader.GetDataTypeName(i)
                });
            }

            // 读取数据行
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[reader.GetName(i)] = value;
                }
                result.Rows.Add(row);
            }
            result.TotalRows = result.Rows.Count;

            return Ok(ApiResponse<DatasetPreviewResult>.Success(result));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<DatasetPreviewResult>.Fail($"执行失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// SQL安全校验：仅允许SELECT
    /// </summary>
    private static bool IsSafeSelectSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return false;
        var normalized = sql.Trim().ToUpperInvariant();
        // 禁止危险关键字
        var forbidden = new[] { "INSERT ", "UPDATE ", "DELETE ", "DROP ", "TRUNCATE ", "ALTER ", "CREATE ", "EXEC ", "EXECUTE ", "GRANT ", "REVOKE " };
        foreach (var kw in forbidden)
        {
            if (normalized.Contains(kw)) return false;
        }
        // 必须以SELECT开头
        return normalized.StartsWith("SELECT ");
    }

    /// <summary>
    /// 包装SQL限制返回行数
    /// </summary>
    private static string WrapSqlWithLimit(string sql, string dbType, int maxRows)
    {
        var trimmed = sql.Trim().TrimEnd(';');
        return dbType.ToLower() switch
        {
            "postgres" or "postgresql" or "mysql" or "doris" => $"{trimmed} LIMIT {maxRows}",
            "sqlserver" or "mssql" => $"SELECT TOP {maxRows} * FROM ({trimmed}) AS __subquery",
            _ => trimmed
        };
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
        if (connString.Contains("ConnectionReset", StringComparison.OrdinalIgnoreCase))
            return connString;
        return connString.TrimEnd(';') + ";ConnectionReset=false;AllowUserVariables=true;CharSet=utf8mb4";
    }
}

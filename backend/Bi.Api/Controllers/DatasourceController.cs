using Bi.Api.Models;
using Bi.Domain.Entities;
using Bi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using Npgsql;
using Microsoft.Data.SqlClient;
using MySqlConnector;

namespace Bi.Api.Controllers;

/// <summary>
/// 数据源管理控制器
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class DatasourceController : ControllerBase
{
    private readonly BiDbContext _db;

    public DatasourceController(BiDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 获取数据源列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<DatasourceDto>>>> GetList()
    {
        var list = await _db.Datasources
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DatasourceDto
            {
                Id = d.Id,
                Name = d.Name,
                Type = d.Type,
                IsEnabled = d.IsEnabled,
                Remark = d.Remark,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            })
            .ToListAsync();
        return Ok(ApiResponse<List<DatasourceDto>>.Success(list));
    }

    /// <summary>
    /// 获取数据源详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<DatasourceDetailDto>>> GetById(long id)
    {
        var ds = await _db.Datasources.FindAsync(id);
        if (ds == null)
            return Ok(ApiResponse<DatasourceDetailDto>.Fail("数据源不存在", 404));

        return Ok(ApiResponse<DatasourceDetailDto>.Success(new DatasourceDetailDto
        {
            Id = ds.Id,
            Name = ds.Name,
            Type = ds.Type,
            ConnString = ds.ConnString, // 返回连接字符串供编辑
            IsEnabled = ds.IsEnabled,
            Remark = ds.Remark,
            CreatedAt = ds.CreatedAt
        }));
    }

    /// <summary>
    /// 新增数据源
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<long>>> Create([FromBody] DatasourceCreateDto dto)
    {
        var entity = new Datasource
        {
            Name = dto.Name,
            Type = dto.Type,
            ConnString = dto.ConnString,
            Remark = dto.Remark,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Datasources.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<long>.Success(entity.Id));
    }

    /// <summary>
    /// 更新数据源
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> Update(long id, [FromBody] DatasourceUpdateDto dto)
    {
        var entity = await _db.Datasources.FindAsync(id);
        if (entity == null)
            return Ok(ApiResponse<bool>.Fail("数据源不存在", 404));

        entity.Name = dto.Name;
        entity.Type = dto.Type;
        if (!string.IsNullOrEmpty(dto.ConnString))
            entity.ConnString = dto.ConnString;
        entity.Remark = dto.Remark;
        entity.IsEnabled = dto.IsEnabled;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<bool>.Success(true));
    }

    /// <summary>
    /// 删除数据源
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(long id)
    {
        var entity = await _db.Datasources.FindAsync(id);
        if (entity == null)
            return Ok(ApiResponse<bool>.Fail("数据源不存在", 404));

        // 检查是否有关联数据集
        var hasDatasets = await _db.Datasets.AnyAsync(d => d.DatasourceId == id);
        if (hasDatasets)
            return Ok(ApiResponse<bool>.Fail("该数据源下存在数据集，无法删除"));

        _db.Datasources.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<bool>.Success(true));
    }

    /// <summary>
    /// 测试数据源连接
    /// </summary>
    [HttpPost("test")]
    public async Task<ActionResult<ApiResponse<string>>> TestConnection([FromBody] DatasourceTestDto dto)
    {
        try
        {
            using var conn = CreateConnection(dto.Type, dto.ConnString);
            await conn.OpenAsync();
            await conn.CloseAsync();
            return Ok(ApiResponse<string>.Success("连接成功"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<string>.Fail($"连接失败: {ex.Message}"));
        }
    }

    private static DbConnection CreateConnection(string type, string connString)
    {
        return type.ToLower() switch
        {
            "postgres" or "postgresql" => new NpgsqlConnection(connString),
            "sqlserver" or "mssql" => new SqlConnection(connString),
            "mysql" => new MySqlConnection(EnsureMySqlConnStringParams(connString)),
            "doris" => new MySqlConnection(EnsureMySqlConnStringParams(connString)),  // Doris使用MySQL协议
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


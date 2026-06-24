using System.Data.Common;
using System.Text.Json;
using Bi.Domain.Entities;
using Bi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Microsoft.Data.SqlClient;
using MySqlConnector;

namespace Bi.Application.Services;

/// <summary>
/// 慢查询服务接口
/// </summary>
public interface ISlowQueryService
{
    /// <summary>
    /// 记录慢查询
    /// </summary>
    Task LogSlowQueryAsync(long datasourceId, long? chartId, string sql, long executionTimeMs, string? executedBy);

    /// <summary>
    /// 获取慢查询列表
    /// </summary>
    Task<List<SlowQueryLog>> GetSlowQueriesAsync(int page = 1, int pageSize = 20, bool? resolved = null);

    /// <summary>
    /// 执行EXPLAIN分析
    /// </summary>
    Task<string> AnalyzeQueryAsync(long logId);

    /// <summary>
    /// 标记为已处理
    /// </summary>
    Task MarkResolvedAsync(long logId, string? suggestion);

    /// <summary>
    /// 获取慢查询阈值（毫秒）
    /// </summary>
    long GetThresholdMs();
}

/// <summary>
/// 慢查询服务实现
/// </summary>
public class SlowQueryService : ISlowQueryService
{
    private readonly BiDbContext _db;
    private readonly ILogger<SlowQueryService> _logger;
    private readonly long _thresholdMs = 3000; // 默认3秒

    public SlowQueryService(BiDbContext db, ILogger<SlowQueryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public long GetThresholdMs() => _thresholdMs;

    public async Task LogSlowQueryAsync(long datasourceId, long? chartId, string sql, long executionTimeMs, string? executedBy)
    {
        if (executionTimeMs < _thresholdMs) return;

        var log = new SlowQueryLog
        {
            DatasourceId = datasourceId,
            ChartId = chartId,
            SqlText = sql,
            ExecutionTimeMs = executionTimeMs,
            ThresholdMs = _thresholdMs,
            ExecutedBy = executedBy,
            ExecutedAt = DateTime.Now,
            IsResolved = false
        };

        _db.SlowQueryLogs.Add(log);
        await _db.SaveChangesAsync();

        _logger.LogWarning("慢查询记录: {ExecutionTimeMs}ms, SQL: {Sql}", executionTimeMs, sql.Length > 200 ? sql[..200] + "..." : sql);
    }

    public async Task<List<SlowQueryLog>> GetSlowQueriesAsync(int page = 1, int pageSize = 20, bool? resolved = null)
    {
        var query = _db.SlowQueryLogs
            .Include(s => s.Datasource)
            .Include(s => s.Chart)
            .AsQueryable();

        if (resolved.HasValue)
            query = query.Where(s => s.IsResolved == resolved.Value);

        return await query
            .OrderByDescending(s => s.ExecutedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<string> AnalyzeQueryAsync(long logId)
    {
        var log = await _db.SlowQueryLogs
            .Include(s => s.Datasource)
            .FirstOrDefaultAsync(s => s.Id == logId);

        if (log?.Datasource == null)
            return "日志或数据源不存在";

        try
        {
            var explainSql = GetExplainSql(log.Datasource.Type, log.SqlText);
            using var conn = CreateConnection(log.Datasource.Type, log.Datasource.ConnString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = explainSql;

            var results = new List<Dictionary<string, object?>>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                results.Add(row);
            }

            var explainJson = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
            log.ExplainResult = explainJson;
            await _db.SaveChangesAsync();

            return explainJson;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EXPLAIN分析失败");
            return $"分析失败: {ex.Message}";
        }
    }

    public async Task MarkResolvedAsync(long logId, string? suggestion)
    {
        var log = await _db.SlowQueryLogs.FindAsync(logId);
        if (log != null)
        {
            log.IsResolved = true;
            log.Suggestion = suggestion;
            await _db.SaveChangesAsync();
        }
    }

    private static string GetExplainSql(string dbType, string sql)
    {
        return dbType.ToLower() switch
        {
            "postgresql" => $"EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) {sql}",
            "mysql" => $"EXPLAIN FORMAT=JSON {sql}",
            "sqlserver" => sql, // SQL Server需要SET SHOWPLAN_XML ON
            _ => $"EXPLAIN {sql}"
        };
    }

    private static DbConnection CreateConnection(string dbType, string connString)
    {
        return dbType.ToLower() switch
        {
            "postgresql" => new NpgsqlConnection(connString),
            "mysql" => new MySqlConnection(connString),
            "sqlserver" => new SqlConnection(connString),
            _ => throw new NotSupportedException($"不支持的数据库类型: {dbType}")
        };
    }
}


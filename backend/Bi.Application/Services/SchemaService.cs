using System.Data.Common;
using System.Text;
using Bi.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Npgsql;

namespace Bi.Application.Services;

/// <summary>
/// Schema元数据服务实现
/// 支持PostgreSQL、SQL Server、MySQL三种数据库
/// ★ 性能优化：内置内存缓存，避免重复查询数据库Schema
/// </summary>
public class SchemaService : ISchemaService
{
    private readonly BiDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SchemaService> _logger;

    // 缓存过期时间（5分钟）
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

    public SchemaService(BiDbContext db, IMemoryCache cache, ILogger<SchemaService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }
    
    /// <summary>
    /// 获取数据源下所有表/视图的列表（带缓存）
    /// </summary>
    public async Task<List<TableInfo>> GetTablesAsync(long datasourceId)
    {
        // ★ 缓存Key
        var cacheKey = $"schema_tables_{datasourceId}";

        // 尝试从缓存获取
        if (_cache.TryGetValue(cacheKey, out List<TableInfo>? cachedTables) && cachedTables != null)
        {
            _logger.LogDebug("Schema缓存命中: {Key}, 共 {Count} 张表", cacheKey, cachedTables.Count);
            return cachedTables;
        }

        var datasource = await _db.Datasources.FindAsync(datasourceId);
        if (datasource == null)
            throw new ArgumentException($"数据源不存在: {datasourceId}");

        using var conn = CreateConnection(datasource.Type, datasource.ConnString);
        await conn.OpenAsync();

        var tables = new List<TableInfo>();
        var sql = GetTablesQuery(datasource.Type);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 30;

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(new TableInfo
            {
                Name = reader.GetString(0),
                Type = reader.GetString(1),
                Comment = reader.IsDBNull(2) ? null : reader.GetString(2)
            });
        }

        // ★ 写入缓存
        _cache.Set(cacheKey, tables, CacheExpiration);
        _logger.LogInformation("Schema缓存写入: {Key}, 共 {Count} 张表, 过期时间 {Expiration}",
            cacheKey, tables.Count, CacheExpiration);

        return tables;
    }

    /// <summary>
    /// 获取指定表的字段信息（带缓存）
    /// </summary>
    public async Task<List<ColumnInfo>> GetColumnsAsync(long datasourceId, string tableName)
    {
        // ★ 缓存Key
        var cacheKey = $"schema_columns_{datasourceId}_{tableName}";

        // 尝试从缓存获取
        if (_cache.TryGetValue(cacheKey, out List<ColumnInfo>? cachedColumns) && cachedColumns != null)
        {
            _logger.LogDebug("Schema缓存命中: {Key}, 共 {Count} 个字段", cacheKey, cachedColumns.Count);
            return cachedColumns;
        }

        var datasource = await _db.Datasources.FindAsync(datasourceId);
        if (datasource == null)
            throw new ArgumentException($"数据源不存在: {datasourceId}");

        using var conn = CreateConnection(datasource.Type, datasource.ConnString);
        await conn.OpenAsync();

        var columns = new List<ColumnInfo>();
        var sql = GetColumnsQuery(datasource.Type, tableName);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 30;

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnInfo
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetString(2).ToUpper() == "YES",
                IsPrimaryKey = !reader.IsDBNull(3) && reader.GetString(3) == "PRI",
                Comment = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }

        // ★ 写入缓存
        _cache.Set(cacheKey, columns, CacheExpiration);
        _logger.LogDebug("Schema缓存写入: {Key}, 共 {Count} 个字段", cacheKey, columns.Count);

        return columns;
    }
    
    /// <summary>
    /// 生成用于AI Prompt的Schema描述文本
    /// </summary>
    public async Task<string> GenerateSchemaTextAsync(long datasourceId, List<string>? tableNames = null)
    {
        var tables = await GetTablesAsync(datasourceId);
        
        // 如果指定了表名，则过滤
        if (tableNames != null && tableNames.Count > 0)
        {
            tables = tables.Where(t => tableNames.Contains(t.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        }
        
        var sb = new StringBuilder();
        sb.AppendLine("## 数据库表结构");
        sb.AppendLine();
        
        foreach (var table in tables)
        {
            var columns = await GetColumnsAsync(datasourceId, table.Name);
            sb.AppendLine($"### 表: {table.Name}");
            if (!string.IsNullOrEmpty(table.Comment))
                sb.AppendLine($"描述: {table.Comment}");
            sb.AppendLine();
            sb.AppendLine("| 列名 | 类型 | 可空 | 主键 | 说明 |");
            sb.AppendLine("|------|------|------|------|------|");
            
            foreach (var col in columns)
            {
                var nullable = col.IsNullable ? "是" : "否";
                var pk = col.IsPrimaryKey ? "是" : "";
                var comment = col.Comment ?? "";
                sb.AppendLine($"| {col.Name} | {col.DataType} | {nullable} | {pk} | {comment} |");
            }
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// 根据数据集ID获取Schema描述
    /// </summary>
    public async Task<string> GetDatasetSchemaAsync(long datasetId)
    {
        var dataset = await _db.Datasets
            .Include(d => d.Fields)
            .Include(d => d.Datasource)
            .FirstOrDefaultAsync(d => d.Id == datasetId);
            
        if (dataset == null)
            throw new ArgumentException($"数据集不存在: {datasetId}");
            
        var sb = new StringBuilder();
        sb.AppendLine($"## 数据集: {dataset.Name}");
        if (!string.IsNullOrEmpty(dataset.Remark))
            sb.AppendLine($"描述: {dataset.Remark}");
        sb.AppendLine();
        sb.AppendLine("### SQL查询:");
        sb.AppendLine("```sql");
        sb.AppendLine(dataset.SqlText);
        sb.AppendLine("```");
        sb.AppendLine();
        
        if (dataset.Fields?.Any() == true)
        {
            sb.AppendLine("### 字段列表:");
            sb.AppendLine("| 字段名 | 别名 | 类型 | 角色 | 聚合 |");
            sb.AppendLine("|--------|------|------|------|------|");
            
            foreach (var field in dataset.Fields)
            {
                sb.AppendLine($"| {field.FieldName} | {field.FieldAlias} | {field.DataType} | {field.Role} | {field.AggType} |");
            }
        }

        return sb.ToString();
    }

    #region 私有方法

    /// <summary>
    /// 创建数据库连接
    /// </summary>
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

    private static string EnsureMySqlConnStringParams(string connString)
    {
        if (connString.Contains("ConnectionReset", StringComparison.OrdinalIgnoreCase))
            return connString;
        return connString.TrimEnd(';') + ";ConnectionReset=false;AllowUserVariables=true;CharSet=utf8mb4";
    }

    /// <summary>
    /// 获取表列表的SQL（根据数据库类型）
    /// </summary>
    private static string GetTablesQuery(string dbType)
    {
        return dbType.ToLower() switch
        {
            "postgres" or "postgresql" => @"
                SELECT table_name, table_type, obj_description(to_regclass(table_schema || '.' || table_name)) as comment
                FROM information_schema.tables
                WHERE table_schema = 'public'
                ORDER BY table_name",
            "sqlserver" or "mssql" => @"
                SELECT t.name,
                       CASE WHEN t.type = 'U' THEN 'TABLE' ELSE 'VIEW' END,
                       ep.value as comment
                FROM sys.tables t
                LEFT JOIN sys.extended_properties ep ON ep.major_id = t.object_id AND ep.minor_id = 0 AND ep.name = 'MS_Description'
                UNION ALL
                SELECT v.name, 'VIEW', ep.value
                FROM sys.views v
                LEFT JOIN sys.extended_properties ep ON ep.major_id = v.object_id AND ep.minor_id = 0 AND ep.name = 'MS_Description'
                ORDER BY 1",
            "mysql" or "doris" => @"
                SELECT table_name, table_type, table_comment
                FROM information_schema.tables
                WHERE table_schema = DATABASE()
                ORDER BY table_name",
            _ => throw new ArgumentException($"不支持的数据源类型: {dbType}")
        };
    }

    /// <summary>
    /// 获取列信息的SQL（根据数据库类型）
    /// </summary>
    private static string GetColumnsQuery(string dbType, string tableName)
    {
        return dbType.ToLower() switch
        {
            "postgres" or "postgresql" => $@"
                SELECT c.column_name, c.data_type, c.is_nullable,
                       CASE WHEN pk.column_name IS NOT NULL THEN 'PRI' ELSE '' END as key_type,
                       col_description(to_regclass('public.{tableName}'), c.ordinal_position) as comment
                FROM information_schema.columns c
                LEFT JOIN (
                    SELECT kcu.column_name
                    FROM information_schema.table_constraints tc
                    JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name
                    WHERE tc.table_name = '{tableName}' AND tc.constraint_type = 'PRIMARY KEY'
                ) pk ON c.column_name = pk.column_name
                WHERE c.table_schema = 'public' AND c.table_name = '{tableName}'
                ORDER BY c.ordinal_position",
            "sqlserver" or "mssql" => $@"
                SELECT c.name as column_name, TYPE_NAME(c.user_type_id) as data_type,
                       CASE WHEN c.is_nullable = 1 THEN 'YES' ELSE 'NO' END as is_nullable,
                       CASE WHEN pk.column_id IS NOT NULL THEN 'PRI' ELSE '' END as key_type,
                       ep.value as comment
                FROM sys.columns c
                JOIN sys.tables t ON c.object_id = t.object_id
                LEFT JOIN (
                    SELECT ic.column_id, ic.object_id
                    FROM sys.index_columns ic
                    JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                    WHERE i.is_primary_key = 1
                ) pk ON c.object_id = pk.object_id AND c.column_id = pk.column_id
                LEFT JOIN sys.extended_properties ep ON ep.major_id = c.object_id AND ep.minor_id = c.column_id AND ep.name = 'MS_Description'
                WHERE t.name = '{tableName}'
                ORDER BY c.column_id",
            "mysql" or "doris" => $@"
                SELECT column_name, column_type, is_nullable, column_key, column_comment
                FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = '{tableName}'
                ORDER BY ordinal_position",
            _ => throw new ArgumentException($"不支持的数据源类型: {dbType}")
        };
    }

    #endregion
}


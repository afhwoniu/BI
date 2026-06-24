namespace Bi.Application.Services;

/// <summary>
/// Schema元数据服务接口
/// 用于获取数据源的表结构、字段信息，生成Schema描述文本供AI Prompt使用
/// </summary>
public interface ISchemaService
{
    /// <summary>
    /// 获取数据源下所有表/视图的列表
    /// </summary>
    /// <param name="datasourceId">数据源ID</param>
    /// <returns>表/视图信息列表</returns>
    Task<List<TableInfo>> GetTablesAsync(long datasourceId);
    
    /// <summary>
    /// 获取指定表的字段信息
    /// </summary>
    /// <param name="datasourceId">数据源ID</param>
    /// <param name="tableName">表名</param>
    /// <returns>字段信息列表</returns>
    Task<List<ColumnInfo>> GetColumnsAsync(long datasourceId, string tableName);
    
    /// <summary>
    /// 生成用于AI Prompt的Schema描述文本
    /// </summary>
    /// <param name="datasourceId">数据源ID</param>
    /// <param name="tableNames">指定表名列表，为空则获取所有表</param>
    /// <returns>Schema描述文本</returns>
    Task<string> GenerateSchemaTextAsync(long datasourceId, List<string>? tableNames = null);
    
    /// <summary>
    /// 根据数据集ID获取Schema描述
    /// </summary>
    /// <param name="datasetId">数据集ID</param>
    /// <returns>数据集的Schema描述</returns>
    Task<string> GetDatasetSchemaAsync(long datasetId);
}

/// <summary>
/// 表/视图信息
/// </summary>
public class TableInfo
{
    /// <summary>
    /// 表名
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 类型：TABLE 或 VIEW
    /// </summary>
    public string Type { get; set; } = "TABLE";
    
    /// <summary>
    /// 表注释/描述
    /// </summary>
    public string? Comment { get; set; }
    
    /// <summary>
    /// 预估行数
    /// </summary>
    public long? RowCount { get; set; }
}

/// <summary>
/// 列/字段信息
/// </summary>
public class ColumnInfo
{
    /// <summary>
    /// 列名
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 数据类型
    /// </summary>
    public string DataType { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否可为空
    /// </summary>
    public bool IsNullable { get; set; }
    
    /// <summary>
    /// 是否为主键
    /// </summary>
    public bool IsPrimaryKey { get; set; }
    
    /// <summary>
    /// 列注释/描述
    /// </summary>
    public string? Comment { get; set; }
    
    /// <summary>
    /// 默认值
    /// </summary>
    public string? DefaultValue { get; set; }
}


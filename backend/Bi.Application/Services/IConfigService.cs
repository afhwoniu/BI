using Bi.Domain.Entities;

namespace Bi.Application.Services;

/// <summary>
/// 系统配置服务接口
/// </summary>
public interface IConfigService
{
    /// <summary>
    /// 获取配置值（字符串）
    /// </summary>
    Task<string?> GetAsync(string key, string? defaultValue = null);
    
    /// <summary>
    /// 获取配置值（泛型）
    /// </summary>
    Task<T?> GetAsync<T>(string key, T? defaultValue = default);
    
    /// <summary>
    /// 设置配置值
    /// </summary>
    Task SetAsync(string key, string? value, bool isEncrypted = false);
    
    /// <summary>
    /// 获取分组下所有配置
    /// </summary>
    Task<List<SysConfig>> GetByGroupAsync(string group);
    
    /// <summary>
    /// 批量保存配置
    /// </summary>
    Task SaveBatchAsync(List<SysConfig> configs);
    
    /// <summary>
    /// 刷新配置缓存
    /// </summary>
    Task RefreshCacheAsync();
    
    /// <summary>
    /// 初始化默认配置（首次运行）
    /// </summary>
    Task InitializeDefaultsAsync();
}

/// <summary>
/// 配置项DTO（用于前端展示，敏感值脱敏）
/// </summary>
public class ConfigDto
{
    public long Id { get; set; }
    public string ConfigKey { get; set; } = string.Empty;
    public string? ConfigValue { get; set; }
    public string ConfigGroup { get; set; } = string.Empty;
    public string ConfigType { get; set; } = "string";
    public bool IsEncrypted { get; set; }
    public string? DisplayName { get; set; }
    public string? Remark { get; set; }
    public int SortOrder { get; set; }
}


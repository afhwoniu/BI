using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bi.Application.Services;

/// <summary>
/// 图表查询结果缓存服务
/// </summary>
public interface IChartCacheService
{
    /// <summary>
    /// 获取缓存的查询结果
    /// </summary>
    Task<List<Dictionary<string, object>>?> GetAsync(long chartId, string? filterJson);

    /// <summary>
    /// 设置缓存
    /// </summary>
    Task SetAsync(long chartId, string? filterJson, List<Dictionary<string, object>> data, TimeSpan? expiration = null);

    /// <summary>
    /// 清除指定图表的缓存
    /// </summary>
    Task ClearChartCacheAsync(long chartId);

    /// <summary>
    /// 清除所有图表缓存
    /// </summary>
    Task ClearAllCacheAsync();

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    CacheStats GetStats();
}

/// <summary>
/// 缓存统计信息
/// </summary>
public class CacheStats
{
    public int TotalEntries { get; set; }
    public long HitCount { get; set; }
    public long MissCount { get; set; }
    public double HitRate => HitCount + MissCount > 0 ? (double)HitCount / (HitCount + MissCount) * 100 : 0;
}

/// <summary>
/// 基于内存的图表缓存服务实现
/// </summary>
public class MemoryChartCacheService : IChartCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryChartCacheService> _logger;
    private readonly HashSet<string> _cacheKeys = new();
    private readonly object _lockObj = new();
    private long _hitCount;
    private long _missCount;

    // 默认缓存时间（5分钟）
    private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(5);

    public MemoryChartCacheService(IMemoryCache cache, ILogger<MemoryChartCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task<List<Dictionary<string, object>>?> GetAsync(long chartId, string? filterJson)
    {
        var key = GenerateCacheKey(chartId, filterJson);
        if (_cache.TryGetValue(key, out List<Dictionary<string, object>>? data))
        {
            Interlocked.Increment(ref _hitCount);
            _logger.LogDebug("缓存命中: {Key}", key);
            return Task.FromResult(data);
        }

        Interlocked.Increment(ref _missCount);
        _logger.LogDebug("缓存未命中: {Key}", key);
        return Task.FromResult<List<Dictionary<string, object>>?>(null);
    }

    public Task SetAsync(long chartId, string? filterJson, List<Dictionary<string, object>> data, TimeSpan? expiration = null)
    {
        var key = GenerateCacheKey(chartId, filterJson);
        var exp = expiration ?? _defaultExpiration;

        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(exp)
            .RegisterPostEvictionCallback((k, v, r, s) =>
            {
                lock (_lockObj)
                {
                    _cacheKeys.Remove(k.ToString()!);
                }
            });

        _cache.Set(key, data, options);

        lock (_lockObj)
        {
            _cacheKeys.Add(key);
        }

        _logger.LogDebug("缓存设置: {Key}, 过期时间: {Expiration}", key, exp);
        return Task.CompletedTask;
    }

    public Task ClearChartCacheAsync(long chartId)
    {
        var prefix = $"chart:{chartId}:";
        var keysToRemove = new List<string>();

        lock (_lockObj)
        {
            keysToRemove = _cacheKeys.Where(k => k.StartsWith(prefix)).ToList();
            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
                _cacheKeys.Remove(key);
            }
        }

        _logger.LogInformation("清除图表缓存: ChartId={ChartId}, 清除数量={Count}", chartId, keysToRemove.Count);
        return Task.CompletedTask;
    }

    public Task ClearAllCacheAsync()
    {
        lock (_lockObj)
        {
            foreach (var key in _cacheKeys)
            {
                _cache.Remove(key);
            }
            _cacheKeys.Clear();
        }

        _logger.LogInformation("清除所有图表缓存");
        return Task.CompletedTask;
    }

    public CacheStats GetStats()
    {
        lock (_lockObj)
        {
            return new CacheStats
            {
                TotalEntries = _cacheKeys.Count,
                HitCount = _hitCount,
                MissCount = _missCount
            };
        }
    }

    private string GenerateCacheKey(long chartId, string? filterJson)
    {
        var filterHash = string.IsNullOrEmpty(filterJson) ? "none" : ComputeHash(filterJson);
        return $"chart:{chartId}:{filterHash}";
    }

    private static string ComputeHash(string input)
    {
        using var md5 = MD5.Create();
        var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLower();
    }
}


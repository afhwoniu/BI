using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bi.Application.Services;

namespace Bi.Api.Controllers;

/// <summary>
/// 缓存管理控制器
/// </summary>
[ApiController]
[Route("api/v1/cache")]
[Authorize]
public class CacheController : ControllerBase
{
    private readonly IChartCacheService _cacheService;

    public CacheController(IChartCacheService cacheService)
    {
        _cacheService = cacheService;
    }

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    [HttpGet("stats")]
    public IActionResult GetStats()
    {
        var stats = _cacheService.GetStats();
        return Ok(new
        {
            code = 0,
            message = "success",
            data = new
            {
                totalEntries = stats.TotalEntries,
                hitCount = stats.HitCount,
                missCount = stats.MissCount,
                hitRate = $"{stats.HitRate:F2}%"
            }
        });
    }

    /// <summary>
    /// 清除指定图表的缓存
    /// </summary>
    [HttpDelete("charts/{chartId}")]
    public async Task<IActionResult> ClearChartCache(long chartId)
    {
        await _cacheService.ClearChartCacheAsync(chartId);
        return Ok(new { code = 0, message = $"图表 {chartId} 的缓存已清除" });
    }

    /// <summary>
    /// 清除所有图表缓存
    /// </summary>
    [HttpDelete("charts")]
    public async Task<IActionResult> ClearAllCache()
    {
        await _cacheService.ClearAllCacheAsync();
        return Ok(new { code = 0, message = "所有图表缓存已清除" });
    }
}


using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bi.Application.Services;

namespace Bi.Api.Controllers;

/// <summary>
/// 慢查询日志管理控制器
/// </summary>
[ApiController]
[Route("api/v1/slow-query")]
[Authorize]
public class SlowQueryController : ControllerBase
{
    private readonly ISlowQueryService _slowQueryService;

    public SlowQueryController(ISlowQueryService slowQueryService)
    {
        _slowQueryService = slowQueryService;
    }

    /// <summary>
    /// 获取慢查询列表
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool? resolved = null)
    {
        var logs = await _slowQueryService.GetSlowQueriesAsync(page, pageSize, resolved);
        return Ok(new
        {
            code = 0,
            message = "success",
            data = logs.Select(l => new
            {
                l.Id,
                l.DatasourceId,
                datasourceName = l.Datasource?.Name,
                l.ChartId,
                chartName = l.Chart?.Name,
                sqlText = l.SqlText.Length > 200 ? l.SqlText[..200] + "..." : l.SqlText,
                l.ExecutionTimeMs,
                l.ThresholdMs,
                l.ExecutedBy,
                l.ExecutedAt,
                l.IsResolved,
                l.Suggestion
            })
        });
    }

    /// <summary>
    /// 获取慢查询详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetDetail(long id)
    {
        var logs = await _slowQueryService.GetSlowQueriesAsync(1, 1000);
        var log = logs.FirstOrDefault(l => l.Id == id);
        if (log == null)
            return Ok(new { code = 1, message = "日志不存在" });

        return Ok(new
        {
            code = 0,
            message = "success",
            data = new
            {
                log.Id,
                log.DatasourceId,
                datasourceName = log.Datasource?.Name,
                log.ChartId,
                chartName = log.Chart?.Name,
                log.SqlText,
                log.ExecutionTimeMs,
                log.ThresholdMs,
                log.ExecutedBy,
                log.ExecutedAt,
                log.ExplainResult,
                log.IsResolved,
                log.Suggestion
            }
        });
    }

    /// <summary>
    /// 执行EXPLAIN分析
    /// </summary>
    [HttpPost("{id}/analyze")]
    public async Task<IActionResult> Analyze(long id)
    {
        var result = await _slowQueryService.AnalyzeQueryAsync(id);
        return Ok(new { code = 0, message = "success", data = result });
    }

    /// <summary>
    /// 标记为已处理
    /// </summary>
    [HttpPost("{id}/resolve")]
    public async Task<IActionResult> Resolve(long id, [FromBody] ResolveRequest? request)
    {
        await _slowQueryService.MarkResolvedAsync(id, request?.Suggestion);
        return Ok(new { code = 0, message = "已标记为已处理" });
    }

    /// <summary>
    /// 获取慢查询阈值
    /// </summary>
    [HttpGet("threshold")]
    public IActionResult GetThreshold()
    {
        return Ok(new { code = 0, data = _slowQueryService.GetThresholdMs() });
    }
}

/// <summary>
/// 处理请求
/// </summary>
public class ResolveRequest
{
    /// <summary>
    /// 优化建议
    /// </summary>
    public string? Suggestion { get; set; }
}


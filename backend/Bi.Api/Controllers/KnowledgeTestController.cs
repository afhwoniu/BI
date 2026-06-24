using Bi.Application.Services;
using Bi.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bi.Api.Controllers;

/// <summary>
/// 知识库测试API
/// 用于评估知识库检索效果（命中率、MRR等指标）
/// </summary>
[ApiController]
[Route("api/v1/knowledge-test")]
[Authorize]
public class KnowledgeTestController : ControllerBase
{
    private readonly IKnowledgeTestService _testService;
    private readonly ILogger<KnowledgeTestController> _logger;

    public KnowledgeTestController(
        IKnowledgeTestService testService,
        ILogger<KnowledgeTestController> logger)
    {
        _testService = testService;
        _logger = logger;
    }

    #region 测试用例管理

    /// <summary>
    /// 获取所有测试用例
    /// </summary>
    [HttpGet("cases")]
    public async Task<IActionResult> GetTestCases()
    {
        var cases = await _testService.GetTestCasesAsync();
        return Ok(new { code = 200, data = cases });
    }

    /// <summary>
    /// 获取单个测试用例
    /// </summary>
    [HttpGet("cases/{id}")]
    public async Task<IActionResult> GetTestCase(long id)
    {
        var testCase = await _testService.GetTestCaseAsync(id);
        if (testCase == null)
            return Ok(new { code = 404, message = "测试用例不存在" });
        return Ok(new { code = 200, data = testCase });
    }

    /// <summary>
    /// 创建测试用例
    /// </summary>
    [HttpPost("cases")]
    public async Task<IActionResult> CreateTestCase([FromBody] CreateTestCaseRequest req)
    {
        var testCase = new KnowledgeTestCase
        {
            Name = req.Name,
            Query = req.Query,
            ExpectedDocumentIds = req.ExpectedDocumentIds,
            ExpectedChunkIds = req.ExpectedChunkIds,
            ExpectedKeywords = req.ExpectedKeywords,
            CategoryId = req.CategoryId,
            Remark = req.Remark,
            IsEnabled = req.IsEnabled
        };
        var created = await _testService.CreateTestCaseAsync(testCase);
        return Ok(new { code = 200, data = created, message = "创建成功" });
    }

    /// <summary>
    /// 更新测试用例
    /// </summary>
    [HttpPut("cases/{id}")]
    public async Task<IActionResult> UpdateTestCase(long id, [FromBody] CreateTestCaseRequest req)
    {
        var testCase = new KnowledgeTestCase
        {
            Id = id,
            Name = req.Name,
            Query = req.Query,
            ExpectedDocumentIds = req.ExpectedDocumentIds,
            ExpectedChunkIds = req.ExpectedChunkIds,
            ExpectedKeywords = req.ExpectedKeywords,
            CategoryId = req.CategoryId,
            Remark = req.Remark,
            IsEnabled = req.IsEnabled
        };
        var updated = await _testService.UpdateTestCaseAsync(testCase);
        return Ok(new { code = 200, data = updated, message = "更新成功" });
    }

    /// <summary>
    /// 删除测试用例
    /// </summary>
    [HttpDelete("cases/{id}")]
    public async Task<IActionResult> DeleteTestCase(long id)
    {
        await _testService.DeleteTestCaseAsync(id);
        return Ok(new { code = 200, message = "删除成功" });
    }

    #endregion

    #region 测试运行管理

    /// <summary>
    /// 获取测试运行列表
    /// </summary>
    [HttpGet("runs")]
    public async Task<IActionResult> GetTestRuns([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var runs = await _testService.GetTestRunsAsync(page, pageSize);
        return Ok(new { code = 200, data = runs });
    }

    /// <summary>
    /// 获取测试运行详情（含报告）
    /// </summary>
    [HttpGet("runs/{id}")]
    public async Task<IActionResult> GetTestRun(long id)
    {
        try
        {
            var report = await _testService.GetTestReportAsync(id);
            return Ok(new { code = 200, data = report });
        }
        catch (Exception ex)
        {
            return Ok(new { code = 404, message = ex.Message });
        }
    }

    /// <summary>
    /// 启动新的测试运行
    /// </summary>
    [HttpPost("runs")]
    public async Task<IActionResult> StartTestRun([FromBody] StartTestRunRequest req)
    {
        try
        {
            var run = await _testService.StartTestRunAsync(req.Name, req.TopK, req.MinScore);
            return Ok(new { code = 200, data = run, message = "测试已启动" });
        }
        catch (Exception ex)
        {
            return Ok(new { code = 400, message = ex.Message });
        }
    }

    /// <summary>
    /// 删除测试运行记录
    /// </summary>
    [HttpDelete("runs/{id}")]
    public async Task<IActionResult> DeleteTestRun(long id)
    {
        await _testService.DeleteTestRunAsync(id);
        return Ok(new { code = 200, message = "删除成功" });
    }

    #endregion
}

public class CreateTestCaseRequest
{
    public string Name { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string? ExpectedDocumentIds { get; set; }
    public string? ExpectedChunkIds { get; set; }
    public string? ExpectedKeywords { get; set; }
    public long? CategoryId { get; set; }
    public string? Remark { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class StartTestRunRequest
{
    public string? Name { get; set; }
    public int TopK { get; set; } = 5;
    public float MinScore { get; set; } = 0.5f;
}


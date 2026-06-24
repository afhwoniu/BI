using Bi.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bi.Api.Controllers;

/// <summary>
/// 报表管理控制器
/// </summary>
[ApiController]
[Route("api/v1/reports")]
[Authorize]
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// 获取报表列表
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetList()
    {
        var list = await _reportService.GetListAsync();
        return Ok(new { code = 0, message = "success", data = list });
    }

    /// <summary>
    /// 获取报表详情
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var report = await _reportService.GetByIdAsync(id);
        if (report == null)
            return NotFound(new { code = 404, message = "报表不存在" });
        return Ok(new { code = 0, message = "success", data = report });
    }

    /// <summary>
    /// 创建报表
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ReportCreateDto dto)
    {
        var report = await _reportService.CreateAsync(dto);
        return Ok(new { code = 0, message = "创建成功", data = new { id = report.Id } });
    }

    /// <summary>
    /// 更新报表
    /// </summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] ReportUpdateDto dto)
    {
        var success = await _reportService.UpdateAsync(id, dto);
        if (!success)
            return NotFound(new { code = 404, message = "报表不存在" });
        return Ok(new { code = 0, message = "更新成功" });
    }

    /// <summary>
    /// 删除报表
    /// </summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var success = await _reportService.DeleteAsync(id);
        if (!success)
            return NotFound(new { code = 404, message = "报表不存在" });
        return Ok(new { code = 0, message = "删除成功" });
    }

    /// <summary>
    /// 保存页面
    /// </summary>
    [HttpPost("{reportId:long}/pages")]
    public async Task<IActionResult> SavePage(long reportId, [FromBody] ReportPageDto dto)
    {
        var page = await _reportService.SavePageAsync(reportId, dto);
        return Ok(new { code = 0, message = "保存成功", data = new { id = page.Id } });
    }

    /// <summary>
    /// 删除页面
    /// </summary>
    [HttpDelete("pages/{pageId:long}")]
    public async Task<IActionResult> DeletePage(long pageId)
    {
        var success = await _reportService.DeletePageAsync(pageId);
        if (!success)
            return NotFound(new { code = 404, message = "页面不存在" });
        return Ok(new { code = 0, message = "删除成功" });
    }

    /// <summary>
    /// 保存元素
    /// </summary>
    [HttpPost("pages/{pageId:long}/items")]
    public async Task<IActionResult> SaveItem(long pageId, [FromBody] ReportItemDto dto)
    {
        var item = await _reportService.SaveItemAsync(pageId, dto);
        return Ok(new { code = 0, message = "保存成功", data = new { id = item.Id } });
    }

    /// <summary>
    /// 删除元素
    /// </summary>
    [HttpDelete("items/{itemId:long}")]
    public async Task<IActionResult> DeleteItem(long itemId)
    {
        var success = await _reportService.DeleteItemAsync(itemId);
        if (!success)
            return NotFound(new { code = 404, message = "元素不存在" });
        return Ok(new { code = 0, message = "删除成功" });
    }

    /// <summary>
    /// 获取报表渲染数据
    /// </summary>
    [HttpGet("{id:long}/render")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRenderData(long id)
    {
        var data = await _reportService.GetRenderDataAsync(id);
        if (data == null)
            return NotFound(new { code = 404, message = "报表不存在" });
        return Ok(new { code = 0, message = "success", data });
    }
}


using Bi.Api.Models;
using Bi.Application.Services;
using Bi.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bi.Api.Controllers;

/// <summary>
/// 知识库管理控制器
/// 提供文档上传、分类管理、向量检索等功能
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class KnowledgeController : ControllerBase
{
    private readonly IKnowledgeService _knowledgeService;
    private readonly ILogger<KnowledgeController> _logger;

    public KnowledgeController(IKnowledgeService knowledgeService, ILogger<KnowledgeController> logger)
    {
        _knowledgeService = knowledgeService;
        _logger = logger;
    }

    #region 分类管理
    /// <summary>
    /// 获取分类列表（树形结构）
    /// </summary>
    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<List<KnowledgeCategory>>>> GetCategories()
    {
        var categories = await _knowledgeService.GetCategoriesAsync();
        return Ok(ApiResponse<List<KnowledgeCategory>>.Success(categories));
    }

    /// <summary>
    /// 创建分类
    /// </summary>
    [HttpPost("categories")]
    public async Task<ActionResult<ApiResponse<KnowledgeCategory>>> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        var category = await _knowledgeService.CreateCategoryAsync(request.Name, request.ParentId, request.Description);
        return Ok(ApiResponse<KnowledgeCategory>.Success(category));
    }

    /// <summary>
    /// 更新分类
    /// </summary>
    [HttpPut("categories/{id}")]
    public async Task<ActionResult<ApiResponse<KnowledgeCategory>>> UpdateCategory(long id, [FromBody] UpdateCategoryRequest request)
    {
        try
        {
            var category = await _knowledgeService.UpdateCategoryAsync(id, request.Name, request.Description);
            return Ok(ApiResponse<KnowledgeCategory>.Success(category));
        }
        catch (KeyNotFoundException ex)
        {
            return Ok(ApiResponse<KnowledgeCategory>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 删除分类
    /// </summary>
    [HttpDelete("categories/{id}")]
    public async Task<ActionResult<ApiResponse>> DeleteCategory(long id)
    {
        try
        {
            await _knowledgeService.DeleteCategoryAsync(id);
            return Ok(ApiResponse.Success("删除成功"));
        }
        catch (KeyNotFoundException ex)
        {
            return Ok(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Ok(ApiResponse.Fail(ex.Message));
        }
    }
    #endregion

    #region 文档管理
    /// <summary>
    /// 获取文档列表
    /// </summary>
    [HttpGet("documents")]
    public async Task<ActionResult<ApiResponse<DocumentListResult>>> GetDocuments(
        [FromQuery] long? categoryId,
        [FromQuery] string? status,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var (items, total) = await _knowledgeService.GetDocumentsAsync(categoryId, status, keyword, page, pageSize);
        return Ok(ApiResponse<DocumentListResult>.Success(new DocumentListResult
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        }));
    }

    /// <summary>
    /// 获取文档详情
    /// </summary>
    [HttpGet("documents/{id}")]
    public async Task<ActionResult<ApiResponse<KnowledgeDocument>>> GetDocument(long id)
    {
        var document = await _knowledgeService.GetDocumentAsync(id);
        if (document == null)
            return Ok(ApiResponse<KnowledgeDocument>.Fail("文档不存在"));
        return Ok(ApiResponse<KnowledgeDocument>.Success(document));
    }

    /// <summary>
    /// 上传文档
    /// </summary>
    [HttpPost("documents/upload")]
    public async Task<ActionResult<ApiResponse<KnowledgeDocument>>> UploadDocument(
        [FromForm] string title,
        [FromForm] IFormFile file,
        [FromForm] long? categoryId,
        [FromForm] long? datasourceId)
    {
        if (file == null || file.Length == 0)
            return Ok(ApiResponse<KnowledgeDocument>.Fail("请选择文件"));

        try
        {
            // 获取当前用户ID
            var userIdClaim = User.FindFirst("userId")?.Value;
            long? userId = long.TryParse(userIdClaim, out var uid) ? uid : null;

            using var stream = file.OpenReadStream();
            var document = await _knowledgeService.CreateDocumentAsync(
                title,
                file.FileName,
                Path.GetExtension(file.FileName).TrimStart('.'),
                file.Length,
                stream,
                categoryId,
                datasourceId,
                userId);

            return Ok(ApiResponse<KnowledgeDocument>.Success(document));
        }
        catch (NotSupportedException ex)
        {
            return Ok(ApiResponse<KnowledgeDocument>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Ok(ApiResponse<KnowledgeDocument>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上传文档失败: {FileName}", file.FileName);
            return Ok(ApiResponse<KnowledgeDocument>.Fail($"上传失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 获取文档处理状态（用于轮询进度）
    /// </summary>
    [HttpGet("documents/{id}/status")]
    public async Task<ActionResult<ApiResponse<DocumentStatusResult>>> GetDocumentStatus(long id)
    {
        var document = await _knowledgeService.GetDocumentAsync(id);
        if (document == null)
            return Ok(ApiResponse<DocumentStatusResult>.Fail("文档不存在"));

        return Ok(ApiResponse<DocumentStatusResult>.Success(new DocumentStatusResult
        {
            Id = document.Id,
            Status = document.Status,
            Progress = document.ProcessProgress,
            ChunkCount = document.ChunkCount,
            ProcessedChunkCount = document.ProcessedChunkCount,
            ErrorMessage = document.ErrorMessage
        }));
    }

    /// <summary>
    /// 删除文档
    /// </summary>
    [HttpDelete("documents/{id}")]
    public async Task<ActionResult<ApiResponse>> DeleteDocument(long id)
    {
        try
        {
            await _knowledgeService.DeleteDocumentAsync(id);
            return Ok(ApiResponse.Success("删除成功"));
        }
        catch (KeyNotFoundException ex)
        {
            return Ok(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 下载文档原始文件
    /// </summary>
    [HttpGet("documents/{id}/download")]
    [AllowAnonymous]  // 允许匿名下载，方便前端直接打开链接
    public async Task<IActionResult> DownloadDocument(long id)
    {
        try
        {
            var document = await _knowledgeService.GetDocumentAsync(id);
            if (document == null)
                return NotFound("文档不存在");

            if (string.IsNullOrEmpty(document.FilePath) || !System.IO.File.Exists(document.FilePath))
                return NotFound("文件不存在或已被删除");

            var fileBytes = await System.IO.File.ReadAllBytesAsync(document.FilePath);
            var contentType = GetContentType(document.FileType);
            var fileName = document.FileName ?? $"{document.Title}.{document.FileType}";

            return File(fileBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载文档失败: {Id}", id);
            return StatusCode(500, "下载失败");
        }
    }

    /// <summary>
    /// 根据文件类型获取MIME类型
    /// </summary>
    private static string GetContentType(string? fileType)
    {
        return fileType?.ToLower() switch
        {
            "pdf" => "application/pdf",
            "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "doc" => "application/msword",
            "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "xls" => "application/vnd.ms-excel",
            "txt" => "text/plain",
            "md" => "text/markdown",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// 重新处理文档
    /// </summary>
    [HttpPost("documents/{id}/reprocess")]
    public async Task<ActionResult<ApiResponse>> ReprocessDocument(long id)
    {
        try
        {
            await _knowledgeService.ReprocessDocumentAsync(id);
            return Ok(ApiResponse.Success("已开始重新处理"));
        }
        catch (KeyNotFoundException ex)
        {
            return Ok(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 获取文档分块列表
    /// </summary>
    [HttpGet("documents/{id}/chunks")]
    public async Task<ActionResult<ApiResponse<List<KnowledgeChunk>>>> GetDocumentChunks(long id)
    {
        var document = await _knowledgeService.GetDocumentAsync(id);
        if (document == null)
            return Ok(ApiResponse<List<KnowledgeChunk>>.Fail("文档不存在"));

        return Ok(ApiResponse<List<KnowledgeChunk>>.Success(document.Chunks.OrderBy(c => c.ChunkIndex).ToList()));
    }
    #endregion

    #region 向量检索
    /// <summary>
    /// 语义检索
    /// </summary>
    [HttpPost("search")]
    public async Task<ActionResult<ApiResponse<List<KnowledgeSearchResult>>>> Search([FromBody] SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return Ok(ApiResponse<List<KnowledgeSearchResult>>.Fail("查询内容不能为空"));

        var results = await _knowledgeService.SearchAsync(
            request.Query,
            request.TopK,
            request.CategoryId,
            request.DatasourceId,
            request.MinScore);

        return Ok(ApiResponse<List<KnowledgeSearchResult>>.Success(results));
    }
    #endregion
}

#region 请求模型
/// <summary>
/// 创建分类请求
/// </summary>
public class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public long? ParentId { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// 更新分类请求
/// </summary>
public class UpdateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// 检索请求
/// </summary>
public class SearchRequest
{
    /// <summary>
    /// 查询文本
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// 返回数量
    /// </summary>
    public int TopK { get; set; } = 5;

    /// <summary>
    /// 限定分类
    /// </summary>
    public long? CategoryId { get; set; }

    /// <summary>
    /// 限定数据源
    /// </summary>
    public long? DatasourceId { get; set; }

    /// <summary>
    /// 最小相似度阈值
    /// </summary>
    public float MinScore { get; set; } = 0.5f;
}

/// <summary>
/// 文档列表结果
/// </summary>
public class DocumentListResult
{
    public List<KnowledgeDocument> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 文档处理状态结果
/// </summary>
public class DocumentStatusResult
{
    /// <summary>
    /// 文档ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 处理状态：pending-待处理, processing-处理中, completed-已完成, failed-失败
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 处理进度（0-100）
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// 总分块数
    /// </summary>
    public int ChunkCount { get; set; }

    /// <summary>
    /// 已处理分块数
    /// </summary>
    public int ProcessedChunkCount { get; set; }

    /// <summary>
    /// 错误信息（失败时）
    /// </summary>
    public string? ErrorMessage { get; set; }
}
#endregion


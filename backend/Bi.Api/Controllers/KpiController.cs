using Bi.Api.Models;
using Bi.Application.Services;
using Bi.Domain.Entities;
using Bi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bi.Api.Controllers;

/// <summary>
/// 指标知识库管理控制器
/// </summary>
[ApiController]
[Route("api/v1/kpi")]
[Authorize]
public class KpiController : ControllerBase
{
    private readonly BiDbContext _db;
    private readonly IKpiRetrieverService _kpiRetriever;
    private readonly ILogger<KpiController> _logger;
    
    public KpiController(BiDbContext db, IKpiRetrieverService kpiRetriever, ILogger<KpiController> logger)
    {
        _db = db;
        _kpiRetriever = kpiRetriever;
        _logger = logger;
    }
    
    #region 分类管理
    
    /// <summary>
    /// 获取分类树
    /// </summary>
    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<List<KpiCategoryDto>>>> GetCategories()
    {
        var categories = await _db.KpiCategories
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .ToListAsync();
        
        var tree = BuildCategoryTree(categories, null);
        return Ok(ApiResponse<List<KpiCategoryDto>>.Success(tree));
    }
    
    /// <summary>
    /// 创建分类
    /// </summary>
    [HttpPost("categories")]
    public async Task<ActionResult<ApiResponse<KpiCategory>>> CreateCategory([FromBody] KpiCategoryCreateDto dto)
    {
        var category = new KpiCategory
        {
            Name = dto.Name,
            ParentId = dto.ParentId,
            Description = dto.Description,
            SortOrder = dto.SortOrder
        };
        
        _db.KpiCategories.Add(category);
        await _db.SaveChangesAsync();
        
        return Ok(ApiResponse<KpiCategory>.Success(category));
    }
    
    /// <summary>
    /// 更新分类
    /// </summary>
    [HttpPut("categories/{id}")]
    public async Task<ActionResult<ApiResponse<KpiCategory>>> UpdateCategory(long id, [FromBody] KpiCategoryCreateDto dto)
    {
        var category = await _db.KpiCategories.FindAsync(id);
        if (category == null)
            return Ok(ApiResponse<KpiCategory>.Fail("分类不存在"));
        
        category.Name = dto.Name;
        category.ParentId = dto.ParentId;
        category.Description = dto.Description;
        category.SortOrder = dto.SortOrder;
        category.UpdatedAt = DateTime.UtcNow;
        
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<KpiCategory>.Success(category));
    }
    
    /// <summary>
    /// 删除分类
    /// </summary>
    [HttpDelete("categories/{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteCategory(long id)
    {
        var category = await _db.KpiCategories.FindAsync(id);
        if (category == null)
            return Ok(ApiResponse<bool>.Fail("分类不存在"));
        
        // 检查是否有子分类
        var hasChildren = await _db.KpiCategories.AnyAsync(c => c.ParentId == id);
        if (hasChildren)
            return Ok(ApiResponse<bool>.Fail("请先删除子分类"));
        
        // 检查是否有指标
        var hasKpis = await _db.KpiDefinitions.AnyAsync(k => k.CategoryId == id);
        if (hasKpis)
            return Ok(ApiResponse<bool>.Fail("请先删除或移动该分类下的指标"));
        
        _db.KpiCategories.Remove(category);
        await _db.SaveChangesAsync();
        
        return Ok(ApiResponse<bool>.Success(true));
    }
    
    #endregion
    
    #region 指标管理
    
    /// <summary>
    /// 获取指标列表
    /// </summary>
    [HttpGet("definitions")]
    public async Task<ActionResult<ApiResponse<PagedResult<KpiDefinitionDto>>>> GetDefinitions(
        [FromQuery] long? categoryId,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.KpiDefinitions.Include(k => k.Category).AsQueryable();
        
        if (categoryId.HasValue)
            query = query.Where(k => k.CategoryId == categoryId.Value);
        
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(k => k.Name.Contains(keyword) || k.Code.Contains(keyword));
        
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(k => k.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(k => new KpiDefinitionDto
            {
                Id = k.Id,
                Code = k.Code,
                Name = k.Name,
                CategoryId = k.CategoryId,
                CategoryName = k.Category != null ? k.Category.Name : null,
                Definition = k.Definition,
                Formula = k.Formula,
                Unit = k.Unit,
                DataType = k.DataType,
                HasEmbedding = k.EmbeddingJson != null,
                EmbeddingUpdatedAt = k.EmbeddingUpdatedAt,
                IsEnabled = k.IsEnabled,
                CreatedAt = k.CreatedAt,
                UpdatedAt = k.UpdatedAt
            })
            .ToListAsync();
        
        return Ok(ApiResponse<PagedResult<KpiDefinitionDto>>.Success(new PagedResult<KpiDefinitionDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        }));
    }

    /// <summary>
    /// 获取指标详情
    /// </summary>
    [HttpGet("definitions/{id}")]
    public async Task<ActionResult<ApiResponse<KpiDefinition>>> GetDefinition(long id)
    {
        var kpi = await _db.KpiDefinitions.Include(k => k.Category).FirstOrDefaultAsync(k => k.Id == id);
        if (kpi == null)
            return Ok(ApiResponse<KpiDefinition>.Fail("指标不存在"));

        return Ok(ApiResponse<KpiDefinition>.Success(kpi));
    }

    /// <summary>
    /// 创建指标
    /// </summary>
    [HttpPost("definitions")]
    public async Task<ActionResult<ApiResponse<KpiDefinition>>> CreateDefinition([FromBody] KpiDefinitionCreateDto dto)
    {
        // 检查编码唯一性
        var exists = await _db.KpiDefinitions.AnyAsync(k => k.Code == dto.Code);
        if (exists)
            return Ok(ApiResponse<KpiDefinition>.Fail("指标编码已存在"));

        var kpi = new KpiDefinition
        {
            Code = dto.Code,
            Name = dto.Name,
            CategoryId = dto.CategoryId,
            Definition = dto.Definition,
            Formula = dto.Formula,
            SqlTemplate = dto.SqlTemplate,
            DatasourceId = dto.DatasourceId,
            Unit = dto.Unit,
            DataType = dto.DataType ?? "number",
            IsEnabled = dto.IsEnabled
        };

        _db.KpiDefinitions.Add(kpi);
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<KpiDefinition>.Success(kpi));
    }

    /// <summary>
    /// 更新指标
    /// </summary>
    [HttpPut("definitions/{id}")]
    public async Task<ActionResult<ApiResponse<KpiDefinition>>> UpdateDefinition(long id, [FromBody] KpiDefinitionCreateDto dto)
    {
        var kpi = await _db.KpiDefinitions.FindAsync(id);
        if (kpi == null)
            return Ok(ApiResponse<KpiDefinition>.Fail("指标不存在"));

        // 检查编码唯一性
        var exists = await _db.KpiDefinitions.AnyAsync(k => k.Code == dto.Code && k.Id != id);
        if (exists)
            return Ok(ApiResponse<KpiDefinition>.Fail("指标编码已存在"));

        kpi.Code = dto.Code;
        kpi.Name = dto.Name;
        kpi.CategoryId = dto.CategoryId;
        kpi.Definition = dto.Definition;
        kpi.Formula = dto.Formula;
        kpi.SqlTemplate = dto.SqlTemplate;
        kpi.DatasourceId = dto.DatasourceId;
        kpi.Unit = dto.Unit;
        kpi.DataType = dto.DataType ?? "number";
        kpi.IsEnabled = dto.IsEnabled;
        kpi.UpdatedAt = DateTime.UtcNow;
        // 内容变更后需要重新生成向量
        kpi.EmbeddingJson = null;
        kpi.EmbeddingUpdatedAt = null;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<KpiDefinition>.Success(kpi));
    }

    /// <summary>
    /// 删除指标
    /// </summary>
    [HttpDelete("definitions/{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteDefinition(long id)
    {
        var kpi = await _db.KpiDefinitions.FindAsync(id);
        if (kpi == null)
            return Ok(ApiResponse<bool>.Fail("指标不存在"));

        _db.KpiDefinitions.Remove(kpi);
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<bool>.Success(true));
    }

    #endregion

    #region 向量检索

    /// <summary>
    /// 语义检索指标
    /// </summary>
    [HttpPost("search")]
    public async Task<ActionResult<ApiResponse<List<KpiSearchResultDto>>>> Search([FromBody] KpiSearchRequest request)
    {
        var results = await _kpiRetriever.SearchAsync(request.Query, request.TopK, request.MinScore);

        var dtos = results.Select(r => new KpiSearchResultDto
        {
            Id = r.Kpi.Id,
            Code = r.Kpi.Code,
            Name = r.Kpi.Name,
            Definition = r.Kpi.Definition,
            Formula = r.Kpi.Formula,
            SqlTemplate = r.Kpi.SqlTemplate,
            Unit = r.Kpi.Unit,
            Score = r.Score
        }).ToList();

        return Ok(ApiResponse<List<KpiSearchResultDto>>.Success(dtos));
    }

    /// <summary>
    /// 为指标生成向量
    /// </summary>
    [HttpPost("definitions/{id}/embedding")]
    public async Task<ActionResult<ApiResponse<bool>>> GenerateEmbedding(long id)
    {
        try
        {
            await _kpiRetriever.GenerateEmbeddingAsync(id);
            return Ok(ApiResponse<bool>.Success(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成向量失败: {Id}", id);
            return Ok(ApiResponse<bool>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 批量生成向量
    /// </summary>
    [HttpPost("definitions/embeddings")]
    public async Task<ActionResult<ApiResponse<bool>>> GenerateEmbeddings([FromBody] List<long>? ids)
    {
        try
        {
            await _kpiRetriever.GenerateEmbeddingsAsync(ids);
            return Ok(ApiResponse<bool>.Success(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量生成向量失败");
            return Ok(ApiResponse<bool>.Fail(ex.Message));
        }
    }

    #endregion

    #region 辅助方法

    private static List<KpiCategoryDto> BuildCategoryTree(List<KpiCategory> all, long? parentId)
    {
        return all
            .Where(c => c.ParentId == parentId)
            .Select(c => new KpiCategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                ParentId = c.ParentId,
                Description = c.Description,
                SortOrder = c.SortOrder,
                Children = BuildCategoryTree(all, c.Id)
            })
            .ToList();
    }

    #endregion
}


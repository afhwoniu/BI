using System.Diagnostics;
using System.Text.Json;
using Bi.Domain.Entities;
using Bi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bi.Application.Services;

/// <summary>
/// 知识库测试服务实现
/// </summary>
public class KnowledgeTestService : IKnowledgeTestService
{
    private readonly BiDbContext _db;
    private readonly IUnifiedSearchService _searchService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KnowledgeTestService> _logger;

    public KnowledgeTestService(
        BiDbContext db,
        IUnifiedSearchService searchService,
        IServiceScopeFactory scopeFactory,
        ILogger<KnowledgeTestService> logger)
    {
        _db = db;
        _searchService = searchService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    #region 测试用例管理

    public async Task<List<KnowledgeTestCase>> GetTestCasesAsync()
    {
        return await _db.KnowledgeTestCases
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync();
    }

    public async Task<KnowledgeTestCase?> GetTestCaseAsync(long id)
    {
        return await _db.KnowledgeTestCases.FindAsync(id);
    }

    public async Task<KnowledgeTestCase> CreateTestCaseAsync(KnowledgeTestCase testCase)
    {
        testCase.CreatedAt = DateTime.UtcNow;
        testCase.UpdatedAt = DateTime.UtcNow;
        _db.KnowledgeTestCases.Add(testCase);
        await _db.SaveChangesAsync();
        return testCase;
    }

    public async Task<KnowledgeTestCase> UpdateTestCaseAsync(KnowledgeTestCase testCase)
    {
        var existing = await _db.KnowledgeTestCases.FindAsync(testCase.Id);
        if (existing == null) throw new Exception("测试用例不存在");

        existing.Name = testCase.Name;
        existing.Query = testCase.Query;
        existing.ExpectedDocumentIds = testCase.ExpectedDocumentIds;
        existing.ExpectedChunkIds = testCase.ExpectedChunkIds;
        existing.ExpectedKeywords = testCase.ExpectedKeywords;
        existing.CategoryId = testCase.CategoryId;
        existing.Remark = testCase.Remark;
        existing.IsEnabled = testCase.IsEnabled;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteTestCaseAsync(long id)
    {
        var testCase = await _db.KnowledgeTestCases.FindAsync(id);
        if (testCase != null)
        {
            _db.KnowledgeTestCases.Remove(testCase);
            await _db.SaveChangesAsync();
        }
    }

    #endregion

    #region 测试运行管理

    public async Task<List<KnowledgeTestRun>> GetTestRunsAsync(int page = 1, int pageSize = 20)
    {
        return await _db.KnowledgeTestRuns
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<KnowledgeTestRun?> GetTestRunAsync(long id)
    {
        return await _db.KnowledgeTestRuns.FindAsync(id);
    }

    public async Task<KnowledgeTestRun> StartTestRunAsync(string? name, int topK = 5, float minScore = 0.5f)
    {
        var enabledCases = await _db.KnowledgeTestCases
            .Where(x => x.IsEnabled)
            .ToListAsync();

        if (enabledCases.Count == 0)
            throw new Exception("没有启用的测试用例");

        var run = new KnowledgeTestRun
        {
            Name = name ?? $"测试运行 {DateTime.Now:yyyy-MM-dd HH:mm}",
            Status = "running",
            TotalCases = enabledCases.Count,
            TopK = topK,
            MinScore = minScore,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        _db.KnowledgeTestRuns.Add(run);
        await _db.SaveChangesAsync();

        // 异步执行测试（不阻塞请求），使用IServiceScopeFactory创建新作用域
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var scopedDb = scope.ServiceProvider.GetRequiredService<BiDbContext>();
            var scopedSearchService = scope.ServiceProvider.GetRequiredService<IUnifiedSearchService>();
            await ExecuteTestRunAsync(scopedDb, scopedSearchService, run.Id, enabledCases, topK, minScore);
        });

        return run;
    }

    public async Task DeleteTestRunAsync(long id)
    {
        var run = await _db.KnowledgeTestRuns.FindAsync(id);
        if (run != null)
        {
            _db.KnowledgeTestRuns.Remove(run);
            await _db.SaveChangesAsync();
        }
    }

    #endregion

    #region 测试报告

    public async Task<TestReportDto> GetTestReportAsync(long runId)
    {
        var run = await _db.KnowledgeTestRuns.FindAsync(runId);
        if (run == null) throw new Exception("测试运行不存在");

        var report = new TestReportDto
        {
            RunId = run.Id,
            Name = run.Name,
            Status = run.Status,
            TotalCases = run.TotalCases,
            CompletedCases = run.CompletedCases,
            TopK = run.TopK,
            MinScore = run.MinScore,
            HitRate = run.HitRate,
            Mrr = run.Mrr,
            AvgPrecision = run.AvgPrecision,
            AvgRecall = run.AvgRecall,
            AvgLatencyMs = run.AvgLatencyMs,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt
        };

        // 解析详细结果
        if (!string.IsNullOrEmpty(run.DetailResults))
        {
            try
            {
                report.Details = JsonSerializer.Deserialize<List<TestCaseResultDto>>(run.DetailResults) ?? new();
            }
            catch { }
        }

        return report;
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 执行测试运行（在独立作用域中）
    /// </summary>
    private async Task ExecuteTestRunAsync(BiDbContext db, IUnifiedSearchService searchService,
        long runId, List<KnowledgeTestCase> cases, int topK, float minScore)
    {
        try
        {
            var results = new List<TestCaseResultDto>();
            int hitCount = 0;
            float totalRR = 0;
            float totalPrecision = 0;
            float totalRecall = 0;
            float totalLatency = 0;

            foreach (var testCase in cases)
            {
                var result = await ExecuteSingleTestAsync(searchService, testCase, topK, minScore);
                results.Add(result);

                if (result.IsHit) hitCount++;
                totalRR += result.ReciprocalRank;
                totalPrecision += result.Precision;
                totalRecall += result.Recall;
                totalLatency += result.LatencyMs;

                // 更新进度
                var run = await db.KnowledgeTestRuns.FindAsync(runId);
                if (run != null)
                {
                    run.CompletedCases++;
                    await db.SaveChangesAsync();
                }
            }

            // 计算最终指标
            int n = cases.Count;
            var finalRun = await db.KnowledgeTestRuns.FindAsync(runId);
            if (finalRun != null)
            {
                finalRun.Status = "completed";
                finalRun.HitRate = n > 0 ? (float)hitCount / n : 0;
                finalRun.Mrr = n > 0 ? totalRR / n : 0;
                finalRun.AvgPrecision = n > 0 ? totalPrecision / n : 0;
                finalRun.AvgRecall = n > 0 ? totalRecall / n : 0;
                finalRun.AvgLatencyMs = n > 0 ? totalLatency / n : 0;
                finalRun.DetailResults = JsonSerializer.Serialize(results);
                finalRun.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            _logger.LogInformation("测试运行完成: {RunId}, 命中率: {HitRate}", runId,
                n > 0 ? (float)hitCount / n : 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试运行失败: {RunId}", runId);

            var run = await db.KnowledgeTestRuns.FindAsync(runId);
            if (run != null)
            {
                run.Status = "failed";
                run.ErrorMessage = ex.Message;
                run.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }
    }

    /// <summary>
    /// 执行单个测试用例
    /// </summary>
    private async Task<TestCaseResultDto> ExecuteSingleTestAsync(IUnifiedSearchService searchService,
        KnowledgeTestCase testCase, int topK, float minScore)
    {
        var result = new TestCaseResultDto
        {
            CaseId = testCase.Id,
            CaseName = testCase.Name,
            Query = testCase.Query
        };

        // 解析期望结果
        var expectedDocIds = ParseLongArray(testCase.ExpectedDocumentIds);
        var expectedChunkIds = ParseLongArray(testCase.ExpectedChunkIds);
        var expectedKeywords = ParseStringArray(testCase.ExpectedKeywords);
        result.ExpectedDocIds = expectedDocIds;
        result.ExpectedChunkIds = expectedChunkIds;

        // 执行检索
        var sw = Stopwatch.StartNew();
        var searchOptions = new UnifiedSearchOptions
        {
            TopK = topK,
            MinScore = minScore,
            CategoryId = testCase.CategoryId,
            IncludeKpi = false,  // 测试只关注知识库
            IncludeKnowledge = true
        };
        var searchResults = await searchService.SearchAsync(testCase.Query, searchOptions);
        sw.Stop();
        result.LatencyMs = (float)sw.ElapsedMilliseconds;

        // 提取知识库结果
        var knowledgeResults = searchResults.Where(r => r.Type == "knowledge").ToList();
        int rank = 0;
        int hitRank = 0;
        int relevantCount = 0;

        foreach (var sr in knowledgeResults)
        {
            rank++;
            bool isExpected = false;

            // 检查是否命中期望的文档
            if (expectedDocIds.Count > 0 && expectedDocIds.Contains(sr.SourceId))
            {
                isExpected = true;
            }
            else if (expectedKeywords.Count > 0)
            {
                // 关键词匹配
                isExpected = expectedKeywords.Any(kw => sr.Content.Contains(kw, StringComparison.OrdinalIgnoreCase));
            }

            if (isExpected)
            {
                relevantCount++;
                if (hitRank == 0) hitRank = rank;
            }

            result.RetrievedChunks.Add(new RetrievedChunkDto
            {
                ChunkId = sr.SourceId, // 使用SourceId作为分块标识
                DocumentId = sr.SourceId,
                DocumentTitle = sr.Title,
                ContentPreview = sr.Content.Length > 200 ? sr.Content[..200] + "..." : sr.Content,
                Score = sr.Score,
                Rank = rank,
                IsExpected = isExpected
            });
        }

        // 计算指标
        result.IsHit = hitRank > 0;
        result.ReciprocalRank = hitRank > 0 ? 1.0f / hitRank : 0;
        int knowledgeCount = knowledgeResults.Count;
        result.Precision = knowledgeCount > 0 ? (float)relevantCount / knowledgeCount : 0;

        // 计算召回率（期望结果中被检索到的比例）
        int expectedTotal = Math.Max(expectedChunkIds.Count, expectedDocIds.Count);
        if (expectedTotal == 0 && expectedKeywords.Count > 0) expectedTotal = expectedKeywords.Count;
        result.Recall = expectedTotal > 0 ? (float)relevantCount / expectedTotal : 0;

        return result;
    }

    private List<long> ParseLongArray(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new List<long>();
        try { return JsonSerializer.Deserialize<List<long>>(json) ?? new(); }
        catch { return new List<long>(); }
    }

    private List<string> ParseStringArray(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch { return new List<string>(); }
    }

    #endregion
}


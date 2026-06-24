using System.Text;
using Microsoft.Extensions.Logging;

namespace Bi.Application.Services;

/// <summary>
/// 统一检索服务实现
/// 整合KPI指标检索和知识库文档检索
/// </summary>
public class UnifiedSearchService : IUnifiedSearchService
{
    private readonly IKpiRetrieverService _kpiRetriever;
    private readonly IKnowledgeService _knowledgeService;
    private readonly ILogger<UnifiedSearchService> _logger;

    public UnifiedSearchService(
        IKpiRetrieverService kpiRetriever,
        IKnowledgeService knowledgeService,
        ILogger<UnifiedSearchService> logger)
    {
        _kpiRetriever = kpiRetriever;
        _knowledgeService = knowledgeService;
        _logger = logger;
    }

    /// <summary>
    /// 统一语义检索
    /// </summary>
    public async Task<List<UnifiedSearchResult>> SearchAsync(string query, UnifiedSearchOptions? options = null)
    {
        options ??= new UnifiedSearchOptions();
        var results = new List<UnifiedSearchResult>();

        _logger.LogInformation("统一检索开始: query={Query}, IncludeKpi={IncludeKpi}, IncludeKnowledge={IncludeKnowledge}, TopK={TopK}, MinScore={MinScore}",
            query.Length > 30 ? query[..30] + "..." : query, options.IncludeKpi, options.IncludeKnowledge, options.TopK, options.MinScore);

        // 并行检索KPI和知识库
        var tasks = new List<Task>();
        List<KpiSearchResult>? kpiResults = null;
        List<KnowledgeSearchResult>? knowledgeResults = null;
        Exception? kpiException = null;
        Exception? knowledgeException = null;

        if (options.IncludeKpi)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    kpiResults = await _kpiRetriever.SearchAsync(query, options.TopK, options.MinScore);
                    _logger.LogInformation("KPI检索完成: 返回 {Count} 条结果", kpiResults?.Count ?? 0);
                }
                catch (Exception ex)
                {
                    kpiException = ex;
                    _logger.LogError(ex, "KPI检索异常");
                }
            }));
        }

        if (options.IncludeKnowledge)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    knowledgeResults = await _knowledgeService.SearchAsync(
                        query, options.TopK, options.CategoryId, options.DatasourceId, options.MinScore);
                    _logger.LogInformation("知识库检索完成: 返回 {Count} 条结果", knowledgeResults?.Count ?? 0);
                }
                catch (Exception ex)
                {
                    knowledgeException = ex;
                    _logger.LogError(ex, "知识库检索异常");
                }
            }));
        }

        await Task.WhenAll(tasks);

        // 合并KPI结果
        if (kpiResults != null)
        {
            foreach (var kpi in kpiResults)
            {
                results.Add(new UnifiedSearchResult
                {
                    Type = "kpi",
                    SourceId = kpi.Kpi.Id,
                    Title = kpi.Kpi.Name,
                    Content = kpi.Kpi.Definition ?? string.Empty,
                    Score = (float)kpi.Score,
                    Metadata = new Dictionary<string, object>
                    {
                        ["categoryName"] = kpi.Kpi.Category?.Name ?? string.Empty,
                        ["sqlTemplate"] = kpi.Kpi.SqlTemplate ?? string.Empty,
                        ["unit"] = kpi.Kpi.Unit ?? string.Empty
                    }
                });
            }
        }

        // 合并知识库结果
        if (knowledgeResults != null)
        {
            foreach (var doc in knowledgeResults)
            {
                results.Add(new UnifiedSearchResult
                {
                    Type = "knowledge",
                    SourceId = doc.ChunkId,
                    Title = doc.DocumentTitle,
                    Content = doc.Content,
                    Score = doc.Score,
                    Metadata = new Dictionary<string, object>
                    {
                        ["documentId"] = doc.DocumentId,
                        ["pageNumber"] = doc.PageNumber ?? 0,
                        ["sectionTitle"] = doc.SectionTitle ?? string.Empty
                    }
                });
            }
        }

        // 按分数排序并取TopK
        return results
            .OrderByDescending(r => r.Score)
            .Take(options.TopK)
            .ToList();
    }

    /// <summary>
    /// 获取RAG上下文
    /// </summary>
    /// <param name="query">查询文本</param>
    /// <param name="datasourceId">数据源ID（可选）</param>
    /// <param name="topK">返回的最大结果数</param>
    /// <param name="minScore">最低相似度阈值</param>
    public async Task<string> GetRagContextAsync(string query, long? datasourceId = null, int topK = 5, float minScore = 0.5f)
    {
        _logger.LogInformation("GetRagContextAsync开始: query长度={QueryLength}, datasourceId={DatasourceId}, topK={TopK}, minScore={MinScore}",
            query.Length, datasourceId, topK, minScore);

        var options = new UnifiedSearchOptions
        {
            TopK = topK,
            MinScore = minScore,
            IncludeKpi = true,
            IncludeKnowledge = true,
            DatasourceId = datasourceId
        };

        var results = await SearchAsync(query, options);

        _logger.LogInformation("GetRagContextAsync检索完成: 共 {Count} 条结果", results.Count);

        if (results.Count == 0)
        {
            _logger.LogInformation("RAG检索无结果，返回空上下文");
            return string.Empty;
        }

        var context = new StringBuilder();
        context.AppendLine("【相关知识参考】");
        context.AppendLine();

        // 分类展示KPI和知识库内容
        var kpiResults = results.Where(r => r.Type == "kpi").ToList();
        var knowledgeResults = results.Where(r => r.Type == "knowledge").ToList();

        if (kpiResults.Count > 0)
        {
            context.AppendLine("## 相关指标定义：");
            foreach (var kpi in kpiResults)
            {
                context.AppendLine($"- **{kpi.Title}**：{kpi.Content}");
                if (kpi.Metadata?.TryGetValue("sqlTemplate", out var sql) == true && !string.IsNullOrEmpty(sql?.ToString()))
                {
                    context.AppendLine($"  SQL参考：{sql}");
                }
            }
            context.AppendLine();
        }

        if (knowledgeResults.Count > 0)
        {
            context.AppendLine("## 相关文档内容：");
            foreach (var doc in knowledgeResults)
            {
                context.AppendLine($"- 来自《{doc.Title}》：{doc.Content}");
            }
        }

        var contextStr = context.ToString();
        _logger.LogInformation("RAG上下文生成完成: KPI数={KpiCount}, 知识库数={DocCount}, 上下文长度={Length}",
            kpiResults.Count, knowledgeResults.Count, contextStr.Length);

        return contextStr;
    }
}


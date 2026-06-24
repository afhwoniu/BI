using Bi.Domain.Entities;

namespace Bi.Application.Services;

/// <summary>
/// 知识库测试服务接口
/// </summary>
public interface IKnowledgeTestService
{
    // 测试用例管理
    Task<List<KnowledgeTestCase>> GetTestCasesAsync();
    Task<KnowledgeTestCase?> GetTestCaseAsync(long id);
    Task<KnowledgeTestCase> CreateTestCaseAsync(KnowledgeTestCase testCase);
    Task<KnowledgeTestCase> UpdateTestCaseAsync(KnowledgeTestCase testCase);
    Task DeleteTestCaseAsync(long id);

    // 测试运行管理
    Task<List<KnowledgeTestRun>> GetTestRunsAsync(int page = 1, int pageSize = 20);
    Task<KnowledgeTestRun?> GetTestRunAsync(long id);
    Task<KnowledgeTestRun> StartTestRunAsync(string? name, int topK = 5, float minScore = 0.5f);
    Task DeleteTestRunAsync(long id);

    // 获取测试报告
    Task<TestReportDto> GetTestReportAsync(long runId);
}

/// <summary>
/// 测试报告DTO
/// </summary>
public class TestReportDto
{
    /// <summary>
    /// 运行ID
    /// </summary>
    public long RunId { get; set; }

    /// <summary>
    /// 运行名称
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 测试用例总数
    /// </summary>
    public int TotalCases { get; set; }

    /// <summary>
    /// 已完成用例数
    /// </summary>
    public int CompletedCases { get; set; }

    /// <summary>
    /// TopK参数
    /// </summary>
    public int TopK { get; set; }

    /// <summary>
    /// 最小相似度阈值
    /// </summary>
    public float MinScore { get; set; }

    /// <summary>
    /// 命中率
    /// </summary>
    public float HitRate { get; set; }

    /// <summary>
    /// MRR
    /// </summary>
    public float Mrr { get; set; }

    /// <summary>
    /// 平均精确率
    /// </summary>
    public float AvgPrecision { get; set; }

    /// <summary>
    /// 平均召回率
    /// </summary>
    public float AvgRecall { get; set; }

    /// <summary>
    /// 平均延迟（毫秒）
    /// </summary>
    public float AvgLatencyMs { get; set; }

    /// <summary>
    /// 详细结果
    /// </summary>
    public List<TestCaseResultDto> Details { get; set; } = new();

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// 单个用例测试结果
/// </summary>
public class TestCaseResultDto
{
    public long CaseId { get; set; }
    public string CaseName { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public bool IsHit { get; set; }
    public float ReciprocalRank { get; set; }
    public float Precision { get; set; }
    public float Recall { get; set; }
    public float LatencyMs { get; set; }
    public List<RetrievedChunkDto> RetrievedChunks { get; set; } = new();
    public List<long> ExpectedDocIds { get; set; } = new();
    public List<long> ExpectedChunkIds { get; set; } = new();
}

/// <summary>
/// 检索到的分块
/// </summary>
public class RetrievedChunkDto
{
    public long ChunkId { get; set; }
    public long DocumentId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string ContentPreview { get; set; } = string.Empty;
    public float Score { get; set; }
    public int Rank { get; set; }
    public bool IsExpected { get; set; }
}


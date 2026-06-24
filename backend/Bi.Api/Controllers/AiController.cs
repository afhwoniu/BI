using System.Data.Common;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bi.Api.Models;
using Bi.Api.Services;
using Bi.Application.Services;
using Bi.Domain.Entities;
using Bi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Npgsql;

namespace Bi.Api.Controllers;

/// <summary>
/// AI智能分析控制器
/// </summary>
[ApiController]
[Route("api/v1/ai")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly BiDbContext _db;
    private readonly ISchemaService _schemaService;
    private readonly ILlmService _llmService;
    private readonly IKpiRetrieverService _kpiRetriever;
    private readonly IUnifiedSearchService _unifiedSearch;
    private readonly IConfigService _configService;
    private readonly IAsrService _asrService;
    private readonly PptGeneratorService _pptGenerator;
    private readonly ILogger<AiController> _logger;

    public AiController(
        BiDbContext db,
        ISchemaService schemaService,
        ILlmService llmService,
        IKpiRetrieverService kpiRetriever,
        IUnifiedSearchService unifiedSearch,
        IConfigService configService,
        IAsrService asrService,
        PptGeneratorService pptGenerator,
        ILogger<AiController> logger)
    {
        _db = db;
        _schemaService = schemaService;
        _llmService = llmService;
        _kpiRetriever = kpiRetriever;
        _unifiedSearch = unifiedSearch;
        _configService = configService;
        _asrService = asrService;
        _pptGenerator = pptGenerator;
        _logger = logger;
    }
    
    // 表数量阈值，超过此值使用两阶段查询
    private const int TABLE_THRESHOLD = 30;
    // 两阶段查询时每阶段最大选择的表数量
    private const int MAX_TABLES_PER_PHASE = 15;

    #region ASR语音识别接口

    /// <summary>
    /// 获取ASR配置 - 前端用于判断是否显示语音按钮
    /// </summary>
    [HttpGet("asr/config")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AsrConfig>>> GetAsrConfig()
    {
        try
        {
            var config = await _asrService.GetConfigAsync();
            return Ok(ApiResponse<AsrConfig>.Success(config));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取ASR配置失败");
            return Ok(ApiResponse<AsrConfig>.Fail($"获取配置失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 语音转文字 - 上传音频文件进行识别
    /// </summary>
    /// <param name="audio">音频文件</param>
    /// <returns>识别结果</returns>
    [HttpPost("asr/transcribe")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AsrResult>>> Transcribe(IFormFile audio)
    {
        if (audio == null || audio.Length == 0)
        {
            return Ok(ApiResponse<AsrResult>.Fail("请上传音频文件"));
        }

        try
        {
            // 检查ASR是否启用
            if (!await _asrService.IsEnabledAsync())
            {
                return Ok(ApiResponse<AsrResult>.Fail("语音识别功能未启用，请在系统配置中开启"));
            }

            // 获取音频格式（从文件扩展名）
            var format = Path.GetExtension(audio.FileName)?.TrimStart('.') ?? "wav";

            // 读取音频数据
            using var ms = new MemoryStream();
            await audio.CopyToAsync(ms);
            var audioData = ms.ToArray();

            _logger.LogInformation("接收到语音识别请求，文件: {FileName}, 大小: {Size}KB",
                audio.FileName, audioData.Length / 1024);

            // 调用ASR服务
            var result = await _asrService.TranscribeAsync(audioData, format);

            if (result.Success)
            {
                return Ok(ApiResponse<AsrResult>.Success(result));
            }
            else
            {
                return Ok(ApiResponse<AsrResult>.Fail(result.Error ?? "识别失败"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "语音识别异常");
            return Ok(ApiResponse<AsrResult>.Fail($"语音识别异常: {ex.Message}"));
        }
    }

    #endregion

    /// <summary>
    /// 测试RAG检索 - 调试用
    /// </summary>
    [HttpPost("test-rag")]
    public async Task<ActionResult<ApiResponse<object>>> TestRag([FromBody] TestRagRequest request)
    {
        try
        {
            // 获取RAG配置
            var ragEnabledStr = await _configService.GetAsync(ConfigKeys.RagEnabled, "true");
            var topKStr = await _configService.GetAsync(ConfigKeys.RagTopK, "4");
            var minScoreStr = await _configService.GetAsync(ConfigKeys.RagMinScore, "0.6");
            var topK = int.TryParse(topKStr, out var k) ? k : 4;
            var minScore = float.TryParse(minScoreStr, out var s) ? s : 0.6f;

            // 覆盖请求参数
            if (request.TopK.HasValue) topK = request.TopK.Value;
            if (request.MinScore.HasValue) minScore = request.MinScore.Value;

            var ragContext = await _unifiedSearch.GetRagContextAsync(
                request.Query,
                request.DatasourceId,
                topK,
                minScore);

            return Ok(ApiResponse<object>.Success(new
            {
                RagEnabled = ragEnabledStr,
                TopK = topK,
                MinScore = minScore,
                Query = request.Query,
                DatasourceId = request.DatasourceId,
                ContextLength = ragContext?.Length ?? 0,
                Context = ragContext
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试RAG失败");
            return Ok(ApiResponse<object>.Fail($"测试失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// AI对话接口 - 根据自然语言生成SQL并执行
    /// 支持三种模式：bi(指标统计)、hz360(患者360)、internetsearch(通用问答)
    /// </summary>
    [HttpPost("chat")]
    public async Task<ActionResult<ApiResponse<AiChatResponse>>> Chat([FromBody] AiChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return Ok(ApiResponse<AiChatResponse>.Fail("请输入问题"));

        if (request.DatasourceId <= 0)
            return Ok(ApiResponse<AiChatResponse>.Fail("请选择数据源"));

        try
        {
            // ★ 第零步：模式分类
            var modeResult = await ClassifyModeAsync(request.Question);
            _logger.LogInformation("问题模式分类: {Mode}, 理由: {Reason}", modeResult.Mode, modeResult.Reason);

            // 根据模式分发处理
            if (modeResult.Mode == "hz360")
            {
                // 患者360模式
                return await HandleHz360ModeAsync(request, modeResult);
            }
            else if (modeResult.Mode == "internetsearch")
            {
                // 通用问答模式
                return await HandleInternetSearchModeAsync(request, modeResult);
            }
            else if (modeResult.Mode == "report")
            {
                // 智能报表模式
                return await HandleReportModeAsync(request, modeResult);
            }

            // 以下是bi模式（指标统计）的处理逻辑

            // 用于收集提示词信息
            var promptInfos = new List<PromptInfo>();

            var datasource = await _db.Datasources.FindAsync(request.DatasourceId);
            if (datasource == null)
                return Ok(ApiResponse<AiChatResponse>.Fail("数据源不存在"));

            // ★ 第一步：先获取RAG上下文（在表选择之前，不限定数据源以获取通用业务知识）
            var ragContext = await BuildRagContextAsync(request.Question);
            _logger.LogInformation("RAG检索完成，上下文长度: {Length}", ragContext?.Length ?? 0);

            // 获取所有表
            var allTables = await _schemaService.GetTablesAsync(request.DatasourceId);

            // 确定要使用的表（用户指定 > 两阶段选择 > 全部）
            List<string> tablesToUse;
            if (request.TableNames != null && request.TableNames.Count > 0)
            {
                // 用户已指定表
                tablesToUse = request.TableNames;
            }
            else if (allTables.Count > TABLE_THRESHOLD)
            {
                // ★ 表太多，启用两阶段查询（传入RAG上下文辅助表选择）
                _logger.LogInformation("数据源有 {Count} 张表，启用两阶段查询策略", allTables.Count);
                var (selectedTables, tableSelectPrompt, tableSelectResponse) = await SelectRelevantTablesWithPromptAsync(
                    allTables, request.Question, datasource.Type, ragContext);
                tablesToUse = selectedTables;

                // 记录第一阶段提示词
                promptInfos.Add(new PromptInfo
                {
                    Phase = "第一阶段：表选择",
                    Content = tableSelectPrompt,
                    Response = tableSelectResponse
                });

                _logger.LogInformation("第一阶段选择了 {Count} 张相关表: {Tables}",
                    tablesToUse.Count, string.Join(", ", tablesToUse));
            }
            else
            {
                // 表数量合理，使用全部
                tablesToUse = allTables.Select(t => t.Name).ToList();
            }

            // 获取选中表的Schema信息
            var schemaText = await _schemaService.GenerateSchemaTextAsync(request.DatasourceId, tablesToUse);

            // ★ 第二阶段：使用同一个RAG上下文（已在上面获取）
            var kpiContext = ragContext;

            // 获取用户自定义提示词
            var customPrompt = await _configService.GetAsync("ai.customPrompt");

            // 构建Prompt
            var systemPrompt = BuildSystemPrompt(datasource.Type, schemaText, kpiContext, customPrompt);
            var fullPrompt = $"[System]\n{systemPrompt}\n\n[User]\n{request.Question}";
            var messages = new List<LlmMessage>
            {
                LlmMessage.System(systemPrompt),
                LlmMessage.User(request.Question)
            };

            // 调用LLM（使用BI业务配置）
            var llmResponse = await _llmService.ChatAsync(messages, new LlmOptions
            {
                Temperature = 0.3,  // 低温度以获得更确定的SQL
                MaxTokens = 2048,
                BusinessType = AiBusinessType.Bi
            });

            // 记录第二阶段提示词
            promptInfos.Add(new PromptInfo
            {
                Phase = "第二阶段：SQL生成",
                Content = fullPrompt,
                Response = llmResponse.Content
            });

            if (!llmResponse.Success)
            {
                return Ok(ApiResponse<AiChatResponse>.Fail($"AI服务错误: {llmResponse.Error}"));
            }

            // 解析响应
            var response = ParseLlmResponse(llmResponse.Content);
            response.SessionId = request.SessionId ?? Guid.NewGuid().ToString("N");
            response.TokensUsed = llmResponse.TotalTokens;
            response.Prompts = promptInfos;

            // ★ SQL字段验证和自动修正机制
            var sqlValidationEnabled = await _configService.GetAsync(ConfigKeys.BizBiSqlValidationEnabled, "true");
            if (sqlValidationEnabled == "true")
            {
                var maxRetryStr = await _configService.GetAsync(ConfigKeys.BizBiSqlValidationMaxRetry, "2");
                var maxRetry = int.TryParse(maxRetryStr, out var r) ? r : 2;
                var currentRetry = 0;
                var currentResponse = response;
                var currentLlmResponseContent = llmResponse.Content;

                while (currentRetry < maxRetry)
                {
                    // 验证SQL字段
                    var (isValid, invalidFields) = await ValidateSqlFieldsAsync(currentResponse, request.DatasourceId, tablesToUse);

                    if (isValid)
                    {
                        _logger.LogInformation("SQL字段验证通过");
                        break;
                    }

                    currentRetry++;
                    _logger.LogWarning("SQL字段验证失败（第{Retry}次），不存在的字段: {Fields}",
                        currentRetry, string.Join(", ", invalidFields));

                    // 构建修正提示
                    var correctionPrompt = BuildSqlCorrectionPrompt(
                        request.Question,
                        currentLlmResponseContent,
                        invalidFields,
                        schemaText);

                    // 重新调用AI修正SQL
                    var correctionMessages = new List<LlmMessage>
                    {
                        LlmMessage.System(systemPrompt),
                        LlmMessage.User(correctionPrompt)
                    };

                    var correctionLlmResponse = await _llmService.ChatAsync(correctionMessages, new LlmOptions
                    {
                        Temperature = 0.1,  // 更低温度以获得更确定的结果
                        MaxTokens = 2048,
                        BusinessType = AiBusinessType.Bi
                    });

                    if (!correctionLlmResponse.Success)
                    {
                        _logger.LogError("AI修正SQL失败: {Error}", correctionLlmResponse.Error);
                        break;
                    }

                    // 记录修正提示词
                    promptInfos.Add(new PromptInfo
                    {
                        Phase = $"SQL字段修正（第{currentRetry}次）",
                        Content = correctionPrompt,
                        Response = correctionLlmResponse.Content
                    });

                    // 解析修正后的响应
                    currentResponse = ParseLlmResponse(correctionLlmResponse.Content);
                    currentLlmResponseContent = correctionLlmResponse.Content;
                    response.TokensUsed += correctionLlmResponse.TotalTokens;
                }

                // 使用最终的响应
                currentResponse.SessionId = response.SessionId;
                currentResponse.TokensUsed = response.TokensUsed;
                currentResponse.Prompts = promptInfos;
                response = currentResponse;
            }

            // ★ 解析问题中的时间范围，用于替换SQL中的时间参数
            var (parsedStartDate, parsedEndDate) = ParseDateRangeFromQuestion(request.Question);
            if (parsedStartDate.HasValue && parsedEndDate.HasValue)
            {
                _logger.LogInformation("解析到时间范围: {Start} 至 {End}", parsedStartDate.Value.ToString("yyyy-MM-dd"), parsedEndDate.Value.ToString("yyyy-MM-dd"));

                // 替换DetailSql中的时间参数
                if (!string.IsNullOrEmpty(response.DetailSql))
                {
                    response.DetailSql = ReplaceDateParameters(response.DetailSql, parsedStartDate.Value, parsedEndDate.Value);
                }

                // 替换Queries中每个SQL的时间参数
                if (response.Queries != null)
                {
                    foreach (var query in response.Queries)
                    {
                        if (!string.IsNullOrEmpty(query.Sql))
                        {
                            query.Sql = ReplaceDateParameters(query.Sql, parsedStartDate.Value, parsedEndDate.Value);
                        }
                    }
                }

                // 替换单SQL模式的时间参数
                if (!string.IsNullOrEmpty(response.Sql))
                {
                    response.Sql = ReplaceDateParameters(response.Sql, parsedStartDate.Value, parsedEndDate.Value);
                }
            }

            // ★ 第二关：SQL语法预验证 + AI自动修正
            // 在字段验证通过、日期参数替换后，试执行SQL检查语法
            if (sqlValidationEnabled == "true")
            {
                var syntaxMaxRetryStr = await _configService.GetAsync(ConfigKeys.BizBiSqlValidationMaxRetry, "2");
                var syntaxMaxRetry = int.TryParse(syntaxMaxRetryStr, out var sr) ? sr : 2;
                var syntaxRetry = 0;

                while (syntaxRetry < syntaxMaxRetry)
                {
                    // 试执行detailSql和所有KPI/Chart SQL，收集语法错误
                    var syntaxErrors = await PreValidateSqlSyntaxAsync(response, datasource.Type, datasource.ConnString);

                    if (syntaxErrors.Count == 0)
                    {
                        _logger.LogInformation("SQL语法预验证通过");
                        break;
                    }

                    syntaxRetry++;
                    _logger.LogWarning("SQL语法预验证失败（第{Retry}次），发现 {Count} 个语法错误",
                        syntaxRetry, syntaxErrors.Count);

                    // 构建语法修正提示
                    var syntaxCorrectionPrompt = BuildSqlSyntaxCorrectionPrompt(
                        request.Question,
                        response,
                        syntaxErrors,
                        schemaText,
                        datasource.Type);

                    // 调用AI修正SQL语法
                    var syntaxCorrectionMessages = new List<LlmMessage>
                    {
                        LlmMessage.System(systemPrompt),
                        LlmMessage.User(syntaxCorrectionPrompt)
                    };

                    var syntaxCorrectionResponse = await _llmService.ChatAsync(syntaxCorrectionMessages, new LlmOptions
                    {
                        Temperature = 0.1,
                        MaxTokens = 4096,
                        BusinessType = AiBusinessType.Bi
                    });

                    if (!syntaxCorrectionResponse.Success)
                    {
                        _logger.LogError("AI修正SQL语法失败: {Error}", syntaxCorrectionResponse.Error);
                        break;
                    }

                    // 记录修正提示词
                    promptInfos.Add(new PromptInfo
                    {
                        Phase = $"SQL语法修正（第{syntaxRetry}次）",
                        Content = syntaxCorrectionPrompt,
                        Response = syntaxCorrectionResponse.Content
                    });

                    // 解析修正后的响应
                    var correctedResponse = ParseLlmResponse(syntaxCorrectionResponse.Content);
                    correctedResponse.SessionId = response.SessionId;
                    correctedResponse.TokensUsed = response.TokensUsed + syntaxCorrectionResponse.TotalTokens;
                    correctedResponse.Prompts = promptInfos;

                    // 重新替换日期参数
                    if (parsedStartDate.HasValue && parsedEndDate.HasValue)
                    {
                        if (!string.IsNullOrEmpty(correctedResponse.DetailSql))
                            correctedResponse.DetailSql = ReplaceDateParameters(correctedResponse.DetailSql, parsedStartDate.Value, parsedEndDate.Value);
                        if (correctedResponse.Queries != null)
                        {
                            foreach (var q in correctedResponse.Queries)
                            {
                                if (!string.IsNullOrEmpty(q.Sql))
                                    q.Sql = ReplaceDateParameters(q.Sql, parsedStartDate.Value, parsedEndDate.Value);
                            }
                        }
                        if (!string.IsNullOrEmpty(correctedResponse.Sql))
                            correctedResponse.Sql = ReplaceDateParameters(correctedResponse.Sql, parsedStartDate.Value, parsedEndDate.Value);
                    }

                    response = correctedResponse;
                }
            }

            // ★ 性能优化：多查询模式并行执行SQL
            if (response.Queries != null && response.Queries.Count > 0)
            {
                // 先进行安全检查，标记不安全的SQL
                foreach (var query in response.Queries)
                {
                    if (string.IsNullOrEmpty(query.Sql)) continue;
                    if (!IsSafeSelectSql(query.Sql))
                    {
                        query.Error = "SQL不安全，已拒绝执行";
                    }
                }

                // 并行执行所有安全的SQL查询
                var safeQueries = response.Queries.Where(q => !string.IsNullOrEmpty(q.Sql) && string.IsNullOrEmpty(q.Error)).ToList();
                if (safeQueries.Count > 0)
                {
                    var queryTasks = safeQueries.Select(async query =>
                    {
                        try
                        {
                            query.Data = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, query.Sql!);
                        }
                        catch (Exception ex)
                        {
                            query.Error = $"执行失败: {ex.Message}";
                        }
                    });
                    await Task.WhenAll(queryTasks);
                    _logger.LogInformation("并行执行 {Count} 个SQL查询完成", safeQueries.Count);
                }
            }
            // 兼容旧版单SQL模式
            else if (!string.IsNullOrEmpty(response.Sql))
            {
                if (!IsSafeSelectSql(response.Sql))
                {
                    response.Error = "生成的SQL不安全，已拒绝执行";
                    response.Data = null;
                }
                else
                {
                    var data = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, response.Sql);
                    response.Data = data;
                }
            }

            // ★ 如果没有 hospitalField，尝试从 dimensions 中自动识别医院相关字段
            _logger.LogInformation("检查医院字段：HospitalField={HospitalField}, Dimensions={Dimensions}, DetailSql长度={DetailSqlLen}",
                response.HospitalField ?? "null",
                response.Dimensions != null ? string.Join(",", response.Dimensions) : "null",
                response.DetailSql?.Length ?? 0);

            if (string.IsNullOrEmpty(response.HospitalField) && response.Dimensions?.Any() == true)
            {
                var hospitalPatterns = new[] { "医院", "机构", "院区", "hospital", "org", "institution" };
                foreach (var dim in response.Dimensions)
                {
                    var lowerDim = dim.ToLower();
                    if (hospitalPatterns.Any(p => lowerDim.Contains(p)))
                    {
                        response.HospitalField = dim;
                        _logger.LogInformation("自动识别医院字段: {Field}（从维度 {Dimensions} 中）", dim, string.Join(",", response.Dimensions));
                        break;
                    }
                }
            }

            // 如果有医院字段，获取医院列表
            _logger.LogInformation("准备获取医院列表：HospitalField={HospitalField}, DetailSql有值={HasDetailSql}",
                response.HospitalField ?? "null",
                !string.IsNullOrEmpty(response.DetailSql));

            if (!string.IsNullOrEmpty(response.DetailSql) && !string.IsNullOrEmpty(response.HospitalField))
            {
                try
                {
                    var hospitalSql = $"SELECT DISTINCT {response.HospitalField} FROM ({response.DetailSql}) t WHERE {response.HospitalField} IS NOT NULL ORDER BY {response.HospitalField} LIMIT 100";
                    var hospitalData = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, hospitalSql);
                    response.Hospitals = hospitalData
                        .Select(row => row.Values.FirstOrDefault()?.ToString() ?? "")
                        .Where(h => !string.IsNullOrEmpty(h))
                        .ToList();
                    _logger.LogInformation("获取医院列表成功，共 {Count} 家医院", response.Hospitals.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "获取医院列表失败");
                }
            }

            // 设置响应的模式为bi
            response.Mode = "bi";
            response.ModeReason = modeResult.Reason;

            // 保存会话和消息到数据库（必须同步等待，因为前端需要真实的messageId来查询明细）
            try
            {
                var sessionKey = response.SessionId;
                var session = await _db.AiSessions.FirstOrDefaultAsync(s => s.SessionKey == sessionKey);

                if (session == null)
                {
                    // 创建新会话（标题去掉时间范围后缀）
                    session = new Domain.Entities.AiSession
                    {
                        SessionKey = sessionKey,
                        Title = ExtractCleanTitle(request.Question),
                        Mode = "bi",  // 设置会话模式
                        DatasourceId = request.DatasourceId,
                        UserId = 1,  // TODO: 从当前用户获取
                        CreatedAt = DateTime.UtcNow,
                        LastActiveAt = DateTime.UtcNow
                    };
                    _db.AiSessions.Add(session);
                    await _db.SaveChangesAsync();
                }
                else
                {
                    session.LastActiveAt = DateTime.UtcNow;
                }

                // 保存用户消息
                var userMessage = new Domain.Entities.AiMessage
                {
                    SessionId = session.Id,
                    Role = "user",
                    Mode = "bi",  // 设置消息模式
                    Content = request.Question,
                    CreatedAt = DateTime.UtcNow
                };
                _db.AiMessages.Add(userMessage);

                // 保存AI回复消息（包含明细SQL和维度配置）
                var aiMessage = new Domain.Entities.AiMessage
                {
                    SessionId = session.Id,
                    Role = "assistant",
                    Mode = "bi",  // 设置消息模式
                    Content = response.Answer ?? "",
                    Sql = response.Sql,
                    DetailSql = response.DetailSql,
                    HospitalField = response.HospitalField,
                    DateField = response.DateField,  // 保存日期字段（用于同比环比和时间参数替换）
                    DimensionFields = response.Dimensions != null ? System.Text.Json.JsonSerializer.Serialize(response.Dimensions) : null,
                    MeasureFields = response.Measures != null ? System.Text.Json.JsonSerializer.Serialize(response.Measures) : null,
                    DefaultChartsConfig = response.DefaultChartsConfig != null ? System.Text.Json.JsonSerializer.Serialize(response.DefaultChartsConfig) : null,
                    KpiConfig = response.KpiConfigs != null ? System.Text.Json.JsonSerializer.Serialize(response.KpiConfigs) : null,  // 保存KPI配置
                    PromptText = fullPrompt,  // 保留旧字段兼容
                    PromptsJson = promptInfos.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(promptInfos) : null,  // 保存完整的分阶段提示词
                    ChartType = response.ChartType,
                    TokensUsed = response.TokensUsed ?? 0,
                    CreatedAt = DateTime.UtcNow
                };
                _db.AiMessages.Add(aiMessage);
                await _db.SaveChangesAsync();

                // 设置消息ID用于后续下钻（必须是真实的数据库ID）
                response.MessageId = aiMessage.Id;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "保存会话失败，但不影响返回结果");
            }

            return Ok(ApiResponse<AiChatResponse>.Success(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI对话错误");
            return Ok(ApiResponse<AiChatResponse>.Fail($"处理失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 知识问答流式API - 使用SSE实时返回AI回答
    /// </summary>
    [HttpPost("chat/stream")]
    public async Task ChatStream([FromBody] AiChatRequest request)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";

        try
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                await WriteSSE("error", "请输入问题");
                return;
            }

            // 先进行模式分类
            var modeResult = await ClassifyModeAsync(request.Question);

            // 发送初始信息
            var initData = new
            {
                sessionId = request.SessionId ?? Guid.NewGuid().ToString("N"),
                mode = modeResult.Mode,
                modeReason = modeResult.Reason
            };
            await WriteSSE("init", System.Text.Json.JsonSerializer.Serialize(initData));

            // 只有internetsearch模式使用流式
            if (modeResult.Mode != "internetsearch")
            {
                await WriteSSE("redirect", modeResult.Mode);
                await WriteSSE("done", "");
                return;
            }

            // 获取RAG上下文
            var ragContext = await BuildRagContextAsync(request.Question);

            var systemPrompt = @"你是一个医疗信息化领域的专家助手。请根据用户的问题提供专业、准确的回答。
如果有相关的知识库内容，请参考这些内容来回答。

## 输出格式要求（必须严格遵守）
1. **必须使用Markdown格式输出，每个段落之间用空行分隔**
2. 使用标题（## 一级、### 二级）分层组织内容，标题独占一行
3. 使用列表时，每个列表项独占一行，格式如：
   - 项目1
   - 项目2
4. 使用数字列表时，格式如：
   1. 第一点
   2. 第二点
5. 重要内容使用**加粗**标记
6. **绝对不要把所有内容写成一长段，必须分段落、分条目**
7. 每个要点之间必须换行";

            if (!string.IsNullOrEmpty(ragContext))
            {
                systemPrompt += $"\n\n## 参考知识\n{ragContext}";
            }

            // 发送提示词信息（供前端"查看提示词"功能使用）
            var promptInfo = new
            {
                phase = "知识问答",
                systemPrompt = systemPrompt,
                userQuestion = request.Question
            };
            await WriteSSE("prompt", System.Text.Json.JsonSerializer.Serialize(promptInfo));

            var messages = new List<LlmMessage>
            {
                LlmMessage.System(systemPrompt),
                LlmMessage.User(request.Question)
            };

            // 流式输出
            var fullContent = new System.Text.StringBuilder();
            await foreach (var chunk in _llmService.ChatStreamAsync(messages, new LlmOptions
            {
                Temperature = 0.7,
                MaxTokens = 2048,
                BusinessType = AiBusinessType.Search
            }))
            {
                if (chunk.StartsWith("[ERROR]"))
                {
                    await WriteSSE("error", chunk);
                    break;
                }
                fullContent.Append(chunk);
                await WriteSSE("content", chunk);
            }

            // 保存会话
            var response = new AiChatResponse
            {
                SessionId = initData.sessionId,
                Mode = "internetsearch",
                ModeReason = modeResult.Reason,
                Answer = fullContent.ToString()
            };
            await SaveSessionAsync(request, response, "internetsearch");

            await WriteSSE("done", "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "流式对话错误");
            await WriteSSE("error", ex.Message);
        }
    }

    /// <summary>
    /// 写入SSE事件
    /// SSE协议要求：如果data包含换行符，需要将每行都用data:前缀发送
    /// 注意：SSE标准要求使用LF(\n)换行，不能使用CRLF(\r\n)
    /// </summary>
    private async Task WriteSSE(string eventType, string data)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"event: {eventType}\n");

        // 如果data包含换行符，需要将每行都用data:前缀发送
        if (data.Contains('\n'))
        {
            // 处理可能的\r\n和\n混合情况
            var lines = data.Replace("\r\n", "\n").Split('\n');
            foreach (var line in lines)
            {
                sb.Append($"data: {line}\n");
            }
        }
        else
        {
            sb.Append($"data: {data}\n");
        }
        sb.Append('\n');  // SSE事件以空行结束

        await Response.WriteAsync(sb.ToString());
        await Response.Body.FlushAsync();
    }

    /// <summary>
    /// 处理患者360模式 - 查询特定患者信息
    /// </summary>
    private async Task<ActionResult<ApiResponse<AiChatResponse>>> HandleHz360ModeAsync(AiChatRequest request, ModeClassifyResult modeResult)
    {
        try
        {
            var response = new AiChatResponse
            {
                SessionId = request.SessionId ?? Guid.NewGuid().ToString("N"),
                Mode = "hz360",
                ModeReason = modeResult.Reason
            };

            // 查询患者360（使用AI生成SQL）
            // 传递用户原始问题，让AI理解并生成正确的SQL
            var (patients, sql, aiPrompt, aiResponse) = await QueryPatient360WithAiAsync(request.Question, request.DatasourceId);
            response.Patients = patients;
            response.Sql = sql;  // 保存执行的SQL，方便前端"查看SQL"按钮显示

            // 将AI提示词和响应添加到Prompts中，方便前端查看
            response.Prompts = new List<PromptInfo>
            {
                new PromptInfo
                {
                    Phase = "患者360查询",
                    Content = aiPrompt,
                    Response = aiResponse
                }
            };

            if (patients.Count > 0)
            {
                response.Answer = $"找到 {patients.Count} 位符合条件的患者，请点击查看详情。";
            }
            else
            {
                response.Answer = $"未找到符合条件的患者。请检查输入的患者姓名、身份证号或就诊号是否正确。\n\n💡 点击右上角\"查看提示词\"可查看AI生成的SQL语句。";
            }

            // 保存会话
            await SaveSessionAsync(request, response, "hz360");

            return Ok(ApiResponse<AiChatResponse>.Success(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "患者360查询错误");
            return Ok(ApiResponse<AiChatResponse>.Fail($"查询失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 处理通用问答模式 - 使用LLM直接回答
    /// </summary>
    private async Task<ActionResult<ApiResponse<AiChatResponse>>> HandleInternetSearchModeAsync(AiChatRequest request, ModeClassifyResult modeResult)
    {
        try
        {
            var promptInfos = new List<PromptInfo>();
            var response = new AiChatResponse
            {
                SessionId = request.SessionId ?? Guid.NewGuid().ToString("N"),
                Mode = "internetsearch",
                ModeReason = modeResult.Reason
            };

            // 获取RAG上下文增强回答
            var ragContext = await BuildRagContextAsync(request.Question);

            var systemPrompt = @"你是一个医疗信息化领域的专家助手。请根据用户的问题提供专业、准确的回答。
如果有相关的知识库内容，请参考这些内容来回答。

## 输出格式要求
1. 使用Markdown格式输出
2. 合理使用段落、换行，让内容结构清晰
3. 使用标题（##、###）分层组织内容
4. 重要内容可以使用**加粗**或列表展示
5. 回答要专业、准确、易于阅读";

            if (!string.IsNullOrEmpty(ragContext))
            {
                systemPrompt += $"\n\n## 参考知识\n{ragContext}";
            }

            var messages = new List<LlmMessage>
            {
                LlmMessage.System(systemPrompt),
                LlmMessage.User(request.Question)
            };

            // 记录提示词信息
            var fullPrompt = $"[System]\n{systemPrompt}\n\n[User]\n{request.Question}";
            promptInfos.Add(new PromptInfo
            {
                Phase = "知识问答",
                Content = fullPrompt,
                Response = ""  // 将在LLM响应后填充
            });

            var llmResponse = await _llmService.ChatAsync(messages, new LlmOptions
            {
                Temperature = 0.7,
                MaxTokens = 2048,
                BusinessType = AiBusinessType.Search  // 通用问答使用检索配置
            });

            if (llmResponse.Success)
            {
                response.Answer = llmResponse.Content;
                response.TokensUsed = llmResponse.TotalTokens;
                // 记录LLM响应
                if (promptInfos.Count > 0)
                {
                    promptInfos[0].Response = llmResponse.Content ?? "";
                }
            }
            else
            {
                response.Answer = "抱歉，暂时无法回答您的问题。请稍后再试。";
                response.Error = llmResponse.Error;
            }

            // 设置提示词信息
            response.Prompts = promptInfos;

            // 保存会话
            await SaveSessionAsync(request, response, "internetsearch");

            return Ok(ApiResponse<AiChatResponse>.Success(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "通用问答错误");
            return Ok(ApiResponse<AiChatResponse>.Fail($"回答失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 处理智能报表模式 - AI生成报表型SQL，以多页签表格形式呈现
    /// </summary>
    private async Task<ActionResult<ApiResponse<AiChatResponse>>> HandleReportModeAsync(AiChatRequest request, ModeClassifyResult modeResult)
    {
        try
        {
            var promptInfos = new List<PromptInfo>();

            var response = new AiChatResponse
            {
                SessionId = request.SessionId ?? Guid.NewGuid().ToString("N"),
                Mode = "report",
                ModeReason = modeResult.Reason
            };

            var datasource = await _db.Datasources.FindAsync(request.DatasourceId);
            if (datasource == null)
                return Ok(ApiResponse<AiChatResponse>.Fail("数据源不存在"));

            // 获取RAG上下文
            var ragContext = await BuildRagContextAsync(request.Question);
            _logger.LogInformation("报表模式：RAG检索完成，上下文长度: {Length}", ragContext?.Length ?? 0);

            // 获取表列表
            var allTables = await _schemaService.GetTablesAsync(request.DatasourceId);

            // 确定要使用的表
            List<string> tablesToUse;
            if (request.TableNames != null && request.TableNames.Count > 0)
            {
                tablesToUse = request.TableNames;
            }
            else if (allTables.Count > TABLE_THRESHOLD)
            {
                _logger.LogInformation("报表模式：数据源有 {Count} 张表，启用两阶段查询策略", allTables.Count);
                var (selectedTables, tableSelectPrompt, tableSelectResponse) = await SelectRelevantTablesWithPromptAsync(
                    allTables, request.Question, datasource.Type, ragContext);
                tablesToUse = selectedTables;

                promptInfos.Add(new PromptInfo
                {
                    Phase = "第一阶段：表选择",
                    Content = tableSelectPrompt,
                    Response = tableSelectResponse
                });
            }
            else
            {
                tablesToUse = allTables.Select(t => t.Name).ToList();
            }

            // 获取Schema
            var schemaText = await _schemaService.GenerateSchemaTextAsync(request.DatasourceId, tablesToUse);

            // 获取自定义提示词
            var customPrompt = await _configService.GetAsync("ai.customPrompt");

            // 构建报表专用Prompt
            var systemPrompt = BuildReportSystemPrompt(datasource.Type, schemaText, ragContext, customPrompt);
            var fullPrompt = $"[System]\n{systemPrompt}\n\n[User]\n{request.Question}";
            var messages = new List<LlmMessage>
            {
                LlmMessage.System(systemPrompt),
                LlmMessage.User(request.Question)
            };

            // 调用LLM
            var llmResponse = await _llmService.ChatAsync(messages, new LlmOptions
            {
                Temperature = 0.3,
                MaxTokens = 4096,
                BusinessType = AiBusinessType.Bi
            });

            promptInfos.Add(new PromptInfo
            {
                Phase = "报表SQL生成",
                Content = fullPrompt,
                Response = llmResponse.Content
            });

            if (!llmResponse.Success)
            {
                return Ok(ApiResponse<AiChatResponse>.Fail($"AI服务错误: {llmResponse.Error}"));
            }

            // 解析AI响应
            var reportData = ParseReportLlmResponse(llmResponse.Content);
            response.Answer = reportData.Answer;
            response.TokensUsed = llmResponse.TotalTokens;
            response.Prompts = promptInfos;

            // 记录解析结果
            if (reportData.Sheets != null)
            {
                foreach (var s in reportData.Sheets)
                {
                    _logger.LogInformation("报表解析结果：页签'{Title}', SQL长度={SqlLen}, 列数={ColCount}, 列定义=[{Cols}]",
                        s.Title,
                        s.Sql?.Length ?? 0,
                        s.Columns?.Count ?? 0,
                        s.Columns != null ? string.Join(", ", s.Columns.Select(c => $"{c.Field}({c.Title})")) : "无");
                }
            }
            else
            {
                _logger.LogWarning("报表解析结果：未获取到sheets数据，AI响应长度={Len}", llmResponse.Content?.Length ?? 0);
            }

            // 解析时间范围
            var (parsedStartDate, parsedEndDate) = ParseDateRangeFromQuestion(request.Question);

            // 执行每个Sheet的SQL
            if (reportData.Sheets != null && reportData.Sheets.Count > 0)
            {
                response.ReportSheets = new List<ReportSheetData>();

                foreach (var sheet in reportData.Sheets)
                {
                    var sheetData = new ReportSheetData
                    {
                        Title = sheet.Title ?? "数据表",
                        Columns = sheet.Columns ?? new List<ReportColumnDef>(),
                        Sql = sheet.Sql
                    };

                    if (!string.IsNullOrEmpty(sheet.Sql))
                    {
                        // 替换时间参数
                        var sql = sheet.Sql;
                        if (parsedStartDate.HasValue && parsedEndDate.HasValue)
                        {
                            sql = ReplaceDateParameters(sql, parsedStartDate.Value, parsedEndDate.Value);
                        }

                        // 报表模式限制最多返回10000行，避免数据量过大导致前端卡死
                        var reportMaxRows = 10000;
                        var limitedSql = sql.TrimEnd(';', ' ', '\n', '\r');
                        if (limitedSql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                        {
                            limitedSql = $"SELECT * FROM ({limitedSql}) t LIMIT {reportMaxRows}";
                        }

                        sheetData.Sql = sql;

                        if (IsSafeSelectSql(limitedSql))
                        {
                            try
                            {
                                sheetData.Rows = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, limitedSql);
                                _logger.LogInformation("报表页签 '{Title}' 查询成功，{Count} 行数据（限制{Max}行）", sheetData.Title, sheetData.Rows.Count, reportMaxRows);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "报表页签 '{Title}' SQL执行失败", sheetData.Title);
                                sheetData.Rows = new List<Dictionary<string, object?>>();
                            }
                        }
                        else
                        {
                            _logger.LogWarning("报表页签 '{Title}' SQL不安全，已拒绝执行", sheetData.Title);
                        }
                    }

                    // 执行合计行SQL
                    if (!string.IsNullOrEmpty(sheet.SummarySql) && sheetData.Rows.Count > 0)
                    {
                        var summarySql = sheet.SummarySql;
                        if (parsedStartDate.HasValue && parsedEndDate.HasValue)
                        {
                            summarySql = ReplaceDateParameters(summarySql, parsedStartDate.Value, parsedEndDate.Value);
                        }

                        if (IsSafeSelectSql(summarySql))
                        {
                            try
                            {
                                var summaryData = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, summarySql);
                                if (summaryData.Count > 0)
                                {
                                    sheetData.SummaryRow = summaryData[0];
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "报表页签 '{Title}' 合计SQL执行失败", sheetData.Title);
                            }
                        }
                    }

                    // 用实际数据行的keys修正列定义，确保field与数据key完全匹配
                    if (sheetData.Rows.Count > 0)
                    {
                        var actualKeys = sheetData.Rows[0].Keys.ToHashSet();
                        // 检查AI返回的列定义是否与实际数据key匹配
                        var fieldMatch = sheetData.Columns.All(c => actualKeys.Contains(c.Field));
                        if (!fieldMatch || sheetData.Columns.Count == 0)
                        {
                            // 不匹配，用实际数据key重建列定义（保留AI的标题和宽度如果field能匹配上）
                            var aiColMap = sheetData.Columns.Where(c => actualKeys.Contains(c.Field)).ToDictionary(c => c.Field);
                            sheetData.Columns = new List<ReportColumnDef>();
                            foreach (var key in actualKeys)
                            {
                                var sampleVal = sheetData.Rows[0][key];
                                var isNumeric = sampleVal != null && IsNumericValue(sampleVal);
                                if (aiColMap.TryGetValue(key, out var aiCol))
                                {
                                    sheetData.Columns.Add(new ReportColumnDef
                                    {
                                        Field = key,
                                        Title = aiCol.Title != key ? aiCol.Title : key,
                                        DataType = aiCol.DataType,
                                        Width = aiCol.Width,
                                        Align = aiCol.Align
                                    });
                                }
                                else
                                {
                                    sheetData.Columns.Add(new ReportColumnDef
                                    {
                                        Field = key,
                                        Title = key,
                                        DataType = isNumeric ? "number" : "text",
                                        Width = isNumeric ? 120 : 150,
                                        Align = isNumeric ? "right" : "left"
                                    });
                                }
                            }
                            _logger.LogInformation("报表页签 '{Title}' 列定义已用实际数据key重建，共{Count}列", sheetData.Title, sheetData.Columns.Count);
                        }
                    }

                    response.ReportSheets.Add(sheetData);
                }
            }

            // 保存会话
            try
            {
                var sessionKey = response.SessionId;
                var session = await _db.AiSessions.FirstOrDefaultAsync(s => s.SessionKey == sessionKey);

                if (session == null)
                {
                    session = new Domain.Entities.AiSession
                    {
                        SessionKey = sessionKey,
                        Title = ExtractCleanTitle(request.Question),
                        Mode = "report",
                        DatasourceId = request.DatasourceId,
                        UserId = 1,
                        CreatedAt = DateTime.UtcNow,
                        LastActiveAt = DateTime.UtcNow
                    };
                    _db.AiSessions.Add(session);
                    await _db.SaveChangesAsync();
                }
                else
                {
                    session.LastActiveAt = DateTime.UtcNow;
                }

                var userMessage = new Domain.Entities.AiMessage
                {
                    SessionId = session.Id,
                    Role = "user",
                    Mode = "report",
                    Content = request.Question,
                    CreatedAt = DateTime.UtcNow
                };
                _db.AiMessages.Add(userMessage);

                var aiMessage = new Domain.Entities.AiMessage
                {
                    SessionId = session.Id,
                    Role = "assistant",
                    Mode = "report",
                    Content = response.Answer ?? "",
                    Sql = response.ReportSheets?.FirstOrDefault()?.Sql,
                    PromptsJson = promptInfos.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(promptInfos) : null,
                    TokensUsed = response.TokensUsed ?? 0,
                    CreatedAt = DateTime.UtcNow
                };
                _db.AiMessages.Add(aiMessage);
                await _db.SaveChangesAsync();

                response.MessageId = aiMessage.Id;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "保存报表会话失败，但不影响返回结果");
            }

            return Ok(ApiResponse<AiChatResponse>.Success(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "智能报表生成错误");
            return Ok(ApiResponse<AiChatResponse>.Fail($"报表生成失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 构建报表模式专用的系统提示词
    /// </summary>
    private static string BuildReportSystemPrompt(string dbType, string schemaText, string? kpiContext = null, string? customPrompt = null)
    {
        var dbHint = dbType.ToLower() switch
        {
            "postgres" or "postgresql" => "PostgreSQL",
            "sqlserver" or "mssql" => "SQL Server",
            "mysql" => "MySQL",
            "doris" => "Apache Doris",
            _ => "SQL"
        };

        var sb = new StringBuilder();

        // 字段使用规则
        sb.AppendLine("# 字段使用规则");
        sb.AppendLine();
        sb.AppendLine("1. **只能使用下面「可用字段清单」中列出的字段名**，字段名必须完全一致（区分大小写）");
        sb.AppendLine("2. **积极匹配**：用户描述的概念可能与字段名不完全一致，请仔细查找语义相近的字段");
        sb.AppendLine("3. **尽力完成查询**：即使部分条件无法满足，也要用现有字段生成有意义的SQL");
        sb.AppendLine();
        sb.AppendLine("禁止的行为：");
        sb.AppendLine("- 臆造不存在的字段名");
        sb.AppendLine("- 轻易放弃（应先尝试用现有字段完成查询）");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        sb.AppendLine($"你是{dbHint}数据分析专家，用户需要生成一张结构化的报表。请根据需求生成合适的SQL查询，并定义报表的列格式。");
        sb.AppendLine();

        // 当前时间
        var now = DateTime.Now;
        sb.AppendLine("## 当前时间");
        sb.AppendLine($"- {now:yyyy年MM月dd日} | 本月：{now.Month}月 | 上月：{now.AddMonths(-1):yyyy年MM月}");
        sb.AppendLine();

        // Schema
        sb.AppendLine("## 可用字段清单（只能使用以下字段！）");
        sb.AppendLine(schemaText);

        // RAG增强
        if (!string.IsNullOrEmpty(kpiContext))
        {
            sb.AppendLine();
            sb.AppendLine("## 业务知识参考");
            sb.AppendLine(kpiContext);
        }

        sb.AppendLine();
        sb.AppendLine("## 输出格式（严格按此JSON格式返回，不要返回其他内容）");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"answer\": \"对报表内容的简要说明\",");
        sb.AppendLine("  \"sheets\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"title\": \"页签标题\",");
        sb.AppendLine("      \"sql\": \"SELECT 字段1 AS 别名1, 字段2 AS 别名2, ... FROM 表名 WHERE 条件 ORDER BY 字段\",");
        sb.AppendLine("      \"summarySql\": \"SELECT SUM(数值列1) AS 别名1, SUM(数值列2) AS 别名2, ... FROM (上面sql) t\",");
        sb.AppendLine("      \"columns\": [");
        sb.AppendLine("        {\"field\": \"别名1\", \"title\": \"显示标题\", \"dataType\": \"text\", \"width\": 150, \"align\": \"left\"},");
        sb.AppendLine("        {\"field\": \"别名2\", \"title\": \"显示标题\", \"dataType\": \"number\", \"width\": 120, \"align\": \"right\"}");
        sb.AppendLine("      ]");
        sb.AppendLine("    }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## 报表生成规则");
        sb.AppendLine("1. **sheets**: 报表可以有1~3个页签，每个页签是一组独立的数据表格");
        sb.AppendLine("   - 如果用户只要求一种报表，返回1个页签即可");
        sb.AppendLine("   - 如果用户的需求可以拆分为不同维度（如按日+按科室），可返回多个页签");
        sb.AppendLine("2. **sql**: 报表数据查询SQL，注意：");
        sb.AppendLine("   - 时间条件必须使用 @startDate 和 @endDate 占位符");
        sb.AppendLine("   - 必须给每个字段起中文别名（AS），别名要简短有意义");
        sb.AppendLine("   - SQL应返回**分组聚合后的多行多列**数据，而不是逐条明细记录");
        sb.AppendLine("   - 必须使用 GROUP BY 实现分组汇总，典型写法：SELECT 维度1, 维度2, SUM(度量1), COUNT(*) ... GROUP BY 维度1, 维度2");
        sb.AppendLine("   - 不要直接 SELECT * 或 SELECT 患者姓名,身份证号 等明细字段，这样会产生海量数据");
        sb.AppendLine("   - 使用 ORDER BY 保证数据有序");
        sb.AppendLine("3. **summarySql**: 合计行SQL，对整张报表的数值列求和");
        sb.AppendLine("   - 格式：SELECT SUM(数值列) AS 别名 FROM (报表SQL) t");
        sb.AppendLine("   - 只需要聚合数值列，文本列不需要");
        sb.AppendLine("   - 如果没有数值列可以不返回summarySql");
        sb.AppendLine("4. **columns**: 列定义，与sql中的SELECT别名一一对应");
        sb.AppendLine("   - field必须与sql中SELECT的别名完全一致");
        sb.AppendLine("   - dataType: text（文本）/ number（数字）/ date（日期）");
        sb.AppendLine("   - 数字列使用 align: \"right\"，文本列使用 align: \"left\"");
        sb.AppendLine("   - width单位是像素，合理设置列宽");
        sb.AppendLine("5. 只生成SELECT查询，必须返回有效JSON");
        sb.AppendLine("6. 报表的SQL应返回分组汇总后的数据（按日期+科室+维度等GROUP BY），不要返回逐条明细记录");
        sb.AppendLine("7. 重要：SQL必须使用 GROUP BY 进行聚合，确保结果行数在合理范围（几百到几千行以内），禁止不加GROUP BY直接SELECT明细");

        // 用户自定义提示词
        if (!string.IsNullOrEmpty(customPrompt))
        {
            sb.AppendLine();
            sb.AppendLine("## 补充说明");
            sb.AppendLine(customPrompt);
        }

        return sb.ToString();
    }

    /// <summary>
    /// 解析报表模式的AI响应
    /// </summary>
    private class ReportLlmParseResult
    {
        public string Answer { get; set; } = string.Empty;
        public List<ReportSheetAi>? Sheets { get; set; }
    }

    private class ReportSheetAi
    {
        public string? Title { get; set; }
        public string? Sql { get; set; }
        public string? SummarySql { get; set; }
        public List<ReportColumnDef>? Columns { get; set; }
    }

    private ReportLlmParseResult ParseReportLlmResponse(string content)
    {
        var result = new ReportLlmParseResult();

        try
        {
            var json = content.Trim();
            if (json.Contains("```"))
            {
                json = Regex.Replace(json, @"```\w*\s*", "");
                json = json.Replace("```", "").Trim();
            }

            var jsonMatch = Regex.Match(json, @"\{[\s\S]*\}", RegexOptions.Multiline);
            if (!jsonMatch.Success)
            {
                result.Answer = content;
                return result;
            }

            var doc = JsonDocument.Parse(jsonMatch.Value);
            var root = doc.RootElement;

            if (root.TryGetProperty("answer", out var answerEl))
                result.Answer = answerEl.GetString() ?? "";

            if (root.TryGetProperty("sheets", out var sheetsEl) && sheetsEl.ValueKind == JsonValueKind.Array)
            {
                result.Sheets = new List<ReportSheetAi>();
                foreach (var sheetEl in sheetsEl.EnumerateArray())
                {
                    var sheet = new ReportSheetAi();

                    if (sheetEl.TryGetProperty("title", out var titleEl))
                        sheet.Title = titleEl.GetString();

                    if (sheetEl.TryGetProperty("sql", out var sqlEl))
                        sheet.Sql = sqlEl.GetString();

                    if (sheetEl.TryGetProperty("summarySql", out var summarySqlEl))
                        sheet.SummarySql = summarySqlEl.GetString();

                    if (sheetEl.TryGetProperty("columns", out var colsEl) && colsEl.ValueKind == JsonValueKind.Array)
                    {
                        sheet.Columns = new List<ReportColumnDef>();
                        foreach (var colEl in colsEl.EnumerateArray())
                        {
                            var col = new ReportColumnDef();
                            if (colEl.TryGetProperty("field", out var f)) col.Field = f.GetString() ?? "";
                            if (colEl.TryGetProperty("title", out var t)) col.Title = t.GetString() ?? "";
                            if (colEl.TryGetProperty("dataType", out var dt)) col.DataType = dt.GetString() ?? "text";
                            if (colEl.TryGetProperty("width", out var w)) col.Width = w.GetInt32();
                            if (colEl.TryGetProperty("align", out var a)) col.Align = a.GetString() ?? "left";
                            if (!string.IsNullOrEmpty(col.Field))
                                sheet.Columns.Add(col);
                        }
                    }

                    result.Sheets.Add(sheet);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析报表AI响应失败");
            result.Answer = content;
        }

        return result;
    }

    /// <summary>
    /// 保存会话和消息到数据库
    /// </summary>
    private async Task SaveSessionAsync(AiChatRequest request, AiChatResponse response, string mode)
    {
        try
        {
            var sessionKey = response.SessionId;
            var session = await _db.AiSessions.FirstOrDefaultAsync(s => s.SessionKey == sessionKey);

            if (session == null)
            {
                session = new Domain.Entities.AiSession
                {
                    SessionKey = sessionKey,
                    Title = ExtractCleanTitle(request.Question),  // 使用干净标题
                    Mode = mode,
                    DatasourceId = request.DatasourceId,
                    UserId = 1,
                    CreatedAt = DateTime.UtcNow,
                    LastActiveAt = DateTime.UtcNow
                };
                _db.AiSessions.Add(session);
                await _db.SaveChangesAsync();
            }
            else
            {
                session.LastActiveAt = DateTime.UtcNow;
            }

            // 保存用户消息
            var userMessage = new Domain.Entities.AiMessage
            {
                SessionId = session.Id,
                Role = "user",
                Mode = mode,
                Content = request.Question,
                CreatedAt = DateTime.UtcNow
            };
            _db.AiMessages.Add(userMessage);

            // 保存AI回复消息
            var aiMessage = new Domain.Entities.AiMessage
            {
                SessionId = session.Id,
                Role = "assistant",
                Mode = mode,
                Content = response.Answer ?? "",
                Sql = response.Sql,  // ★ 保存SQL（患者360需要用于回放）
                PromptsJson = response.Prompts != null
                    ? System.Text.Json.JsonSerializer.Serialize(response.Prompts)
                    : null,  // ★ 保存提示词JSON
                TokensUsed = response.TokensUsed ?? 0,
                CreatedAt = DateTime.UtcNow
            };
            _db.AiMessages.Add(aiMessage);
            await _db.SaveChangesAsync();

            response.MessageId = aiMessage.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "保存会话失败");
        }
    }

    /// <summary>
    /// 两阶段查询 - 第一阶段：让LLM从表名列表中选择与问题相关的表（返回提示词信息）
    /// </summary>
    private async Task<(List<string> tables, string prompt, string response)> SelectRelevantTablesWithPromptAsync(
        List<TableInfo> allTables, string question, string dbType, string? ragContext = null)
    {
        // 构建表名列表（包含注释）
        var tableListSb = new StringBuilder();
        tableListSb.AppendLine("可用的数据库表列表：");
        foreach (var table in allTables)
        {
            if (!string.IsNullOrEmpty(table.Comment))
                tableListSb.AppendLine($"- {table.Name}: {table.Comment}");
            else
                tableListSb.AppendLine($"- {table.Name}");
        }

        // ★ 构建RAG参考部分
        var ragSection = string.Empty;
        if (!string.IsNullOrEmpty(ragContext))
        {
            ragSection = $@"

## 业务知识参考（RAG检索结果）
以下是从知识库中检索到的与问题相关的业务知识，请参考这些信息来选择正确的表：
{ragContext}
";
        }

        var prompt = $@"你是一个数据库专家。用户有一个数据分析问题，你需要从下面的表列表中选择与问题最相关的表。

## 用户问题
{question}
{ragSection}
## {tableListSb}

## 要求
1. 分析用户问题，结合业务知识参考（如有），判断需要查询哪些表
2. 只选择与问题直接相关的表，最多选择 {MAX_TABLES_PER_PHASE} 张表
3. 如果问题涉及关联查询，选择所有需要JOIN的表
4. 如果业务知识参考中提到了具体的表名，优先选择这些表
5. 返回格式为JSON数组，只包含表名，例如：[""table1"", ""table2"", ""table3""]
6. 只返回JSON数组，不要包含其他内容";

        var messages = new List<LlmMessage>
        {
            LlmMessage.User(prompt)
        };

        var llmResponse = await _llmService.ChatAsync(messages, new LlmOptions
        {
            Temperature = 0.1,  // 极低温度确保稳定输出
            MaxTokens = 500,
            BusinessType = AiBusinessType.Bi  // 表选择使用BI配置
        });

        if (!llmResponse.Success)
        {
            _logger.LogWarning("两阶段查询第一阶段失败: {Error}，使用前{Max}张表", llmResponse.Error, MAX_TABLES_PER_PHASE);
            return (allTables.Take(MAX_TABLES_PER_PHASE).Select(t => t.Name).ToList(), prompt, $"错误: {llmResponse.Error}");
        }

        // 解析LLM返回的表名列表
        try
        {
            var content = llmResponse.Content.Trim();
            // 去除可能的markdown代码块标记
            if (content.StartsWith("```"))
            {
                content = Regex.Replace(content, @"```\w*\s*", "");
                content = content.Replace("```", "").Trim();
            }

            var selectedTables = JsonSerializer.Deserialize<List<string>>(content);
            if (selectedTables != null && selectedTables.Count > 0)
            {
                // 验证表名是否存在
                var validTables = selectedTables
                    .Where(t => allTables.Any(at => at.Name.Equals(t, StringComparison.OrdinalIgnoreCase)))
                    .Take(MAX_TABLES_PER_PHASE)
                    .ToList();

                if (validTables.Count > 0)
                    return (validTables, prompt, llmResponse.Content);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析表选择结果失败: {Content}", llmResponse.Content);
        }

        // 解析失败，使用前N张表
        return (allTables.Take(MAX_TABLES_PER_PHASE).Select(t => t.Name).ToList(), prompt, llmResponse.Content);
    }
    
    /// <summary>
    /// 获取数据源的表列表
    /// </summary>
    [HttpGet("tables/{datasourceId}")]
    public async Task<ActionResult<ApiResponse<List<TableInfo>>>> GetTables(long datasourceId)
    {
        try
        {
            var tables = await _schemaService.GetTablesAsync(datasourceId);
            return Ok(ApiResponse<List<TableInfo>>.Success(tables));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<List<TableInfo>>.Fail(ex.Message));
        }
    }
    
    /// <summary>
    /// 获取表的字段列表
    /// </summary>
    [HttpGet("columns/{datasourceId}/{tableName}")]
    public async Task<ActionResult<ApiResponse<List<Application.Services.ColumnInfo>>>> GetColumns(long datasourceId, string tableName)
    {
        try
        {
            var columns = await _schemaService.GetColumnsAsync(datasourceId, tableName);
            return Ok(ApiResponse<List<Application.Services.ColumnInfo>>.Success(columns));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<List<Application.Services.ColumnInfo>>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 将AI分析结果保存为正式图表
    /// </summary>
    [HttpPost("save-as-chart")]
    public async Task<ActionResult<ApiResponse<long>>> SaveAsChart([FromBody] SaveAsChartRequest request)
    {
        try
        {
            var datasource = await _db.Datasources.FindAsync(request.DatasourceId);
            if (datasource == null)
                return Ok(ApiResponse<long>.Fail("数据源不存在"));

            // 创建数据集
            var dataset = new Domain.Entities.Dataset
            {
                Name = $"AI生成-{request.Title}",
                DatasourceId = request.DatasourceId,
                SqlText = request.Sql,
                Remark = $"由AI智能分析自动生成，原始问题：{request.Question}"
            };
            _db.Datasets.Add(dataset);
            await _db.SaveChangesAsync();

            // 执行SQL探测字段，自动生成DatasetField和ChartConfig
            var dimensions = new List<object>();
            var measures = new List<object>();
            try
            {
                var sampleData = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, request.Sql);
                if (sampleData.Count > 0)
                {
                    var columns = sampleData[0].Keys.ToList();
                    for (int i = 0; i < columns.Count; i++)
                    {
                        var fieldName = columns[i];
                        var sampleVal = sampleData[0][fieldName];
                        var isNumeric = sampleVal != null && IsNumericValue(sampleVal);
                        var role = (i == 0 || !isNumeric) ? "dim" : "measure";
                        var aggType = isNumeric ? "sum" : "none";

                        if (isNumeric && i > 0)
                        {
                            measures.Add(new { field = fieldName, alias = fieldName, aggType = "sum" });
                        }
                        else
                        {
                            dimensions.Add(new { field = fieldName, alias = fieldName });
                        }

                        _db.DatasetFields.Add(new Domain.Entities.DatasetField
                        {
                            DatasetId = dataset.Id,
                            FieldName = fieldName,
                            FieldAlias = fieldName,
                            DataType = isNumeric ? "number" : "text",
                            Role = role,
                            AggType = aggType,
                            SortOrder = i
                        });
                    }
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "探测字段失败，跳过字段自动生成: {Sql}", request.Sql);
            }

            // 构建ChartConfig JSON
            var chartType = request.ChartType ?? "bar";
            string configJson;
            if (chartType == "kpi")
            {
                configJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    dimensions,
                    measures,
                    title = request.Title
                });
            }
            else
            {
                configJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    dimensions,
                    measures
                });
            }

            // 创建图表
            var chart = new Domain.Entities.Chart
            {
                Name = request.Title,
                DatasetId = dataset.Id,
                ChartType = chartType,
                ConfigJson = configJson,
                Remark = "由AI智能分析自动生成"
            };
            _db.Charts.Add(chart);
            await _db.SaveChangesAsync();

            _logger.LogInformation("保存图表成功: ChartId={ChartId}, DatasetId={DatasetId}, Dimensions={DimCount}, Measures={MeasureCount}",
                chart.Id, dataset.Id, dimensions.Count, measures.Count);

            return Ok(ApiResponse<long>.Success(chart.Id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存为图表失败");
            return Ok(ApiResponse<long>.Fail(ex.Message));
        }
    }

    private static bool IsNumericValue(object val)
    {
        return val is int or long or float or double or decimal or short or byte or uint or ulong or ushort or sbyte;
    }

    /// <summary>
    /// 下钻查询 - 基于已保存的明细SQL进行聚合分析
    /// </summary>
    [HttpPost("drill")]
    public async Task<ActionResult<ApiResponse<DrillResponse>>> Drill([FromBody] DrillRequest request)
    {
        try
        {
            // 获取消息中的明细SQL
            var message = await _db.AiMessages.FindAsync(request.MessageId);
            if (message == null || string.IsNullOrEmpty(message.DetailSql))
            {
                return Ok(ApiResponse<DrillResponse>.Fail("未找到明细SQL，请先进行AI分析"));
            }

            // 获取数据源
            var datasource = await _db.Datasources.FindAsync(request.DatasourceId);
            if (datasource == null)
            {
                return Ok(ApiResponse<DrillResponse>.Fail("数据源不存在"));
            }

            // 构建聚合SQL
            var detailSql = message.DetailSql;
            var groupBy = request.GroupBy;

            // 构建度量表达式
            var measureExprs = new List<string>();
            if (request.Measures != null && request.Measures.Count > 0)
            {
                foreach (var m in request.Measures)
                {
                    var expr = m.Field == "*" ? $"{m.Agg}(*)" : $"{m.Agg}({m.Field})";
                    measureExprs.Add($"{expr} as {m.Alias}");
                }
            }
            else
            {
                measureExprs.Add("COUNT(*) as 数量");
            }

            // 构建WHERE条件
            var whereClauses = new List<string>();
            if (request.Filters != null)
            {
                foreach (var filter in request.Filters)
                {
                    var safeField = filter.Field.Replace("'", "''");
                    var safeValue = filter.Value.Replace("'", "''");
                    switch (filter.Op.ToLower())
                    {
                        case "=":
                            whereClauses.Add($"{safeField} = '{safeValue}'");
                            break;
                        case "!=":
                            whereClauses.Add($"{safeField} != '{safeValue}'");
                            break;
                        case "like":
                            whereClauses.Add($"{safeField} LIKE '%{safeValue}%'");
                            break;
                        case "in":
                            var values = safeValue.Split(',').Select(v => $"'{v.Trim()}'");
                            whereClauses.Add($"{safeField} IN ({string.Join(",", values)})");
                            break;
                        default:
                            whereClauses.Add($"{safeField} {filter.Op} '{safeValue}'");
                            break;
                    }
                }
            }

            // 组装最终SQL
            var whereClause = whereClauses.Count > 0 ? $"WHERE {string.Join(" AND ", whereClauses)}" : "";
            var orderByClause = !string.IsNullOrEmpty(request.OrderBy)
                ? $"ORDER BY {measureExprs[0].Split(" as ")[1]} {request.OrderBy}"
                : $"ORDER BY {measureExprs[0].Split(" as ")[1]} DESC";

            var drillSql = $@"
                SELECT {groupBy}, {string.Join(", ", measureExprs)}
                FROM ({detailSql}) t
                {whereClause}
                GROUP BY {groupBy}
                {orderByClause}
                LIMIT {request.Limit}";

            // 执行SQL
            var data = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, drillSql);

            return Ok(ApiResponse<DrillResponse>.Success(new DrillResponse
            {
                Data = data,
                ExecutedSql = drillSql
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下钻查询失败");
            return Ok(ApiResponse<DrillResponse>.Fail($"下钻查询失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 明细查询 - 点击图表下钻到明细数据（分页）
    /// </summary>
    [HttpPost("detail")]
    public async Task<ActionResult<ApiResponse<DetailResponse>>> Detail([FromBody] DetailRequest request)
    {
        try
        {
            // 获取消息中的明细SQL
            var message = await _db.AiMessages.FindAsync(request.MessageId);
            if (message == null || string.IsNullOrEmpty(message.DetailSql))
            {
                return Ok(ApiResponse<DetailResponse>.Fail("未找到明细SQL，请先进行AI分析"));
            }

            // 获取数据源
            var datasource = await _db.Datasources.FindAsync(request.DatasourceId);
            if (datasource == null)
            {
                return Ok(ApiResponse<DetailResponse>.Fail("数据源不存在"));
            }

            var detailSql = message.DetailSql;

            // ★ 关键修复：如果传入了时间范围，替换SQL中的时间参数
            if (!string.IsNullOrEmpty(request.StartDate) && !string.IsNullOrEmpty(request.EndDate)
                && DateTime.TryParse(request.StartDate, out var sd) && DateTime.TryParse(request.EndDate, out var ed))
            {
                _logger.LogInformation("明细查询：使用时间范围 {Start} 至 {End}，日期字段: {DateField}",
                    request.StartDate, request.EndDate, message.DateField);
                detailSql = ReplaceDateParameters(detailSql, sd, ed, message.DateField);
            }

            // 构建筛选条件
            // 构建筛选条件（不加引号，与明细SQL中的字段名保持一致）
            var whereClauses = new List<string>();
            var filterDesc = new List<string>();
            if (request.Filters != null && request.Filters.Count > 0)
            {
                foreach (var f in request.Filters)
                {
                    // 保持字段名原样，不加引号（因为明细SQL中的字段名可能带有别名等）
                    var safeField = f.Field.Replace("'", "''");
                    var safeValue = f.Value.Replace("'", "''");
                    switch (f.Op.ToLower())
                    {
                        case "=":
                            whereClauses.Add($"{safeField} = '{safeValue}'");
                            filterDesc.Add($"{safeField}={safeValue}");
                            break;
                        case "!=":
                        case "<>":
                            whereClauses.Add($"{safeField} <> '{safeValue}'");
                            filterDesc.Add($"{safeField}≠{safeValue}");
                            break;
                        case "like":
                            whereClauses.Add($"{safeField} LIKE '%{safeValue}%'");
                            filterDesc.Add($"{safeField}包含{safeValue}");
                            break;
                        default:
                            whereClauses.Add($"{safeField} = '{safeValue}'");
                            filterDesc.Add($"{safeField}={safeValue}");
                            break;
                    }
                }
            }

            var whereClause = whereClauses.Count > 0 ? $"WHERE {string.Join(" AND ", whereClauses)}" : "";

            // 计算总数
            var countSql = $"SELECT COUNT(*) as cnt FROM ({detailSql}) t {whereClause}";
            var countData = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, countSql);
            var total = countData.Count > 0 ? Convert.ToInt32(countData[0]["cnt"]) : 0;

            // 构建分页查询
            var offset = (request.Page - 1) * request.PageSize;
            var orderByClause = !string.IsNullOrEmpty(request.OrderBy)
                ? $"ORDER BY \"{request.OrderBy.Replace("\"", "")}\" {(request.OrderDir?.ToLower() == "asc" ? "ASC" : "DESC")}"
                : "";

            var pageSql = $@"
                SELECT *
                FROM ({detailSql}) t
                {whereClause}
                {orderByClause}
                LIMIT {request.PageSize} OFFSET {offset}";

            // 执行SQL
            var data = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, pageSql);

            // 获取列名
            var columns = data.Count > 0 ? data[0].Keys.ToList() : new List<string>();

            return Ok(ApiResponse<DetailResponse>.Success(new DetailResponse
            {
                Data = data,
                Total = total,
                Page = request.Page,
                PageSize = request.PageSize,
                Columns = columns,
                ExecutedSql = pageSql,
                FilterDescription = filterDesc.Count > 0 ? string.Join(", ", filterDesc) : null
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "明细查询失败");
            return Ok(ApiResponse<DetailResponse>.Fail($"明细查询失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 刷新查询 - 带筛选条件重新执行KPI和图表查询
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<RefreshResponse>>> Refresh([FromBody] RefreshRequest request)
    {
        try
        {
            // 获取消息
            var message = await _db.AiMessages.FindAsync(request.MessageId);
            if (message == null || string.IsNullOrEmpty(message.DetailSql))
            {
                return Ok(ApiResponse<RefreshResponse>.Fail("未找到原始查询数据"));
            }

            // 获取数据源
            var datasource = await _db.Datasources.FindAsync(request.DatasourceId);
            if (datasource == null)
            {
                return Ok(ApiResponse<RefreshResponse>.Fail("数据源不存在"));
            }

            var detailSql = message.DetailSql;

            // ★ 解析保存的KPI配置（提前解析，因为时间替换需要同时处理）
            var kpiConfigs = string.IsNullOrEmpty(message.KpiConfig)
                ? new List<KpiConfig>()
                : System.Text.Json.JsonSerializer.Deserialize<List<KpiConfig>>(message.KpiConfig) ?? new List<KpiConfig>();

            // ★ 关键修复：如果传入了时间范围，先替换SQL中的时间参数
            if (!string.IsNullOrEmpty(request.StartDate) && !string.IsNullOrEmpty(request.EndDate)
                && DateTime.TryParse(request.StartDate, out var sd) && DateTime.TryParse(request.EndDate, out var ed))
            {
                _logger.LogInformation("Refresh接口：使用新时间范围 {Start} 至 {End}，日期字段: {DateField}",
                    request.StartDate, request.EndDate, message.DateField);

                // 替换detailSql中的时间参数
                detailSql = ReplaceDateParameters(detailSql, sd, ed, message.DateField);

                // 同时替换KPI配置中的SQL模板
                foreach (var kpiConfig in kpiConfigs)
                {
                    kpiConfig.SqlTemplate = ReplaceDateParameters(kpiConfig.SqlTemplate, sd, ed, message.DateField);
                }
            }

            // 构建筛选条件
            var whereClauses = new List<string>();
            var filterDesc = new List<string>();
            if (request.Filters != null && request.Filters.Count > 0)
            {
                foreach (var f in request.Filters)
                {
                    var safeField = f.Field.Replace("'", "''");
                    var safeValue = f.Value.Replace("'", "''");
                    whereClauses.Add($"{safeField} = '{safeValue}'");
                    filterDesc.Add($"{safeField}={safeValue}");
                }
            }
            var whereClause = whereClauses.Count > 0 ? $"WHERE {string.Join(" AND ", whereClauses)}" : "";

            // 构建带筛选的基础子查询
            var filteredBaseSql = whereClauses.Count > 0
                ? $"SELECT * FROM ({detailSql}) t {whereClause}"
                : detailSql;

            // 解析维度和度量配置
            var dimensions = string.IsNullOrEmpty(message.DimensionFields)
                ? new List<string>()
                : System.Text.Json.JsonSerializer.Deserialize<List<string>>(message.DimensionFields) ?? new List<string>();

            var measures = string.IsNullOrEmpty(message.MeasureFields)
                ? new List<MeasureField>()
                : System.Text.Json.JsonSerializer.Deserialize<List<MeasureField>>(message.MeasureFields) ?? new List<MeasureField>();

            // 解析保存的图表配置
            var defaultCharts = string.IsNullOrEmpty(message.DefaultChartsConfig)
                ? new List<DefaultChartConfig>()
                : System.Text.Json.JsonSerializer.Deserialize<List<DefaultChartConfig>>(message.DefaultChartsConfig) ?? new List<DefaultChartConfig>();

            var queries = new List<QueryItem>();

            // 优先使用保存的KPI配置（保持原有的所有KPI）
            if (kpiConfigs.Count > 0)
            {
                foreach (var kpiConfig in kpiConfigs)
                {
                    // 使用筛选后的数据替换SQL模板中的占位符
                    var kpiSql = kpiConfig.SqlTemplate;
                    if (kpiSql.Contains("(...)"))
                    {
                        kpiSql = kpiSql.Replace("(...)", $"({filteredBaseSql})");
                    }
                    else
                    {
                        // 如果没有占位符，尝试用筛选后的数据作为子查询
                        kpiSql = $"SELECT * FROM ({kpiSql}) orig WHERE EXISTS (SELECT 1 FROM ({filteredBaseSql}) filt)";
                        // 简化处理：直接用筛选后的基础SQL重新构建
                        kpiSql = kpiConfig.SqlTemplate.Replace($"({detailSql})", $"({filteredBaseSql})");
                    }

                    try
                    {
                        var kpiData = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, kpiSql);
                        queries.Add(new QueryItem
                        {
                            Type = "kpi",
                            Title = kpiConfig.Title,
                            Sql = kpiSql,
                            Field = "value",
                            Data = kpiData
                        });
                    }
                    catch (Exception ex)
                    {
                        queries.Add(new QueryItem
                        {
                            Type = "kpi",
                            Title = kpiConfig.Title,
                            Error = ex.Message
                        });
                    }
                }
            }
            else if (measures.Count > 0)
            {
                // 回退：使用度量配置生成KPI
                var m = measures[0];
                var aggExpr = m.Field == "*" ? $"{m.Agg}(*)" : $"{m.Agg}({m.Field})";
                var kpiSql = $"SELECT {aggExpr} as value FROM ({filteredBaseSql}) t";
                try
                {
                    var kpiData = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, kpiSql);
                    queries.Add(new QueryItem
                    {
                        Type = "kpi",
                        Title = m.Alias ?? "总计",
                        Sql = kpiSql,
                        Field = "value",
                        Data = kpiData
                    });
                }
                catch (Exception ex)
                {
                    queries.Add(new QueryItem
                    {
                        Type = "kpi",
                        Title = m.Alias ?? "总计",
                        Error = ex.Message
                    });
                }
            }
            else
            {
                // 默认COUNT查询
                var kpiSql = $"SELECT COUNT(*) as value FROM ({filteredBaseSql}) t";
                try
                {
                    var kpiData = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, kpiSql);
                    queries.Add(new QueryItem
                    {
                        Type = "kpi",
                        Title = "总数量",
                        Sql = kpiSql,
                        Field = "value",
                        Data = kpiData
                    });
                }
                catch (Exception ex)
                {
                    queries.Add(new QueryItem
                    {
                        Type = "kpi",
                        Title = "总数量",
                        Error = ex.Message
                    });
                }
            }

            // 优先使用保存的图表配置生成图表
            if (defaultCharts.Count > 0)
            {
                foreach (var chart in defaultCharts)
                {
                    if (string.IsNullOrEmpty(chart.GroupBy)) continue;

                    var measure = chart.Measure ?? new MeasureField { Field = "*", Agg = "COUNT", Alias = "数量" };
                    var aggExpr = measure.Field == "*" ? $"{measure.Agg}(*)" : $"{measure.Agg}({measure.Field})";
                    var chartSql = $"SELECT {chart.GroupBy}, {aggExpr} as {measure.Alias} FROM ({filteredBaseSql}) t GROUP BY {chart.GroupBy} ORDER BY {measure.Alias} DESC LIMIT 50";

                    try
                    {
                        var chartData = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, chartSql);
                        queries.Add(new QueryItem
                        {
                            Type = chart.Type,
                            Title = chart.Title,
                            Sql = chartSql,
                            Data = chartData
                        });
                    }
                    catch (Exception ex)
                    {
                        queries.Add(new QueryItem
                        {
                            Type = chart.Type,
                            Title = chart.Title,
                            Error = ex.Message
                        });
                    }
                }
            }
            else
            {
                // 回退：使用维度配置生成默认图表
                // 生成时间趋势图表（第一个维度）
                if (dimensions.Count > 0)
                {
                    var timeDim = dimensions[0]; // 假设第一个维度是时间
                    var measure = measures.Count > 0 ? measures[0] : new MeasureField { Field = "*", Agg = "COUNT", Alias = "数量" };
                    var aggExpr = measure.Field == "*" ? $"{measure.Agg}(*)" : $"{measure.Agg}({measure.Field})";
                    var chartSql = $"SELECT {timeDim}, {aggExpr} as {measure.Alias} FROM ({filteredBaseSql}) t GROUP BY {timeDim} ORDER BY {timeDim} LIMIT 100";

                    try
                    {
                        var chartData = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, chartSql);
                        queries.Add(new QueryItem
                        {
                            Type = "line",
                            Title = $"按{timeDim}趋势",
                            Sql = chartSql,
                            Data = chartData
                        });
                    }
                    catch (Exception ex)
                    {
                        queries.Add(new QueryItem
                        {
                            Type = "line",
                            Title = $"按{timeDim}趋势",
                            Error = ex.Message
                        });
                    }
                }

                // 生成分布图表（第二个维度，如果有的话，通常是科室）
                if (dimensions.Count > 1)
                {
                    var categoryDim = dimensions[1];
                    var measure = measures.Count > 0 ? measures[0] : new MeasureField { Field = "*", Agg = "COUNT", Alias = "数量" };
                    var aggExpr = measure.Field == "*" ? $"{measure.Agg}(*)" : $"{measure.Agg}({measure.Field})";
                    var chartSql = $"SELECT {categoryDim}, {aggExpr} as {measure.Alias} FROM ({filteredBaseSql}) t GROUP BY {categoryDim} ORDER BY {measure.Alias} DESC LIMIT 50";

                    try
                    {
                        var chartData = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, chartSql);
                        queries.Add(new QueryItem
                        {
                            Type = "bar",
                            Title = $"按{categoryDim}分布",
                            Sql = chartSql,
                            Data = chartData
                        });
                    }
                    catch (Exception ex)
                    {
                        queries.Add(new QueryItem
                        {
                            Type = "bar",
                            Title = $"按{categoryDim}分布",
                            Error = ex.Message
                        });
                    }
                }
            }

            // ★ 性能优化：并行计算同比环比（如果提供了时间范围）
            if (!string.IsNullOrEmpty(request.StartDate) && !string.IsNullOrEmpty(request.EndDate) &&
                DateTime.TryParse(request.StartDate, out var startDate) &&
                DateTime.TryParse(request.EndDate, out var endDate))
            {
                var dateField = FindDateField(dimensions) ?? "未知日期字段";
                _logger.LogInformation("计算同比环比，日期字段: {DateField}, 时间范围: {Start} 至 {End}, KPI数量: {Count}",
                    dateField, request.StartDate, request.EndDate, queries.Count(q => q.Type == "kpi"));

                // 并行计算所有KPI的同比环比
                var kpiTasks = queries.Select(async (query, index) =>
                {
                    if (query.Type == "kpi" && query.Error == null)
                    {
                        _logger.LogDebug("开始计算KPI同比环比: {Title}", query.Title);
                        queries[index] = await CalculateKpiYoyMomAsync(
                            query, filteredBaseSql, dateField,
                            startDate, endDate,
                            datasource.Type, datasource.ConnString);
                        _logger.LogDebug("KPI同比环比计算完成: {Title}, 同比率: {YoyRate}%, 环比率: {MomRate}%",
                            queries[index].Title, queries[index].YoyRate, queries[index].MomRate);
                    }
                });
                await Task.WhenAll(kpiTasks);
                _logger.LogInformation("并行计算同比环比完成");
            }
            else
            {
                _logger.LogDebug("未计算同比环比：StartDate={Start}, EndDate={End}",
                    request.StartDate ?? "null", request.EndDate ?? "null");
            }

            return Ok(ApiResponse<RefreshResponse>.Success(new RefreshResponse
            {
                Queries = queries,
                FilterDescription = filterDesc.Count > 0 ? string.Join(", ", filterDesc) : null
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新查询失败");
            return Ok(ApiResponse<RefreshResponse>.Fail($"刷新查询失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 获取医院列表 - 基于明细SQL获取可筛选的医院
    /// </summary>
    [HttpGet("hospitals/{messageId}/{datasourceId}")]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetHospitals(long messageId, long datasourceId)
    {
        try
        {
            // 获取消息
            var message = await _db.AiMessages.FindAsync(messageId);
            if (message == null || string.IsNullOrEmpty(message.DetailSql) || string.IsNullOrEmpty(message.HospitalField))
            {
                return Ok(ApiResponse<List<string>>.Success(new List<string>()));
            }

            // 获取数据源
            var datasource = await _db.Datasources.FindAsync(datasourceId);
            if (datasource == null)
            {
                return Ok(ApiResponse<List<string>>.Fail("数据源不存在"));
            }

            // 查询医院列表
            var hospitalField = message.HospitalField;
            var sql = $"SELECT DISTINCT {hospitalField} FROM ({message.DetailSql}) t WHERE {hospitalField} IS NOT NULL ORDER BY {hospitalField} LIMIT 100";

            var data = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, sql);
            var hospitals = data
                .Select(row => row.Values.FirstOrDefault()?.ToString() ?? "")
                .Where(h => !string.IsNullOrEmpty(h))
                .ToList();

            return Ok(ApiResponse<List<string>>.Success(hospitals));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取医院列表失败");
            return Ok(ApiResponse<List<string>>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 获取会话历史列表
    /// </summary>
    [HttpGet("sessions")]
    public async Task<ActionResult<ApiResponse<List<SessionListItem>>>> GetSessions([FromQuery] long? datasourceId = null)
    {
        try
        {
            var query = _db.AiSessions.Include(s => s.Messages).AsQueryable();

            if (datasourceId.HasValue)
            {
                query = query.Where(s => s.DatasourceId == datasourceId.Value);
            }

            var sessionsWithImages = await query
                .Where(s => !string.IsNullOrEmpty(s.Title))  // 必须有标题
                .OrderByDescending(s => s.LastActiveAt)
                .Take(50)  // 最多返回50条
                .Select(s => new
                {
                    Session = s,
                    // 获取所有消息的ChartImages字段（JSON数组）
                    ChartImagesJson = s.Messages
                        .Where(m => !string.IsNullOrEmpty(m.ChartImages))
                        .Select(m => m.ChartImages)
                        .ToList(),
                    // 获取最后一条assistant消息ID
                    LastMessageId = s.Messages
                        .Where(m => m.Role == "assistant")
                        .OrderByDescending(m => m.CreatedAt)
                        .Select(m => (long?)m.Id)
                        .FirstOrDefault()
                })
                .ToListAsync();

            // 转换为 SessionListItem 并计算图片数量
            var sessions = sessionsWithImages.Select(x =>
            {
                // 统计所有消息中的图片总数
                int imageCount = 0;
                foreach (var json in x.ChartImagesJson)
                {
                    try
                    {
                        var images = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                        imageCount += images?.Count ?? 0;
                    }
                    catch { /* 忽略解析错误 */ }
                }

                return new SessionListItem
                {
                    Id = x.Session.Id,
                    SessionKey = x.Session.SessionKey,
                    Title = x.Session.Title ?? "",
                    Mode = x.Session.Mode,
                    DatasourceId = x.Session.DatasourceId,
                    CreatedAt = x.Session.CreatedAt,
                    LastActiveAt = x.Session.LastActiveAt,
                    LastMessageId = x.LastMessageId,
                    ImageCount = imageCount  // 已保存图片数量
                };
            }).ToList();

            return Ok(ApiResponse<List<SessionListItem>>.Success(sessions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取会话列表失败");
            return Ok(ApiResponse<List<SessionListItem>>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 删除会话
    /// </summary>
    [HttpDelete("sessions/{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteSession(long id)
    {
        try
        {
            var session = await _db.AiSessions
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (session == null)
            {
                return Ok(ApiResponse<bool>.Fail("会话不存在"));
            }

            // 删除会话的所有消息
            _db.AiMessages.RemoveRange(session.Messages);
            // 删除会话
            _db.AiSessions.Remove(session);
            await _db.SaveChangesAsync();

            _logger.LogInformation("删除会话成功: {SessionId}", id);
            return Ok(ApiResponse<bool>.Success(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除会话失败: {SessionId}", id);
            return Ok(ApiResponse<bool>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 更新会话标题
    /// </summary>
    [HttpPut("sessions/{id}/title")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateSessionTitle(long id, [FromBody] UpdateSessionTitleRequest request)
    {
        try
        {
            var session = await _db.AiSessions.FirstOrDefaultAsync(s => s.Id == id);
            if (session == null)
            {
                return Ok(ApiResponse<bool>.Fail("会话不存在"));
            }

            session.Title = request.Title;
            await _db.SaveChangesAsync();

            _logger.LogInformation("更新会话标题成功: {SessionId} -> {Title}", id, request.Title);
            return Ok(ApiResponse<bool>.Success(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新会话标题失败: {SessionId}", id);
            return Ok(ApiResponse<bool>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 获取会话的已保存图片列表
    /// </summary>
    [HttpGet("sessions/{id}/images")]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetSessionImages(long id)
    {
        try
        {
            // 获取会话的所有消息中的图片
            var session = await _db.AiSessions
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (session == null)
            {
                return Ok(ApiResponse<List<string>>.Fail("会话不存在"));
            }

            // 收集所有图片路径
            var images = new List<string>();
            foreach (var msg in session.Messages.Where(m => !string.IsNullOrEmpty(m.ChartImages)))
            {
                try
                {
                    var chartImages = System.Text.Json.JsonSerializer.Deserialize<List<string>>(msg.ChartImages!);
                    if (chartImages != null)
                    {
                        images.AddRange(chartImages);
                    }
                }
                catch { /* 忽略解析错误 */ }
            }

            return Ok(ApiResponse<List<string>>.Success(images));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取会话图片失败: {SessionId}", id);
            return Ok(ApiResponse<List<string>>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 删除会话中的指定图片
    /// </summary>
    [HttpDelete("sessions/{id}/images")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteSessionImage(long id, [FromQuery] string imagePath)
    {
        try
        {
            if (string.IsNullOrEmpty(imagePath))
            {
                return Ok(ApiResponse<bool>.Fail("图片路径不能为空"));
            }

            // 从URL中提取相对路径（移除后端地址前缀）
            var relativePath = imagePath;
            if (imagePath.Contains("/uploads/"))
            {
                var idx = imagePath.IndexOf("/uploads/");
                relativePath = imagePath.Substring(idx);
            }

            // 获取会话的所有消息
            var session = await _db.AiSessions
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (session == null)
            {
                return Ok(ApiResponse<bool>.Fail("会话不存在"));
            }

            // 遍历消息，找到并删除指定图片
            var imageDeleted = false;
            foreach (var msg in session.Messages.Where(m => !string.IsNullOrEmpty(m.ChartImages)))
            {
                try
                {
                    var chartImages = JsonSerializer.Deserialize<List<string>>(msg.ChartImages!);
                    if (chartImages != null && chartImages.Contains(relativePath))
                    {
                        // 从列表中移除
                        chartImages.Remove(relativePath);
                        msg.ChartImages = chartImages.Count > 0
                            ? JsonSerializer.Serialize(chartImages)
                            : null;
                        imageDeleted = true;

                        // 删除物理文件
                        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                        if (System.IO.File.Exists(fullPath))
                        {
                            System.IO.File.Delete(fullPath);
                            _logger.LogInformation("删除图片文件: {Path}", fullPath);
                        }
                        break;
                    }
                }
                catch { /* 忽略解析错误 */ }
            }

            if (imageDeleted)
            {
                await _db.SaveChangesAsync();
                _logger.LogInformation("删除会话图片成功: SessionId={SessionId}, ImagePath={ImagePath}", id, relativePath);
                return Ok(ApiResponse<bool>.Success(true, "图片删除成功"));
            }
            else
            {
                return Ok(ApiResponse<bool>.Fail("未找到指定图片"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除会话图片失败: {SessionId}", id);
            return Ok(ApiResponse<bool>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 重放历史会话 - 基于保存的配置重新执行SQL获取最新数据
    /// 支持传入新的时间范围，替换原SQL中的时间参数
    /// </summary>
    [HttpPost("replay/{messageId}")]
    public async Task<ActionResult<ApiResponse<AiChatResponse>>> ReplaySession(
        long messageId,
        [FromQuery] long datasourceId,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null)
    {
        try
        {
            // 获取消息
            var message = await _db.AiMessages
                .Include(m => m.Session)
                .FirstOrDefaultAsync(m => m.Id == messageId && m.Role == "assistant");

            if (message == null)
            {
                return Ok(ApiResponse<AiChatResponse>.Fail("未找到历史消息"));
            }

            // ★ 根据模式分发处理
            var mode = message.Mode ?? message.Session?.Mode ?? "bi";
            _logger.LogInformation("历史会话重放：消息ID={MessageId}, 模式={Mode}", messageId, mode);

            // 患者360模式 - 重新执行SQL获取患者列表
            if (mode == "hz360")
            {
                return await ReplayHz360SessionAsync(message, datasourceId);
            }

            // 知识问答模式 - 直接返回保存的回答
            if (mode == "internetsearch")
            {
                return await ReplayInternetSearchSessionAsync(message);
            }

            // 报表模式 - 重新执行SQL生成报表
            if (mode == "report")
            {
                return await ReplayReportSessionAsync(message, datasourceId, startDate, endDate);
            }

            // ★ 以下是BI模式的处理逻辑
            if (string.IsNullOrEmpty(message.DetailSql))
            {
                return Ok(ApiResponse<AiChatResponse>.Fail("BI模式需要DetailSql，但未找到"));
            }

            // 获取数据源
            var datasource = await _db.Datasources.FindAsync(datasourceId);
            if (datasource == null)
            {
                return Ok(ApiResponse<AiChatResponse>.Fail("数据源不存在"));
            }

            // 解析保存的配置
            var dimensions = string.IsNullOrEmpty(message.DimensionFields)
                ? new List<string>()
                : System.Text.Json.JsonSerializer.Deserialize<List<string>>(message.DimensionFields) ?? new List<string>();

            var measures = string.IsNullOrEmpty(message.MeasureFields)
                ? new List<MeasureField>()
                : System.Text.Json.JsonSerializer.Deserialize<List<MeasureField>>(message.MeasureFields) ?? new List<MeasureField>();

            var defaultCharts = string.IsNullOrEmpty(message.DefaultChartsConfig)
                ? new List<DefaultChartConfig>()
                : System.Text.Json.JsonSerializer.Deserialize<List<DefaultChartConfig>>(message.DefaultChartsConfig) ?? new List<DefaultChartConfig>();

            // ★ 解析保存的KPI配置（修复：使用KpiConfig恢复所有KPI，而不是只用measures生成1个）
            var kpiConfigs = string.IsNullOrEmpty(message.KpiConfig)
                ? new List<KpiConfig>()
                : System.Text.Json.JsonSerializer.Deserialize<List<KpiConfig>>(message.KpiConfig) ?? new List<KpiConfig>();

            var detailSql = message.DetailSql;

            // ★ 如果传入了新的时间范围，替换SQL中的时间参数
            DateTime? parsedStartDate = null;
            DateTime? parsedEndDate = null;
            if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate) &&
                DateTime.TryParse(startDate, out var sd) && DateTime.TryParse(endDate, out var ed))
            {
                parsedStartDate = sd;
                parsedEndDate = ed;
                _logger.LogInformation("历史会话重放：使用新时间范围 {Start} 至 {End}，日期字段: {DateField}", startDate, endDate, message.DateField);

                // 替换detailSql中的时间参数（传入日期字段用于智能替换）
                detailSql = ReplaceDateParameters(detailSql, sd, ed, message.DateField);

                // 同时替换KPI配置中的SQL模板
                foreach (var kpiConfig in kpiConfigs)
                {
                    kpiConfig.SqlTemplate = ReplaceDateParameters(kpiConfig.SqlTemplate, sd, ed, message.DateField);
                }
            }

            var queries = new List<QueryItem>();

            // ★ 优先使用保存的KPI配置（保持原有的所有KPI）
            if (kpiConfigs.Count > 0)
            {
                _logger.LogInformation("历史会话重放：使用KpiConfig恢复 {Count} 个KPI", kpiConfigs.Count);
                foreach (var kpiConfig in kpiConfigs)
                {
                    // 使用明细SQL替换SQL模板中的占位符
                    var kpiSql = kpiConfig.SqlTemplate;
                    if (kpiSql.Contains("(...)"))
                    {
                        kpiSql = kpiSql.Replace("(...)", $"({detailSql})");
                    }

                    try
                    {
                        var kpiData = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, kpiSql);
                        queries.Add(new QueryItem
                        {
                            Type = "kpi",
                            Title = kpiConfig.Title,
                            Sql = kpiSql,
                            Field = "value",
                            Data = kpiData
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "KPI查询失败: {Title}", kpiConfig.Title);
                        queries.Add(new QueryItem
                        {
                            Type = "kpi",
                            Title = kpiConfig.Title,
                            Error = ex.Message
                        });
                    }
                }
            }
            else if (measures.Count > 0)
            {
                // 回退：使用度量配置生成单个KPI
                _logger.LogInformation("历史会话重放：使用measures回退生成KPI");
                var m = measures[0];
                var aggExpr = m.Field == "*" ? $"{m.Agg}(*)" : $"{m.Agg}({m.Field})";
                var kpiSql = $"SELECT {aggExpr} as value FROM ({detailSql}) t";
                try
                {
                    var kpiData = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, kpiSql);
                    queries.Add(new QueryItem
                    {
                        Type = "kpi",
                        Title = m.Alias ?? "总计",
                        Sql = kpiSql,
                        Field = "value",
                        Data = kpiData
                    });
                }
                catch (Exception ex)
                {
                    queries.Add(new QueryItem { Type = "kpi", Title = m.Alias ?? "总计", Error = ex.Message });
                }
            }
            else
            {
                // 最终回退：COUNT(*)
                var kpiSql = $"SELECT COUNT(*) as value FROM ({detailSql}) t";
                try
                {
                    var kpiData = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, kpiSql);
                    queries.Add(new QueryItem { Type = "kpi", Title = "总数量", Sql = kpiSql, Field = "value", Data = kpiData });
                }
                catch (Exception ex)
                {
                    queries.Add(new QueryItem { Type = "kpi", Title = "总数量", Error = ex.Message });
                }
            }

            // 使用保存的图表配置生成图表
            if (defaultCharts.Count > 0)
            {
                foreach (var chart in defaultCharts)
                {
                    if (string.IsNullOrEmpty(chart.GroupBy)) continue;

                    var measure = chart.Measure ?? new MeasureField { Field = "*", Agg = "COUNT", Alias = "数量" };
                    var aggExpr = measure.Field == "*" ? $"{measure.Agg}(*)" : $"{measure.Agg}({measure.Field})";
                    var chartSql = $"SELECT {chart.GroupBy}, {aggExpr} as {measure.Alias} FROM ({detailSql}) t GROUP BY {chart.GroupBy} ORDER BY {measure.Alias} DESC LIMIT 50";

                    try
                    {
                        var chartData = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, chartSql);
                        queries.Add(new QueryItem { Type = chart.Type, Title = chart.Title, Sql = chartSql, Data = chartData });
                    }
                    catch (Exception ex)
                    {
                        queries.Add(new QueryItem { Type = chart.Type, Title = chart.Title, Error = ex.Message });
                    }
                }
            }
            else if (dimensions.Count > 0)
            {
                // 回退到维度配置
                var measure = measures.Count > 0 ? measures[0] : new MeasureField { Field = "*", Agg = "COUNT", Alias = "数量" };
                var aggExpr = measure.Field == "*" ? $"{measure.Agg}(*)" : $"{measure.Agg}({measure.Field})";

                var chartSql = $"SELECT {dimensions[0]}, {aggExpr} as {measure.Alias} FROM ({detailSql}) t GROUP BY {dimensions[0]} ORDER BY {dimensions[0]} LIMIT 100";
                try
                {
                    var chartData = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, chartSql);
                    queries.Add(new QueryItem { Type = "line", Title = $"按{dimensions[0]}趋势", Sql = chartSql, Data = chartData });
                }
                catch (Exception ex)
                {
                    queries.Add(new QueryItem { Type = "line", Title = $"按{dimensions[0]}趋势", Error = ex.Message });
                }

                if (dimensions.Count > 1)
                {
                    chartSql = $"SELECT {dimensions[1]}, {aggExpr} as {measure.Alias} FROM ({detailSql}) t GROUP BY {dimensions[1]} ORDER BY {measure.Alias} DESC LIMIT 50";
                    try
                    {
                        var chartData = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, chartSql);
                        queries.Add(new QueryItem { Type = "bar", Title = $"按{dimensions[1]}分布", Sql = chartSql, Data = chartData });
                    }
                    catch (Exception ex)
                    {
                        queries.Add(new QueryItem { Type = "bar", Title = $"按{dimensions[1]}分布", Error = ex.Message });
                    }
                }
            }

            // ★ 如果没有 hospitalField，尝试从 dimensions 中自动识别医院相关字段
            var effectiveHospitalField = message.HospitalField;
            if (string.IsNullOrEmpty(effectiveHospitalField) && dimensions?.Any() == true)
            {
                var hospitalPatterns = new[] { "医院", "机构", "院区", "hospital", "org", "institution" };
                foreach (var dim in dimensions)
                {
                    var lowerDim = dim.ToLower();
                    if (hospitalPatterns.Any(p => lowerDim.Contains(p)))
                    {
                        effectiveHospitalField = dim;
                        _logger.LogInformation("历史会话重放：自动识别医院字段: {Field}", dim);
                        break;
                    }
                }
            }

            // ★ 性能优化：并行执行同比环比计算和医院列表查询
            var hospitals = new List<string>();
            var parallelTasks = new List<Task>();

            // 任务1：计算同比环比（如果提供了时间范围）
            if (parsedStartDate.HasValue && parsedEndDate.HasValue)
            {
                var dateFieldForCalc = message.DateField ?? FindDateField(dimensions) ?? "未知日期字段";
                _logger.LogInformation("历史会话重放：计算同比环比，日期字段: {DateField}, 时间范围: {Start} 至 {End}",
                    dateFieldForCalc, parsedStartDate.Value.ToString("yyyy-MM-dd"), parsedEndDate.Value.ToString("yyyy-MM-dd"));

                // 并行计算所有KPI的同比环比
                var kpiTasks = queries.Select(async (query, index) =>
                {
                    if (query.Type == "kpi" && query.Error == null)
                    {
                        queries[index] = await CalculateKpiYoyMomAsync(
                            query, detailSql, dateFieldForCalc,
                            parsedStartDate.Value, parsedEndDate.Value,
                            datasource.Type, datasource.ConnString);
                    }
                });
                parallelTasks.Add(Task.WhenAll(kpiTasks));
            }
            else
            {
                _logger.LogDebug("历史会话重放：未计算同比环比（无时间范围参数）");
            }

            // 任务2：加载医院列表（如果有HospitalField）
            if (!string.IsNullOrEmpty(effectiveHospitalField) && !string.IsNullOrEmpty(detailSql))
            {
                parallelTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var hospitalSql = $"SELECT DISTINCT {effectiveHospitalField} FROM ({detailSql}) t WHERE {effectiveHospitalField} IS NOT NULL ORDER BY {effectiveHospitalField} LIMIT 100";
                        var hospitalData = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, hospitalSql);
                        hospitals = hospitalData
                            .Select(row => row.Values.FirstOrDefault()?.ToString() ?? "")
                            .Where(h => !string.IsNullOrEmpty(h))
                            .ToList();
                        _logger.LogInformation("历史会话重放：获取医院列表成功，共 {Count} 家医院", hospitals.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "历史会话重放：获取医院列表失败");
                    }
                }));
            }

            // 等待所有并行任务完成
            if (parallelTasks.Count > 0)
            {
                await Task.WhenAll(parallelTasks);
                _logger.LogInformation("历史会话重放：并行任务完成，共 {Count} 个任务", parallelTasks.Count);
            }

            // ★ 尝试恢复分阶段提示词
            List<PromptInfo>? prompts = null;
            if (!string.IsNullOrEmpty(message.PromptsJson))
            {
                try
                {
                    prompts = System.Text.Json.JsonSerializer.Deserialize<List<PromptInfo>>(message.PromptsJson);
                }
                catch { /* 解析失败则忽略 */ }
            }

            // 构建响应
            var response = new AiChatResponse
            {
                SessionId = message.Session?.SessionKey ?? "",
                MessageId = message.Id,
                Answer = message.Content,
                DetailSql = detailSql,  // ★ 使用替换后的detailSql
                HospitalField = effectiveHospitalField,  // ★ 使用有效的医院字段（可能是自动识别的）
                DateField = message.DateField,
                Dimensions = dimensions,
                Measures = measures,
                Queries = queries,
                Hospitals = hospitals,  // ★ 返回医院列表
                Prompts = prompts,  // ★ 返回分阶段提示词
                PromptText = message.PromptText  // 兼容旧版，便于调试复盘
            };

            return Ok(ApiResponse<AiChatResponse>.Success(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重放会话失败");
            return Ok(ApiResponse<AiChatResponse>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 重放患者360会话 - 重新执行SQL获取最新患者列表
    /// </summary>
    private async Task<ActionResult<ApiResponse<AiChatResponse>>> ReplayHz360SessionAsync(
        Domain.Entities.AiMessage message, long datasourceId)
    {
        try
        {
            var response = new AiChatResponse
            {
                SessionId = message.Session?.SessionKey ?? "",
                MessageId = message.Id,
                Mode = "hz360",
                Answer = message.Content
            };

            // 如果保存了SQL，重新执行获取最新患者列表
            if (!string.IsNullOrEmpty(message.Sql))
            {
                var datasource = await _db.Datasources.FindAsync(datasourceId);
                if (datasource != null)
                {
                    try
                    {
                        var data = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, message.Sql);
                        response.Patients = data.Select(row =>
                        {
                            var birthDateStr = row.GetValueOrDefault("birthdate")?.ToString() ?? row.GetValueOrDefault("出生日期")?.ToString();
                            DateTime? birthDate = null;
                            if (!string.IsNullOrEmpty(birthDateStr) && DateTime.TryParse(birthDateStr, out var bd))
                            {
                                birthDate = bd;
                            }

                            return new Patient360Info
                            {
                                PatientId = row.GetValueOrDefault("patientid")?.ToString() ?? row.GetValueOrDefault("患者ID")?.ToString() ?? "",
                                PatientName = row.GetValueOrDefault("patientname")?.ToString() ?? row.GetValueOrDefault("姓名")?.ToString() ?? "",
                                Gender = row.GetValueOrDefault("gender")?.ToString() ?? row.GetValueOrDefault("性别")?.ToString(),
                                BirthDate = birthDate,
                                IdCard = row.GetValueOrDefault("idcard")?.ToString() ?? row.GetValueOrDefault("身份证号")?.ToString()
                            };
                        }).ToList();

                        if (response.Patients.Count > 0)
                        {
                            response.Answer = $"找到 {response.Patients.Count} 位符合条件的患者，请点击查看详情。";
                        }
                        else
                        {
                            response.Answer = "未找到符合条件的患者。";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "患者360重放：执行SQL失败");
                        response.Answer = $"查询失败：{ex.Message}";
                    }
                }
            }

            // 恢复提示词
            if (!string.IsNullOrEmpty(message.PromptsJson))
            {
                try
                {
                    response.Prompts = System.Text.Json.JsonSerializer.Deserialize<List<PromptInfo>>(message.PromptsJson);
                }
                catch { }
            }

            response.Sql = message.Sql;
            return Ok(ApiResponse<AiChatResponse>.Success(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "患者360重放失败");
            return Ok(ApiResponse<AiChatResponse>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 重放知识问答会话 - 直接返回保存的回答
    /// </summary>
    private async Task<ActionResult<ApiResponse<AiChatResponse>>> ReplayInternetSearchSessionAsync(
        Domain.Entities.AiMessage message)
    {
        try
        {
            var response = new AiChatResponse
            {
                SessionId = message.Session?.SessionKey ?? "",
                MessageId = message.Id,
                Mode = "internetsearch",
                Answer = message.Content
            };

            // 恢复提示词
            if (!string.IsNullOrEmpty(message.PromptsJson))
            {
                try
                {
                    response.Prompts = System.Text.Json.JsonSerializer.Deserialize<List<PromptInfo>>(message.PromptsJson);
                }
                catch { }
            }

            return await Task.FromResult(Ok(ApiResponse<AiChatResponse>.Success(response)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "知识问答重放失败");
            return Ok(ApiResponse<AiChatResponse>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// 重放报表模式会话 - 重新执行SQL获取报表数据
    /// </summary>
    private async Task<ActionResult<ApiResponse<AiChatResponse>>> ReplayReportSessionAsync(
        Domain.Entities.AiMessage message, long datasourceId, string? startDate, string? endDate)
    {
        try
        {
            var datasource = await _db.Datasources.FindAsync(datasourceId);
            if (datasource == null)
                return Ok(ApiResponse<AiChatResponse>.Fail("数据源不存在"));

            var response = new AiChatResponse
            {
                SessionId = message.Session?.SessionKey ?? "",
                MessageId = message.Id,
                Mode = "report",
                Answer = message.Content
            };

            // 恢复提示词
            if (!string.IsNullOrEmpty(message.PromptsJson))
            {
                try
                {
                    response.Prompts = System.Text.Json.JsonSerializer.Deserialize<List<PromptInfo>>(message.PromptsJson);
                }
                catch { }
            }

            // 报表模式replay：由于原始SQL保存在message.Sql中，需要重新执行
            // 但report模式的SQL结构（多Sheet）存储在PromptsJson的AI响应中
            // 简化方案：从message.Sql恢复执行
            if (!string.IsNullOrEmpty(message.Sql))
            {
                var sql = message.Sql;
                if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
                {
                    sql = ReplaceDateParameters(sql, DateTime.Parse(startDate), DateTime.Parse(endDate));
                }

                if (IsSafeSelectSql(sql))
                {
                    var rows = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, sql);
                    var columns = new List<ReportColumnDef>();
                    if (rows.Count > 0)
                    {
                        foreach (var key in rows[0].Keys)
                        {
                            var val = rows[0][key];
                            var isNumeric = val != null && IsNumericValue(val);
                            columns.Add(new ReportColumnDef
                            {
                                Field = key,
                                Title = key,
                                DataType = isNumeric ? "number" : "text",
                                Width = isNumeric ? 120 : 150,
                                Align = isNumeric ? "right" : "left"
                            });
                        }
                    }

                    response.ReportSheets = new List<ReportSheetData>
                    {
                        new ReportSheetData
                        {
                            Title = "数据报表",
                            Columns = columns,
                            Rows = rows,
                            Sql = sql
                        }
                    };
                }
            }

            return Ok(ApiResponse<AiChatResponse>.Success(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "报表重放失败");
            return Ok(ApiResponse<AiChatResponse>.Fail($"报表重放失败: {ex.Message}"));
        }
    }

    #region 图表截图上传

    /// <summary>
    /// 上传图表截图请求
    /// </summary>
    /// <summary>
    /// 图表截图项（包含图片和标题）
    /// </summary>
    public class ChartImageItem
    {
        /// <summary>Base64图片</summary>
        public string Image { get; set; } = string.Empty;
        /// <summary>图表标题</summary>
        public string Title { get; set; } = string.Empty;
    }

    public class UploadChartImagesRequest
    {
        /// <summary>消息ID</summary>
        public long MessageId { get; set; }
        /// <summary>图表图片Base64列表（兼容旧版）</summary>
        public List<string> Images { get; set; } = new();
        /// <summary>新版：带标题的图表截图列表</summary>
        public List<ChartImageItem>? ChartImages { get; set; }
    }

    /// <summary>
    /// 上传图表截图
    /// 前端渲染图表后，将截图上传到服务器保存
    /// 文件命名规则：{会话名称}_{图表标题}_{时间戳}.png
    /// </summary>
    [HttpPost("chart-images")]
    public async Task<IActionResult> UploadChartImages([FromBody] UploadChartImagesRequest request)
    {
        try
        {
            // 优先使用新版带标题的截图列表
            var hasNewFormat = request.ChartImages != null && request.ChartImages.Count > 0;
            var imageCount = hasNewFormat ? request.ChartImages!.Count : request.Images?.Count ?? 0;

            if (imageCount == 0)
            {
                return Ok(ApiResponse<List<string>>.Success(new List<string>()));
            }

            // 获取消息及其所属会话
            var message = await _db.AiMessages
                .Include(m => m.Session)
                .FirstOrDefaultAsync(m => m.Id == request.MessageId);
            if (message == null)
            {
                return Ok(ApiResponse<List<string>>.Fail("消息不存在"));
            }

            // 获取会话名称（用于文件命名）
            var sessionTitle = message.Session?.Title ?? "未命名会话";
            // 清理文件名中的非法字符
            var safeSessionTitle = SanitizeFileName(sessionTitle);

            // 创建上传目录
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "charts");
            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }

            var savedPaths = new List<string>();
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

            for (int i = 0; i < imageCount; i++)
            {
                string base64;
                string chartTitle;

                if (hasNewFormat)
                {
                    // 新版：使用带标题的截图列表
                    base64 = request.ChartImages![i].Image;
                    chartTitle = request.ChartImages[i].Title;
                }
                else
                {
                    // 兼容旧版：使用纯图片列表
                    base64 = request.Images![i];
                    chartTitle = $"图表{i + 1}";
                }

                if (string.IsNullOrEmpty(base64)) continue;

                try
                {
                    // 移除Base64前缀（如 "data:image/png;base64,"）
                    var base64Data = base64;
                    if (base64Data.Contains(","))
                    {
                        base64Data = base64Data.Split(',')[1];
                    }

                    var imageBytes = Convert.FromBase64String(base64Data);

                    // 新命名规则：{会话名称}_{图表标题}_{时间戳}.png
                    var safeChartTitle = SanitizeFileName(chartTitle);
                    var fileName = $"{safeSessionTitle}_{safeChartTitle}_{timestamp}_{i}.png";
                    var filePath = Path.Combine(uploadsDir, fileName);

                    await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

                    // 返回相对路径（用于前端访问）
                    var relativePath = $"/uploads/charts/{fileName}";
                    savedPaths.Add(relativePath);

                    _logger.LogDebug("保存图表截图: {FileName}", fileName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "保存图表图片失败: Index={Index}, Title={Title}", i, chartTitle);
                }
            }

            // 更新消息的ChartImages字段（追加模式，不覆盖已有图片）
            var existingImages = new List<string>();
            if (!string.IsNullOrEmpty(message.ChartImages))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<List<string>>(message.ChartImages);
                    if (parsed != null)
                    {
                        existingImages = parsed;
                    }
                }
                catch { /* 忽略解析错误 */ }
            }

            // 追加新保存的图片路径
            existingImages.AddRange(savedPaths);
            message.ChartImages = JsonSerializer.Serialize(existingImages);
            await _db.SaveChangesAsync();

            _logger.LogInformation("图表截图上传成功: SessionTitle={SessionTitle}, MessageId={MessageId}, NewCount={NewCount}, TotalCount={TotalCount}",
                sessionTitle, request.MessageId, savedPaths.Count, existingImages.Count);

            return Ok(ApiResponse<List<string>>.Success(savedPaths, "图表截图上传成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上传图表截图失败");
            return Ok(ApiResponse<List<string>>.Fail($"上传失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 清理文件名中的非法字符
    /// </summary>
    /// <summary>
    /// 从标题中提取关键词（用于图片匹配）
    /// </summary>
    private static List<string> ExtractKeywords(string title)
    {
        if (string.IsNullOrEmpty(title)) return new List<string>();
        var result = new List<string>();
        var buffer = new System.Text.StringBuilder();
        foreach (var c in title)
        {
            if (c >= 0x4E00 && c <= 0x9FFF)
            {
                buffer.Append(c);
            }
            else
            {
                if (buffer.Length >= 2) result.Add(buffer.ToString());
                buffer.Clear();
            }
        }
        if (buffer.Length >= 2) result.Add(buffer.ToString());
        return result;
    }

    /// <summary>
    /// 从图片路径中提取图表标题
    /// 文件名格式：{会话标题}_{图表标题}_{时间戳}_{序号}.png
    /// </summary>
    private static string ExtractChartTitleFromPath(string imagePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(imagePath);
        var parts = fileName.Split('_');
        if (parts.Length >= 4)
        {
            // 中间部分（去掉第一段会话标题、最后两段时间戳+序号）就是图表标题
            var titleParts = parts.Skip(1).Take(parts.Length - 3);
            return string.Join("", titleParts);
        }
        return fileName;
    }

    /// <summary>
    /// 为幻灯片匹配最佳图片
    /// 优先级：1.精确匹配 2.子串匹配 3.关键词匹配 4.未使用过的图片
    /// </summary>
    private static string? MatchBestImage(string slideTitle, List<string> imagePaths, HashSet<string> usedImages, ILogger logger)
    {
        if (imagePaths == null || imagePaths.Count == 0) return null;

        var candidates = imagePaths
            .Select(p => new { Path = p, Title = ExtractChartTitleFromPath(p) })
            .ToList();

        // 策略1：精确匹配 - 幻灯片标题包含图片标题，或图片标题包含幻灯片标题
        foreach (var c in candidates)
        {
            if (usedImages.Contains(c.Path)) continue;
            var cleanSlideTitle = slideTitle.Replace(" ", "").Replace("：", "").Replace(":", "");
            var cleanChartTitle = c.Title.Replace(" ", "").Replace("：", "").Replace(":", "");
            if (cleanSlideTitle.Equals(cleanChartTitle, StringComparison.OrdinalIgnoreCase))
            {
                usedImages.Add(c.Path);
                return c.Path;
            }
        }

        // 策略2：子串匹配
        foreach (var c in candidates)
        {
            if (usedImages.Contains(c.Path)) continue;
            var cleanSlideTitle = slideTitle.Replace(" ", "").Replace("：", "").Replace(":", "");
            var cleanChartTitle = c.Title.Replace(" ", "").Replace("：", "").Replace(":", "");
            if (cleanSlideTitle.Contains(cleanChartTitle) || cleanChartTitle.Contains(cleanSlideTitle))
            {
                usedImages.Add(c.Path);
                return c.Path;
            }
        }

        // 策略3：关键词匹配 - 计算关键词重叠度
        var slideKeywords = ExtractKeywords(slideTitle);
        var bestMatch = candidates
            .Where(c => !usedImages.Contains(c.Path))
            .Select(c => new
            {
                c.Path,
                c.Title,
                Score = ExtractKeywords(c.Title).Intersect(slideKeywords).Count()
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (bestMatch != null)
        {
            usedImages.Add(bestMatch.Path);
            logger.LogInformation("图片关键词匹配: SlideTitle={SlideTitle}, ChartTitle={ChartTitle}, Score={Score}",
                slideTitle, bestMatch.Title, bestMatch.Score);
            return bestMatch.Path;
        }

        // 策略4：使用第一个未使用的图片
        var unused = candidates.FirstOrDefault(c => !usedImages.Contains(c.Path));
        if (unused != null)
        {
            usedImages.Add(unused.Path);
            logger.LogInformation("图片兜底匹配(未使用图片): SlideTitle={SlideTitle}, ChartTitle={ChartTitle}",
                slideTitle, unused.Title);
            return unused.Path;
        }

        // 所有图片都用完了，返回第一张
        logger.LogWarning("所有图片已分配完，无图片可用: SlideTitle={SlideTitle}", slideTitle);
        return imagePaths.FirstOrDefault();
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return "未命名";

        // 移除或替换文件名中的非法字符
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Where(c => !invalidChars.Contains(c))
            .ToArray());

        // 限制长度，避免文件名过长
        if (sanitized.Length > 50)
        {
            sanitized = sanitized.Substring(0, 50);
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "未命名" : sanitized.Trim();
    }

    #endregion

    #region PPT生成接口

    /// <summary>
    /// 生成PPT大纲
    /// 根据选中的会话历史，调用大模型生成结构化的PPT大纲
    /// </summary>
    [HttpPost("ppt/outline")]
    public async Task<IActionResult> GeneratePptOutline([FromBody] PptOutlineRequest request)
    {
        try
        {
            if (request.SessionIds == null || request.SessionIds.Count == 0)
            {
                return Ok(ApiResponse<PptOutlineResponse>.Fail("请至少选择一个会话"));
            }

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Ok(ApiResponse<PptOutlineResponse>.Fail("请输入PPT标题"));
            }

            // 获取选中会话的消息内容
            var sessions = await _db.AiSessions
                .Where(s => request.SessionIds.Contains(s.Id))
                .Include(s => s.Messages)
                .ToListAsync();

            if (sessions.Count == 0)
            {
                return Ok(ApiResponse<PptOutlineResponse>.Fail("未找到选中的会话"));
            }

            // 构建会话内容摘要
            var contentBuilder = new StringBuilder();
            // 扩展元组，添加ChartImages字段用于收集已保存的图片
            var chartMessages = new List<(long MessageId, string Title, string? ChartConfig, string? KpiConfig, string? ChartImages)>();

            foreach (var session in sessions)
            {
                contentBuilder.AppendLine($"## 会话：{session.Title}");
                foreach (var msg in session.Messages.OrderBy(m => m.CreatedAt))
                {
                    if (msg.Role == "user")
                    {
                        contentBuilder.AppendLine($"问题：{msg.Content}");
                    }
                    else if (msg.Role == "assistant")
                    {
                        contentBuilder.AppendLine($"回答：{msg.Content}");
                        // 收集有图表配置、图表截图或KPI配置的消息
                        // 新模式：DefaultChartsConfig + ChartImages + KpiConfig
                        // 旧模式：ChartConfig
                        var hasChart = !string.IsNullOrEmpty(msg.ChartConfig) ||
                            !string.IsNullOrEmpty(msg.DefaultChartsConfig) ||
                            !string.IsNullOrEmpty(msg.ChartImages);
                        var hasKpi = !string.IsNullOrEmpty(msg.KpiConfig);

                        if (hasChart || hasKpi)
                        {
                            chartMessages.Add((
                                msg.Id,
                                msg.Content?.Split('\n').FirstOrDefault() ?? "图表",
                                msg.ChartConfig ?? msg.DefaultChartsConfig,
                                msg.KpiConfig,
                                msg.ChartImages  // 添加已保存的图片路径
                            ));
                        }
                    }
                }
                contentBuilder.AppendLine();
            }

            // 构建Prompt让大模型生成PPT大纲（包含智能布局推荐）
            var systemPrompt = @"你是一个专业的PPT大纲生成助手。根据用户提供的对话历史和主题，生成一个结构清晰的PPT大纲。

要求：
1. 第一页必须是封面页（type: title），包含标题
2. 根据内容合理安排内容页（type: content）、图表页（type: chart）和指标页（type: kpi）
3. 如果有KPI指标数据，创建指标页展示关键数值
4. 如果有图表数据，在合适的位置插入图表页
5. [重要] 图表页的title必须与已保存的图表截图中列出的图片标题完全一致，这样才能自动匹配嵌入对应图片
6. [重要] KPI指标页的title必须使用可用KPI列表中提供的具体标题
7. [重要] 每张图片只用于一个图表页，不要多页共用同一张图片。如果有多张图片，应为每张图片创建单独的图表页
8. 最后一页是总结页（type: summary）
9. 每页的要点（points）控制在3-5条
10. [智能布局] 为每页推荐合适的布局模板（layout字段）
11. 返回JSON格式，不要有其他内容

可用的布局模板：
- 封面页(title): centered-title(居中封面), left-title(左对齐封面)
- 内容页(content): bullets-left(左侧要点), two-column(双栏布局，适合要点多于5条), bullets-centered(居中要点)
- 图表页(chart): full-image(全幅图表), image-left-text-right(左图右文，适合有说明文字), image-right-text-left(左文右图), image-top-text-bottom(上图下文)
- 指标页(kpi): three-kpi(三指标卡片), four-kpi(四指标卡片), kpi-with-chart(指标+图表)
- 总结页(summary): summary-points(总结要点), summary-centered(居中总结)

布局选择建议：
- 封面页：标题较长用left-title，标题简短用centered-title
- 内容页：要点超过5条用two-column，否则用bullets-left
- 图表页：有详细说明用image-left-text-right，纯图表用full-image
- 指标页：3个指标用three-kpi，4个指标用four-kpi
- 总结页：多条总结用summary-points，简短总结用summary-centered

JSON格式示例：
{
  ""title"": ""PPT标题"",
  ""slides"": [
    {
      ""order"": 1,
      ""title"": ""封面标题"",
      ""type"": ""title"",
      ""layout"": ""centered-title"",
      ""points"": [""副标题或日期""],
      ""notes"": ""讲稿备注""
    },
    {
      ""order"": 2,
      ""title"": ""核心指标概览"",
      ""type"": ""kpi"",
      ""layout"": ""three-kpi"",
      ""points"": [""指标说明""],
      ""messageId"": 123,
      ""notes"": ""讲稿备注""
    },
    {
      ""order"": 3,
      ""title"": ""【使用可用图表列表中的标题】"",
      ""type"": ""chart"",
      ""layout"": ""full-image"",
      ""points"": [""图表说明""],
      ""messageId"": 123,
      ""notes"": ""讲稿备注""
    },
    {
      ""order"": 4,
      ""title"": ""内容页标题"",
      ""type"": ""content"",
      ""layout"": ""bullets-left"",
      ""points"": [""要点1"", ""要点2"", ""要点3""],
      ""notes"": ""讲稿备注""
    }
  ]
}";

            // 分离图表和KPI消息
            var chartOnlyMessages = chartMessages.Where(c => !string.IsNullOrEmpty(c.ChartConfig)).ToList();
            var kpiOnlyMessages = chartMessages.Where(c => !string.IsNullOrEmpty(c.KpiConfig)).ToList();

            // 收集有已保存图片的消息（用于告知AI有哪些图片可用）
            var messagesWithImages = new List<(long MessageId, string Title, List<string> ImagePaths)>();
            foreach (var cm in chartMessages.Where(c => !string.IsNullOrEmpty(c.ChartImages)))
            {
                try
                {
                    var imagePaths = JsonSerializer.Deserialize<List<string>>(cm.ChartImages!);
                    if (imagePaths != null && imagePaths.Count > 0)
                    {
                        // 从图片路径中提取文件名作为图片描述
                        var imageDescriptions = imagePaths.Select(p => Path.GetFileNameWithoutExtension(p)).ToList();
                        messagesWithImages.Add((cm.MessageId, cm.Title, imagePaths));
                    }
                }
                catch { /* 忽略解析错误 */ }
            }

            // 构建已保存图片的描述
            var savedImagesInfo = new StringBuilder();
            if (messagesWithImages.Count > 0)
            {
                savedImagesInfo.AppendLine("已保存的图表截图（这些图片将自动嵌入到对应的图表页中）：");
                foreach (var mi in messagesWithImages)
                {
                    savedImagesInfo.AppendLine($"- MessageId: {mi.MessageId}, 标题: {mi.Title}, 图片数量: {mi.ImagePaths.Count}张");
                    foreach (var path in mi.ImagePaths)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(path);
                        // 文件名格式：{会话标题}_{图表标题}_{时间戳}_{序号}
                        var parts = fileName.Split('_');
                        var chartTitle = parts.Length >= 2 ? string.Join(" ", parts.Skip(1).Take(parts.Length - 3)) : fileName;
                        savedImagesInfo.AppendLine($"  - 图片标题: {chartTitle} (文件名: {fileName})");
                    }
                }
            }
            else
            {
                savedImagesInfo.AppendLine("已保存的图表截图：无（用户尚未保存任何图表截图）");
            }

            var userPrompt = $@"请根据以下对话历史，生成主题为""{request.Title}""的PPT大纲。

{(string.IsNullOrEmpty(request.Idea) ? "" : $"用户要求：{request.Idea}\n\n")}对话历史：
{contentBuilder}

可用的图表（可在大纲中使用type:chart引用messageId）：
{(chartOnlyMessages.Count > 0 ? string.Join("\n", chartOnlyMessages.Select(c => $"- MessageId: {c.MessageId}, 标题: {c.Title}")) : "无")}

可用的KPI指标（可在大纲中使用type:kpi引用messageId）：
{(kpiOnlyMessages.Count > 0 ? string.Join("\n", kpiOnlyMessages.Select(c => $"- MessageId: {c.MessageId}, 标题: {c.Title}")) : "无")}

{savedImagesInfo}

请生成PPT大纲（JSON格式）：";

            var messages = new List<LlmMessage>
            {
                LlmMessage.System(systemPrompt),
                LlmMessage.User(userPrompt)
            };

            var llmResponse = await _llmService.ChatAsync(messages, new LlmOptions
            {
                Temperature = 0.7,
                MaxTokens = 4096,
                BusinessType = AiBusinessType.DocGen  // PPT生成使用文档生成配置
            });

            if (!llmResponse.Success)
            {
                return Ok(ApiResponse<PptOutlineResponse>.Fail($"大模型调用失败: {llmResponse.Error}"));
            }

            // 解析JSON响应
            var content = llmResponse.Content;
            if (content.Contains("```"))
            {
                content = Regex.Replace(content, @"```json\s*", "");
                content = Regex.Replace(content, @"```\s*", "");
            }
            content = content.Trim();

            var outline = JsonSerializer.Deserialize<PptOutlineResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (outline == null)
            {
                return Ok(ApiResponse<PptOutlineResponse>.Fail("大纲解析失败"));
            }

            // 补充图表数据和图表截图URL（智能匹配：根据slide标题匹配最合适的图片）
            var usedImageIndices = new HashSet<string>(); // 跟踪已使用的图片，避免重复分配
            foreach (var slide in outline.Slides.Where(s => s.Type == "chart" && s.MessageId.HasValue))
            {
                var chartMsg = chartMessages.FirstOrDefault(c => c.MessageId == slide.MessageId);
                if (chartMsg != default)
                {
                    slide.ChartConfig = chartMsg.ChartConfig;
                }

                try
                {
                    var msg = await _db.AiMessages.FindAsync(slide.MessageId!.Value);
                    if (msg != null && !string.IsNullOrEmpty(msg.ChartImages))
                    {
                        var chartImages = JsonSerializer.Deserialize<List<string>>(msg.ChartImages);
                        if (chartImages != null && chartImages.Count > 0)
                        {
                            var matchedImage = MatchBestImage(slide.Title, chartImages, usedImageIndices, _logger);
                            slide.ChartImageUrls = matchedImage != null ? new List<string> { matchedImage } : null;
                            _logger.LogInformation("图表幻灯片匹配图片: MessageId={MessageId}, Title={SlideTitle}, Matched={Matched}",
                                slide.MessageId, slide.Title, matchedImage ?? "无");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "获取图表截图URL失败: MessageId={MessageId}", slide.MessageId);
                }
            }

            // 补充KPI幻灯片的图片URL（智能匹配）
            foreach (var slide in outline.Slides.Where(s => s.Type == "kpi" && s.MessageId.HasValue))
            {
                try
                {
                    var msg = await _db.AiMessages.FindAsync(slide.MessageId!.Value);
                    if (msg != null && !string.IsNullOrEmpty(msg.ChartImages))
                    {
                        var chartImages = JsonSerializer.Deserialize<List<string>>(msg.ChartImages);
                        if (chartImages != null && chartImages.Count > 0)
                        {
                            var matchedImage = MatchBestImage(slide.Title, chartImages, usedImageIndices, _logger);
                            slide.ChartImageUrls = matchedImage != null ? new List<string> { matchedImage } : null;
                            _logger.LogInformation("KPI幻灯片匹配图片: MessageId={MessageId}, Title={SlideTitle}, Matched={Matched}",
                                slide.MessageId, slide.Title, matchedImage ?? "无");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "获取KPI截图URL失败: MessageId={MessageId}", slide.MessageId);
                }
            }

            // 补充KPI指标数据 - 需要执行SQL获取实际值
            // 获取数据源连接
            var datasource = request.DatasourceId > 0
                ? await _db.Datasources.FindAsync(request.DatasourceId)
                : null;

            foreach (var slide in outline.Slides.Where(s => s.Type == "kpi" && s.MessageId.HasValue))
            {
                try
                {
                    var msg = await _db.AiMessages.FindAsync(slide.MessageId!.Value);
                    if (msg != null && !string.IsNullOrEmpty(msg.KpiConfig))
                    {
                        // KpiConfig格式为: [{Title, SqlTemplate}, ...]
                        var kpiConfigs = JsonSerializer.Deserialize<List<KpiConfig>>(msg.KpiConfig);
                        if (kpiConfigs != null && kpiConfigs.Count > 0)
                        {
                            slide.KpiCards = new List<KpiCardData>();
                            foreach (var kpi in kpiConfigs)
                            {
                                var card = new KpiCardData
                                {
                                    Title = kpi.Title,
                                    Value = "N/A",  // 默认值
                                    Unit = null
                                };

                                // 如果有数据源和SQL模板，执行SQL获取实际值
                                if (datasource != null && !string.IsNullOrEmpty(kpi.SqlTemplate))
                                {
                                    try
                                    {
                                        // 替换SQL模板中的占位符
                                        var kpiSql = kpi.SqlTemplate;
                                        if (!string.IsNullOrEmpty(msg.DetailSql) && kpiSql.Contains("(...)"))
                                        {
                                            kpiSql = kpiSql.Replace("(...)", $"({msg.DetailSql})");
                                        }

                                        var kpiData = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, kpiSql);
                                        if (kpiData.Count > 0)
                                        {
                                            // 获取第一行数据的value字段
                                            var firstRow = kpiData[0];
                                            if (firstRow.TryGetValue("value", out var val) && val != null)
                                            {
                                                // 格式化数值
                                                if (decimal.TryParse(val.ToString(), out var numVal))
                                                {
                                                    card.Value = numVal.ToString("N0");  // 千分位格式
                                                }
                                                else
                                                {
                                                    card.Value = val.ToString() ?? "N/A";
                                                }
                                            }
                                        }
                                        _logger.LogInformation("KPI查询成功: {Title} = {Value}", kpi.Title, card.Value);
                                    }
                                    catch (Exception sqlEx)
                                    {
                                        _logger.LogWarning(sqlEx, "KPI SQL执行失败: {Title}", kpi.Title);
                                        card.Value = "查询失败";
                                    }
                                }

                                slide.KpiCards.Add(card);
                            }
                            _logger.LogInformation("KPI幻灯片获取到指标数据: MessageId={MessageId}, Count={Count}",
                                slide.MessageId, slide.KpiCards.Count);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "获取KPI指标数据失败: MessageId={MessageId}", slide.MessageId);
                }
            }

            outline.SystemPrompt = systemPrompt;
            outline.UserPrompt = userPrompt;

            return Ok(ApiResponse<PptOutlineResponse>.Success(outline));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成PPT大纲失败");
            return Ok(ApiResponse<PptOutlineResponse>.Fail($"生成失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 优化PPT大纲
    /// 根据用户提示词对现有大纲进行优化调整
    /// 支持全局优化和单页优化两种模式
    /// </summary>
    [HttpPost("ppt/optimize")]
    public async Task<IActionResult> OptimizePptOutline([FromBody] PptOptimizeRequest request)
    {
        try
        {
            if (request.Outline?.Slides == null || request.Outline.Slides.Count == 0)
            {
                return Ok(ApiResponse<PptOutlineResponse>.Fail("大纲不能为空"));
            }

            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return Ok(ApiResponse<PptOutlineResponse>.Fail("请输入优化提示词"));
            }

            // 判断是单页优化还是全局优化
            var isSingleSlide = request.Mode == "single" && request.SlideIndex.HasValue
                && request.SlideIndex.Value >= 0
                && request.SlideIndex.Value < request.Outline.Slides.Count;

            string systemPrompt;
            string userPrompt;
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            if (isSingleSlide)
            {
                // 单页优化模式
                var targetSlide = request.Outline.Slides[request.SlideIndex!.Value];
                var slideJson = JsonSerializer.Serialize(targetSlide, jsonOptions);

                systemPrompt = @"你是一个专业的PPT幻灯片优化助手。根据用户的优化要求，对单张幻灯片进行调整和优化。

要求：
1. 保持幻灯片类型（type）不变，除非用户明确要求更改
2. 可以调整标题、要点内容、布局（layout）等
3. 保留原有的messageId、chartConfig、kpiCards等技术字段不变
4. 返回优化后的单张幻灯片JSON，不要有其他内容
5. 如果用户要求更换布局，从以下选项中选择合适的：
   - title类型: centered-title(居中封面), left-title(左对齐封面)
   - content类型: bullets-left(左侧要点), two-column(双栏布局), bullets-centered(居中要点)
   - chart类型: full-image(全幅图表), image-left-text-right(左图右文), image-right-text-left(左文右图), image-top-text-bottom(上图下文)
   - kpi类型: three-kpi(三指标卡片), four-kpi(四指标网格), kpi-with-chart(指标+图表)
   - summary类型: summary-points(总结要点), summary-centered(居中总结)

返回格式为单个幻灯片对象。";

                userPrompt = $@"请根据以下优化要求，调整这张幻灯片：

## 优化要求
{request.Prompt}

## 当前幻灯片（第{request.SlideIndex.Value + 1}页）
{slideJson}

请返回优化后的幻灯片（JSON格式）：";
            }
            else
            {
                // 全局优化模式
                var currentOutlineJson = JsonSerializer.Serialize(request.Outline, jsonOptions);

                systemPrompt = @"你是一个专业的PPT大纲优化助手。根据用户的优化要求，对现有PPT大纲进行调整和优化。

要求：
1. 保持原有的幻灯片类型（title/content/chart/kpi/summary）不变，除非用户明确要求
2. 可以调整标题、要点内容、顺序、布局（layout）等
3. 保留原有的messageId、chartConfig、kpiCards等技术字段
4. 返回完整的优化后JSON格式大纲，不要有其他内容
5. 如果用户要求添加新幻灯片，可以添加（type为content）
6. 如果用户要求删除某些内容，可以删除对应幻灯片
7. 布局选项参考：
   - title类型: centered-title, left-title
   - content类型: bullets-left, two-column, bullets-centered
   - chart类型: full-image, image-left-text-right, image-right-text-left, image-top-text-bottom
   - kpi类型: three-kpi, four-kpi, kpi-with-chart
   - summary类型: summary-points, summary-centered

返回格式与输入格式相同。";

                userPrompt = $@"请根据以下优化要求，调整PPT大纲：

## 优化要求
{request.Prompt}

## 当前大纲
{currentOutlineJson}

请返回优化后的完整大纲（JSON格式）：";
            }

            var messages = new List<LlmMessage>
            {
                LlmMessage.System(systemPrompt),
                LlmMessage.User(userPrompt)
            };

            var llmResponse = await _llmService.ChatAsync(messages, new LlmOptions
            {
                Temperature = 0.7,
                MaxTokens = 4096
            });

            if (!llmResponse.Success)
            {
                return Ok(ApiResponse<PptOutlineResponse>.Fail($"大模型调用失败: {llmResponse.Error}"));
            }

            // 解析JSON响应
            var content = llmResponse.Content;
            if (content.Contains("```"))
            {
                content = Regex.Replace(content, @"```json\s*", "");
                content = Regex.Replace(content, @"```\s*", "");
            }
            content = content.Trim();

            PptOutlineResponse optimizedOutline;

            if (isSingleSlide)
            {
                // 单页优化：解析单个幻灯片，然后替换到原大纲中
                var optimizedSlide = JsonSerializer.Deserialize<PptSlide>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (optimizedSlide == null)
                {
                    return Ok(ApiResponse<PptOutlineResponse>.Fail("优化结果解析失败"));
                }

                // 保留原有技术字段
                var originalSlide = request.Outline.Slides[request.SlideIndex!.Value];
                optimizedSlide.MessageId = originalSlide.MessageId;
                optimizedSlide.ChartConfig = originalSlide.ChartConfig;
                optimizedSlide.ChartImageBase64 = originalSlide.ChartImageBase64;
                optimizedSlide.ChartImageUrls = originalSlide.ChartImageUrls;
                optimizedSlide.KpiCards = originalSlide.KpiCards;
                optimizedSlide.Order = originalSlide.Order;

                // 替换到原大纲
                optimizedOutline = request.Outline;
                optimizedOutline.Slides[request.SlideIndex.Value] = optimizedSlide;

                _logger.LogInformation("PPT单页优化成功，第{Index}页", request.SlideIndex.Value + 1);
            }
            else
            {
                // 全局优化：解析完整大纲
                optimizedOutline = JsonSerializer.Deserialize<PptOutlineResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (optimizedOutline == null)
                {
                    return Ok(ApiResponse<PptOutlineResponse>.Fail("优化结果解析失败"));
                }

                _logger.LogInformation("PPT大纲全局优化成功，幻灯片数量: {Count}", optimizedOutline.Slides.Count);
            }

            return Ok(ApiResponse<PptOutlineResponse>.Success(optimizedOutline));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "优化PPT大纲失败");
            return Ok(ApiResponse<PptOutlineResponse>.Fail($"优化失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 生成PPT文件
    /// 根据大纲生成PPTX文件并返回下载
    /// </summary>
    [HttpPost("ppt/generate")]
    public async Task<IActionResult> GeneratePptFile([FromBody] PptGenerateRequest request)
    {
        try
        {
            if (request.Outline?.Slides == null || request.Outline.Slides.Count == 0)
            {
                return Ok(ApiResponse<string>.Fail("PPT大纲不能为空"));
            }

            // 获取数据源连接（用于执行图表SQL）
            var datasource = request.DatasourceId > 0
                ? await _db.Datasources.FindAsync(request.DatasourceId)
                : null;

            // 为图表页和KPI页获取数据和图片
            // 支持 chart 和 kpi 类型的幻灯片
            foreach (var slide in request.Outline.Slides.Where(s => (s.Type == "chart" || s.Type == "kpi") && s.MessageId.HasValue))
            {
                try
                {
                    // 获取消息
                    var message = await _db.AiMessages.FindAsync(slide.MessageId!.Value);
                    if (message != null)
                    {
                        // 优先使用保存的图表截图
                        if (!string.IsNullOrEmpty(message.ChartImages))
                        {
                            var chartImages = JsonSerializer.Deserialize<List<string>>(message.ChartImages);
                            if (chartImages != null && chartImages.Count > 0)
                            {
                                // 根据幻灯片标题匹配对应的图片（多策略匹配）
                                string? matchedImagePath = null;
                                var bestScore = 0;

                                foreach (var imgPath in chartImages)
                                {
                                    var fileName = Path.GetFileNameWithoutExtension(imgPath);
                                    // 文件名格式：{会话标题}_{图表标题}_{时间戳}_{序号}
                                    var parts = fileName.Split('_');
                                    var chartTitle = parts.Length >= 2 ? string.Join("", parts.Skip(1).Take(parts.Length - 3)) : fileName;

                                    if (string.IsNullOrEmpty(slide.Title)) continue;

                                    // 策略1：完全匹配
                                    if (chartTitle == slide.Title.Replace(" ", ""))
                                    {
                                        matchedImagePath = imgPath;
                                        break;
                                    }

                                    // 策略2：标题包含在图片标题中，或反之
                                    var slideTitleClean = slide.Title.Replace(" ", "");
                                    var chartTitleClean = chartTitle.Replace(" ", "");
                                    if (chartTitleClean.Contains(slideTitleClean) || slideTitleClean.Contains(chartTitleClean))
                                    {
                                        var score = Math.Min(chartTitleClean.Length, slideTitleClean.Length);
                                        if (score > bestScore)
                                        {
                                            bestScore = score;
                                            matchedImagePath = imgPath;
                                        }
                                    }

                                    // 策略3：关键词匹配（从标题提取2字以上的关键词）
                                    if (matchedImagePath == null)
                                    {
                                        var slideKeywords = ExtractKeywords(slide.Title);
                                        var matchCount = slideKeywords.Count(kw => chartTitle.Contains(kw));
                                        if (matchCount >= Math.Max(2, slideKeywords.Count / 2) && matchCount > bestScore)
                                        {
                                            bestScore = matchCount;
                                            matchedImagePath = imgPath;
                                        }
                                    }
                                }

                                // 如果没有匹配到，使用第一张图片
                                if (string.IsNullOrEmpty(matchedImagePath))
                                {
                                    matchedImagePath = chartImages[0];
                                }

                                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", matchedImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                                if (System.IO.File.Exists(fullPath))
                                {
                                    var imageBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
                                    slide.ChartImageBase64 = Convert.ToBase64String(imageBytes);
                                    _logger.LogInformation("使用保存的图表截图: Type={Type}, MessageId={MessageId}, Path={Path}", slide.Type, slide.MessageId, matchedImagePath);
                                }
                            }
                        }

                        // 如果没有截图，获取SQL数据作为备用
                        if (string.IsNullOrEmpty(slide.ChartImageBase64) && !string.IsNullOrEmpty(message.Sql) && datasource != null)
                        {
                            var data = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, message.Sql);
                            if (data.Count > 0)
                            {
                                slide.ChartData = data;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "获取图表数据失败: MessageId={MessageId}", slide.MessageId);
                    // 继续处理，不中断PPT生成
                }
            }

            // 使用ShapeCrawler方案生成PPT
            _logger.LogInformation("使用ShapeCrawler生成PPT");
            var pptBytes = _pptGenerator.GeneratePptx(request.Outline, request.Template, request.PptTitle);

            // 返回base64编码的文件内容
            var base64 = Convert.ToBase64String(pptBytes);
            return Ok(ApiResponse<string>.Success(base64, "PPT生成成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成PPT文件失败");
            return Ok(ApiResponse<string>.Fail($"生成失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 使用OpenXML SDK生成PPTX文件
    /// </summary>
    private async Task<byte[]> GeneratePptxAsync(PptOutlineResponse outline, string template)
    {
        await Task.CompletedTask; // 异步占位

        using var ms = new MemoryStream();
        using (var ppt = DocumentFormat.OpenXml.Packaging.PresentationDocument.Create(ms, DocumentFormat.OpenXml.PresentationDocumentType.Presentation))
        {
            // 创建演示文稿部分
            var presentationPart = ppt.AddPresentationPart();
            presentationPart.Presentation = new DocumentFormat.OpenXml.Presentation.Presentation();

            // 1. 创建必要的SlideMaster、SlideLayout和Theme
            CreatePresentationParts(presentationPart, template);

            // 2. 创建幻灯片大小（16:9）
            presentationPart.Presentation.SlideSize = new DocumentFormat.OpenXml.Presentation.SlideSize
            {
                Cx = 12192000, // 宽度 (EMU)
                Cy = 6858000  // 高度 (EMU)
            };
            presentationPart.Presentation.NotesSize = new DocumentFormat.OpenXml.Presentation.NotesSize
            {
                Cx = 6858000,
                Cy = 9144000
            };

            // 3. 添加幻灯片
            uint slideId = 256;
            foreach (var slideData in outline.Slides.OrderBy(s => s.Order))
            {
                // 创建幻灯片部分
                var slidePart = presentationPart.AddNewPart<DocumentFormat.OpenXml.Packaging.SlidePart>();

                // 关联到SlideLayout
                var slideLayoutPart = presentationPart.SlideMasterParts.First().SlideLayoutParts.First();
                slidePart.AddPart(slideLayoutPart, "rId1");

                // 根据类型创建不同的幻灯片
                switch (slideData.Type)
                {
                    case "title":
                        CreateTitleSlideOpenXml(slidePart, slideData, template);
                        break;
                    case "chart":
                        CreateChartSlideOpenXml(slidePart, slideData, template);
                        break;
                    case "summary":
                        CreateSummarySlideOpenXml(slidePart, slideData, template);
                        break;
                    default:
                        CreateContentSlideOpenXml(slidePart, slideData, template);
                        break;
                }

                // 添加幻灯片ID到列表
                presentationPart.Presentation.SlideIdList!.Append(new DocumentFormat.OpenXml.Presentation.SlideId
                {
                    Id = slideId++,
                    RelationshipId = presentationPart.GetIdOfPart(slidePart)
                });
            }

            presentationPart.Presentation.Save();
        }

        return ms.ToArray();
    }

    /// <summary>
    /// 创建PPT必要的部件：SlideMaster、SlideLayout、Theme
    /// </summary>
    private void CreatePresentationParts(DocumentFormat.OpenXml.Packaging.PresentationPart presentationPart, string template)
    {
        var (titleColorHex, _) = GetTemplateColorsHex(template);

        // 创建SlideMasterIdList和SlideIdList
        var slideMasterIdList = new DocumentFormat.OpenXml.Presentation.SlideMasterIdList(
            new DocumentFormat.OpenXml.Presentation.SlideMasterId() { Id = 2147483648U, RelationshipId = "rId1" });
        var slideIdList = new DocumentFormat.OpenXml.Presentation.SlideIdList();
        var defaultTextStyle = new DocumentFormat.OpenXml.Presentation.DefaultTextStyle();

        presentationPart.Presentation.Append(slideMasterIdList, slideIdList, defaultTextStyle);

        // 创建SlideMasterPart
        var slideMasterPart = presentationPart.AddNewPart<DocumentFormat.OpenXml.Packaging.SlideMasterPart>("rId1");

        // 创建SlideLayoutPart
        var slideLayoutPart = slideMasterPart.AddNewPart<DocumentFormat.OpenXml.Packaging.SlideLayoutPart>("rId1");
        slideLayoutPart.SlideLayout = new DocumentFormat.OpenXml.Presentation.SlideLayout(
            new DocumentFormat.OpenXml.Presentation.CommonSlideData(
                new DocumentFormat.OpenXml.Presentation.ShapeTree(
                    new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeProperties(
                        new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties() { Id = 1U, Name = "" },
                        new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeDrawingProperties(),
                        new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties()),
                    new DocumentFormat.OpenXml.Presentation.GroupShapeProperties(
                        new DocumentFormat.OpenXml.Drawing.TransformGroup(
                            new DocumentFormat.OpenXml.Drawing.Offset { X = 0L, Y = 0L },
                            new DocumentFormat.OpenXml.Drawing.Extents { Cx = 0L, Cy = 0L },
                            new DocumentFormat.OpenXml.Drawing.ChildOffset { X = 0L, Y = 0L },
                            new DocumentFormat.OpenXml.Drawing.ChildExtents { Cx = 0L, Cy = 0L })))),
            new DocumentFormat.OpenXml.Presentation.ColorMapOverride(new DocumentFormat.OpenXml.Drawing.MasterColorMapping()));

        // 创建SlideMaster
        slideMasterPart.SlideMaster = new DocumentFormat.OpenXml.Presentation.SlideMaster(
            new DocumentFormat.OpenXml.Presentation.CommonSlideData(
                new DocumentFormat.OpenXml.Presentation.ShapeTree(
                    new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeProperties(
                        new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties() { Id = 1U, Name = "" },
                        new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeDrawingProperties(),
                        new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties()),
                    new DocumentFormat.OpenXml.Presentation.GroupShapeProperties(
                        new DocumentFormat.OpenXml.Drawing.TransformGroup(
                            new DocumentFormat.OpenXml.Drawing.Offset { X = 0L, Y = 0L },
                            new DocumentFormat.OpenXml.Drawing.Extents { Cx = 0L, Cy = 0L },
                            new DocumentFormat.OpenXml.Drawing.ChildOffset { X = 0L, Y = 0L },
                            new DocumentFormat.OpenXml.Drawing.ChildExtents { Cx = 0L, Cy = 0L })))),
            new DocumentFormat.OpenXml.Presentation.ColorMap()
            {
                Background1 = DocumentFormat.OpenXml.Drawing.ColorSchemeIndexValues.Light1,
                Text1 = DocumentFormat.OpenXml.Drawing.ColorSchemeIndexValues.Dark1,
                Background2 = DocumentFormat.OpenXml.Drawing.ColorSchemeIndexValues.Light2,
                Text2 = DocumentFormat.OpenXml.Drawing.ColorSchemeIndexValues.Dark2,
                Accent1 = DocumentFormat.OpenXml.Drawing.ColorSchemeIndexValues.Accent1,
                Accent2 = DocumentFormat.OpenXml.Drawing.ColorSchemeIndexValues.Accent2,
                Accent3 = DocumentFormat.OpenXml.Drawing.ColorSchemeIndexValues.Accent3,
                Accent4 = DocumentFormat.OpenXml.Drawing.ColorSchemeIndexValues.Accent4,
                Accent5 = DocumentFormat.OpenXml.Drawing.ColorSchemeIndexValues.Accent5,
                Accent6 = DocumentFormat.OpenXml.Drawing.ColorSchemeIndexValues.Accent6,
                Hyperlink = DocumentFormat.OpenXml.Drawing.ColorSchemeIndexValues.Hyperlink,
                FollowedHyperlink = DocumentFormat.OpenXml.Drawing.ColorSchemeIndexValues.FollowedHyperlink
            },
            new DocumentFormat.OpenXml.Presentation.SlideLayoutIdList(
                new DocumentFormat.OpenXml.Presentation.SlideLayoutId() { Id = 2147483649U, RelationshipId = "rId1" }),
            new DocumentFormat.OpenXml.Presentation.TextStyles(
                new DocumentFormat.OpenXml.Presentation.TitleStyle(),
                new DocumentFormat.OpenXml.Presentation.BodyStyle(),
                new DocumentFormat.OpenXml.Presentation.OtherStyle()));

        // 创建ThemePart
        var themePart = slideMasterPart.AddNewPart<DocumentFormat.OpenXml.Packaging.ThemePart>("rId5");
        themePart.Theme = CreatePptTheme(titleColorHex);

        // 关联Theme到Presentation
        presentationPart.AddPart(themePart, "rId5");
    }

    /// <summary>
    /// 创建PPT主题
    /// </summary>
    private DocumentFormat.OpenXml.Drawing.Theme CreatePptTheme(string accentColor)
    {
        var theme = new DocumentFormat.OpenXml.Drawing.Theme() { Name = "Office Theme" };
        var themeElements = new DocumentFormat.OpenXml.Drawing.ThemeElements(
            new DocumentFormat.OpenXml.Drawing.ColorScheme(
                new DocumentFormat.OpenXml.Drawing.Dark1Color(new DocumentFormat.OpenXml.Drawing.SystemColor() { Val = DocumentFormat.OpenXml.Drawing.SystemColorValues.WindowText, LastColor = "000000" }),
                new DocumentFormat.OpenXml.Drawing.Light1Color(new DocumentFormat.OpenXml.Drawing.SystemColor() { Val = DocumentFormat.OpenXml.Drawing.SystemColorValues.Window, LastColor = "FFFFFF" }),
                new DocumentFormat.OpenXml.Drawing.Dark2Color(new DocumentFormat.OpenXml.Drawing.RgbColorModelHex() { Val = "1F497D" }),
                new DocumentFormat.OpenXml.Drawing.Light2Color(new DocumentFormat.OpenXml.Drawing.RgbColorModelHex() { Val = "EEECE1" }),
                new DocumentFormat.OpenXml.Drawing.Accent1Color(new DocumentFormat.OpenXml.Drawing.RgbColorModelHex() { Val = accentColor }),
                new DocumentFormat.OpenXml.Drawing.Accent2Color(new DocumentFormat.OpenXml.Drawing.RgbColorModelHex() { Val = "C0504D" }),
                new DocumentFormat.OpenXml.Drawing.Accent3Color(new DocumentFormat.OpenXml.Drawing.RgbColorModelHex() { Val = "9BBB59" }),
                new DocumentFormat.OpenXml.Drawing.Accent4Color(new DocumentFormat.OpenXml.Drawing.RgbColorModelHex() { Val = "8064A2" }),
                new DocumentFormat.OpenXml.Drawing.Accent5Color(new DocumentFormat.OpenXml.Drawing.RgbColorModelHex() { Val = "4BACC6" }),
                new DocumentFormat.OpenXml.Drawing.Accent6Color(new DocumentFormat.OpenXml.Drawing.RgbColorModelHex() { Val = "F79646" }),
                new DocumentFormat.OpenXml.Drawing.Hyperlink(new DocumentFormat.OpenXml.Drawing.RgbColorModelHex() { Val = "0000FF" }),
                new DocumentFormat.OpenXml.Drawing.FollowedHyperlinkColor(new DocumentFormat.OpenXml.Drawing.RgbColorModelHex() { Val = "800080" }))
            { Name = "Office" },
            new DocumentFormat.OpenXml.Drawing.FontScheme(
                new DocumentFormat.OpenXml.Drawing.MajorFont(
                    new DocumentFormat.OpenXml.Drawing.LatinFont() { Typeface = "微软雅黑" },
                    new DocumentFormat.OpenXml.Drawing.EastAsianFont() { Typeface = "微软雅黑" },
                    new DocumentFormat.OpenXml.Drawing.ComplexScriptFont() { Typeface = "" }),
                new DocumentFormat.OpenXml.Drawing.MinorFont(
                    new DocumentFormat.OpenXml.Drawing.LatinFont() { Typeface = "微软雅黑" },
                    new DocumentFormat.OpenXml.Drawing.EastAsianFont() { Typeface = "微软雅黑" },
                    new DocumentFormat.OpenXml.Drawing.ComplexScriptFont() { Typeface = "" }))
            { Name = "Office" },
            new DocumentFormat.OpenXml.Drawing.FormatScheme(
                new DocumentFormat.OpenXml.Drawing.FillStyleList(
                    new DocumentFormat.OpenXml.Drawing.SolidFill(new DocumentFormat.OpenXml.Drawing.SchemeColor() { Val = DocumentFormat.OpenXml.Drawing.SchemeColorValues.PhColor }),
                    new DocumentFormat.OpenXml.Drawing.SolidFill(new DocumentFormat.OpenXml.Drawing.SchemeColor() { Val = DocumentFormat.OpenXml.Drawing.SchemeColorValues.PhColor }),
                    new DocumentFormat.OpenXml.Drawing.SolidFill(new DocumentFormat.OpenXml.Drawing.SchemeColor() { Val = DocumentFormat.OpenXml.Drawing.SchemeColorValues.PhColor })),
                new DocumentFormat.OpenXml.Drawing.LineStyleList(
                    new DocumentFormat.OpenXml.Drawing.Outline(new DocumentFormat.OpenXml.Drawing.SolidFill(new DocumentFormat.OpenXml.Drawing.SchemeColor() { Val = DocumentFormat.OpenXml.Drawing.SchemeColorValues.PhColor })) { Width = 9525 },
                    new DocumentFormat.OpenXml.Drawing.Outline(new DocumentFormat.OpenXml.Drawing.SolidFill(new DocumentFormat.OpenXml.Drawing.SchemeColor() { Val = DocumentFormat.OpenXml.Drawing.SchemeColorValues.PhColor })) { Width = 9525 },
                    new DocumentFormat.OpenXml.Drawing.Outline(new DocumentFormat.OpenXml.Drawing.SolidFill(new DocumentFormat.OpenXml.Drawing.SchemeColor() { Val = DocumentFormat.OpenXml.Drawing.SchemeColorValues.PhColor })) { Width = 9525 }),
                new DocumentFormat.OpenXml.Drawing.EffectStyleList(
                    new DocumentFormat.OpenXml.Drawing.EffectStyle(new DocumentFormat.OpenXml.Drawing.EffectList()),
                    new DocumentFormat.OpenXml.Drawing.EffectStyle(new DocumentFormat.OpenXml.Drawing.EffectList()),
                    new DocumentFormat.OpenXml.Drawing.EffectStyle(new DocumentFormat.OpenXml.Drawing.EffectList())),
                new DocumentFormat.OpenXml.Drawing.BackgroundFillStyleList(
                    new DocumentFormat.OpenXml.Drawing.SolidFill(new DocumentFormat.OpenXml.Drawing.SchemeColor() { Val = DocumentFormat.OpenXml.Drawing.SchemeColorValues.PhColor }),
                    new DocumentFormat.OpenXml.Drawing.SolidFill(new DocumentFormat.OpenXml.Drawing.SchemeColor() { Val = DocumentFormat.OpenXml.Drawing.SchemeColorValues.PhColor }),
                    new DocumentFormat.OpenXml.Drawing.SolidFill(new DocumentFormat.OpenXml.Drawing.SchemeColor() { Val = DocumentFormat.OpenXml.Drawing.SchemeColorValues.PhColor })))
            { Name = "Office" });

        theme.Append(themeElements);
        theme.Append(new DocumentFormat.OpenXml.Drawing.ObjectDefaults());
        theme.Append(new DocumentFormat.OpenXml.Drawing.ExtraColorSchemeList());

        return theme;
    }

    /// <summary>
    /// 创建封面页 (OpenXML)
    /// </summary>
    private void CreateTitleSlideOpenXml(DocumentFormat.OpenXml.Packaging.SlidePart slidePart, PptSlide data, string template)
    {
        var (titleColor, textColor) = GetTemplateColorsHex(template);

        var slide = new DocumentFormat.OpenXml.Presentation.Slide(
            new DocumentFormat.OpenXml.Presentation.CommonSlideData(
                new DocumentFormat.OpenXml.Presentation.ShapeTree(
                    new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeProperties(
                        new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties { Id = 1U, Name = "" },
                        new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeDrawingProperties(),
                        new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties()),
                    CreateGroupShapeProperties(),
                    // 标题文本框
                    CreateTextShape(2, "标题", data.Title, 500000, 2500000, 11000000, 1200000, 4400, true, titleColor, DocumentFormat.OpenXml.Drawing.TextAnchoringTypeValues.Center),
                    // 副标题
                    CreateTextShape(3, "副标题", data.Points.Count > 0 ? data.Points[0] : "", 500000, 4000000, 11000000, 600000, 2400, false, textColor, DocumentFormat.OpenXml.Drawing.TextAnchoringTypeValues.Center)
                )
            ),
            new DocumentFormat.OpenXml.Presentation.ColorMapOverride(new DocumentFormat.OpenXml.Drawing.MasterColorMapping())
        );

        slidePart.Slide = slide;
    }

    /// <summary>
    /// 创建内容页 (OpenXML)
    /// </summary>
    private void CreateContentSlideOpenXml(DocumentFormat.OpenXml.Packaging.SlidePart slidePart, PptSlide data, string template)
    {
        var (titleColor, textColor) = GetTemplateColorsHex(template);

        // 构建要点文本
        var pointsText = string.Join("\n• ", data.Points);
        if (data.Points.Count > 0) pointsText = "• " + pointsText;

        var slide = new DocumentFormat.OpenXml.Presentation.Slide(
            new DocumentFormat.OpenXml.Presentation.CommonSlideData(
                new DocumentFormat.OpenXml.Presentation.ShapeTree(
                    new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeProperties(
                        new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties { Id = 1U, Name = "" },
                        new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeDrawingProperties(),
                        new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties()),
                    CreateGroupShapeProperties(),
                    // 标题
                    CreateTextShape(2, "标题", data.Title, 500000, 300000, 11000000, 800000, 3200, true, titleColor, DocumentFormat.OpenXml.Drawing.TextAnchoringTypeValues.Top),
                    // 内容
                    CreateTextShape(3, "内容", pointsText, 500000, 1300000, 11000000, 5000000, 2000, false, textColor, DocumentFormat.OpenXml.Drawing.TextAnchoringTypeValues.Top)
                )
            ),
            new DocumentFormat.OpenXml.Presentation.ColorMapOverride(new DocumentFormat.OpenXml.Drawing.MasterColorMapping())
        );

        slidePart.Slide = slide;
    }

    /// <summary>
    /// 创建图表页 (OpenXML) - 支持图片嵌入
    /// </summary>
    private void CreateChartSlideOpenXml(DocumentFormat.OpenXml.Packaging.SlidePart slidePart, PptSlide data, string template)
    {
        var (titleColor, textColor) = GetTemplateColorsHex(template);

        // 创建ShapeTree
        var shapeTree = new DocumentFormat.OpenXml.Presentation.ShapeTree(
            new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeProperties(
                new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties { Id = 1U, Name = "" },
                new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeDrawingProperties(),
                new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties()),
            CreateGroupShapeProperties(),
            // 标题
            CreateTextShape(2, "标题", data.Title, 500000, 300000, 11000000, 800000, 3200, true, titleColor, DocumentFormat.OpenXml.Drawing.TextAnchoringTypeValues.Top)
        );

        // 如果有图表图片，嵌入图片；否则显示数据摘要文本
        if (!string.IsNullOrEmpty(data.ChartImageBase64))
        {
            try
            {
                // 解码Base64图片
                var imageBytes = Convert.FromBase64String(data.ChartImageBase64);

                // 添加ImagePart（使用AddNewPart方法）
                var imagePart = slidePart.AddNewPart<DocumentFormat.OpenXml.Packaging.ImagePart>("image/png", "rIdImg1");
                using (var stream = new MemoryStream(imageBytes))
                {
                    imagePart.FeedData(stream);
                }

                // 获取relationship ID
                string relationshipId = slidePart.GetIdOfPart(imagePart);

                // 创建图片元素 (位置: 左边距500000, 上边距1200000, 宽度10000000, 高度4200000)
                var picture = CreatePicture(3, "图表图片", relationshipId, 500000, 1200000, 10000000, 4200000);
                shapeTree.Append(picture);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "解码图表图片失败，使用文本摘要");
                var chartDataText = BuildChartDataSummary(data);
                shapeTree.Append(CreateTextShape(3, "图表数据", chartDataText, 500000, 1300000, 11000000, 4000000, 1600, false, textColor, DocumentFormat.OpenXml.Drawing.TextAnchoringTypeValues.Top));
            }
        }
        else
        {
            // 没有图片时显示数据摘要
            var chartDataText = BuildChartDataSummary(data);
            shapeTree.Append(CreateTextShape(3, "图表数据", chartDataText, 500000, 1300000, 11000000, 4000000, 1600, false, textColor, DocumentFormat.OpenXml.Drawing.TextAnchoringTypeValues.Top));
        }

        // 添加说明
        shapeTree.Append(CreateTextShape(4, "说明", string.Join(" | ", data.Points), 500000, 5600000, 11000000, 600000, 1400, false, "666666", DocumentFormat.OpenXml.Drawing.TextAnchoringTypeValues.Top));

        var slide = new DocumentFormat.OpenXml.Presentation.Slide(
            new DocumentFormat.OpenXml.Presentation.CommonSlideData(shapeTree),
            new DocumentFormat.OpenXml.Presentation.ColorMapOverride(new DocumentFormat.OpenXml.Drawing.MasterColorMapping())
        );

        slidePart.Slide = slide;
    }

    /// <summary>
    /// 创建图片元素 (OpenXML)
    /// </summary>
    private DocumentFormat.OpenXml.Presentation.Picture CreatePicture(
        uint id, string name, string relationshipId,
        long x, long y, long cx, long cy)
    {
        return new DocumentFormat.OpenXml.Presentation.Picture(
            new DocumentFormat.OpenXml.Presentation.NonVisualPictureProperties(
                new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties { Id = id, Name = name },
                new DocumentFormat.OpenXml.Presentation.NonVisualPictureDrawingProperties(
                    new DocumentFormat.OpenXml.Drawing.PictureLocks { NoChangeAspect = true }),
                new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties()),
            new DocumentFormat.OpenXml.Presentation.BlipFill(
                new DocumentFormat.OpenXml.Drawing.Blip { Embed = relationshipId },
                new DocumentFormat.OpenXml.Drawing.Stretch(new DocumentFormat.OpenXml.Drawing.FillRectangle())),
            new DocumentFormat.OpenXml.Presentation.ShapeProperties(
                new DocumentFormat.OpenXml.Drawing.Transform2D(
                    new DocumentFormat.OpenXml.Drawing.Offset { X = x, Y = y },
                    new DocumentFormat.OpenXml.Drawing.Extents { Cx = cx, Cy = cy }),
                new DocumentFormat.OpenXml.Drawing.PresetGeometry(
                    new DocumentFormat.OpenXml.Drawing.AdjustValueList()) { Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle })
        );
    }

    /// <summary>
    /// 构建图表数据摘要文本
    /// </summary>
    private string BuildChartDataSummary(PptSlide data)
    {
        // 如果有图表数据，生成数据摘要
        if (data.ChartData != null && data.ChartData.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("📊 图表数据摘要：");
            sb.AppendLine();

            // 获取列名
            var columns = data.ChartData[0].Keys.ToList();

            // 限制显示前10行数据
            var displayRows = data.ChartData.Take(10).ToList();

            // 构建简单的表格显示
            foreach (var row in displayRows)
            {
                var values = columns.Select(col =>
                {
                    var val = row.ContainsKey(col) ? row[col]?.ToString() ?? "-" : "-";
                    return $"{col}: {val}";
                });
                sb.AppendLine("• " + string.Join("  |  ", values));
            }

            if (data.ChartData.Count > 10)
            {
                sb.AppendLine($"... 共 {data.ChartData.Count} 条数据");
            }

            sb.AppendLine();
            sb.AppendLine("💡 提示：请根据以上数据在PowerPoint中插入对应的图表");

            return sb.ToString();
        }

        // 没有数据时显示占位符
        return "[图表区域]\n\n请根据数据在PowerPoint中插入图表\n\n提示：可以使用\"插入-图表\"功能创建柱状图、折线图等";
    }

    /// <summary>
    /// 创建总结页 (OpenXML)
    /// </summary>
    private void CreateSummarySlideOpenXml(DocumentFormat.OpenXml.Packaging.SlidePart slidePart, PptSlide data, string template)
    {
        var (titleColor, textColor) = GetTemplateColorsHex(template);

        // 构建要点文本
        var pointsText = string.Join("\n• ", data.Points);
        if (data.Points.Count > 0) pointsText = "• " + pointsText;

        var slide = new DocumentFormat.OpenXml.Presentation.Slide(
            new DocumentFormat.OpenXml.Presentation.CommonSlideData(
                new DocumentFormat.OpenXml.Presentation.ShapeTree(
                    new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeProperties(
                        new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties { Id = 1U, Name = "" },
                        new DocumentFormat.OpenXml.Presentation.NonVisualGroupShapeDrawingProperties(),
                        new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties()),
                    CreateGroupShapeProperties(),
                    // 标题
                    CreateTextShape(2, "总结", data.Title, 500000, 300000, 11000000, 800000, 3600, true, titleColor, DocumentFormat.OpenXml.Drawing.TextAnchoringTypeValues.Center),
                    // 内容
                    CreateTextShape(3, "内容", pointsText, 1000000, 1500000, 10000000, 4500000, 2200, false, textColor, DocumentFormat.OpenXml.Drawing.TextAnchoringTypeValues.Center)
                )
            ),
            new DocumentFormat.OpenXml.Presentation.ColorMapOverride(new DocumentFormat.OpenXml.Drawing.MasterColorMapping())
        );

        slidePart.Slide = slide;
    }

    /// <summary>
    /// 创建幻灯片组形状属性（包含完整的TransformGroup）
    /// </summary>
    private DocumentFormat.OpenXml.Presentation.GroupShapeProperties CreateGroupShapeProperties()
    {
        return new DocumentFormat.OpenXml.Presentation.GroupShapeProperties(
            new DocumentFormat.OpenXml.Drawing.TransformGroup(
                new DocumentFormat.OpenXml.Drawing.Offset { X = 0L, Y = 0L },
                new DocumentFormat.OpenXml.Drawing.Extents { Cx = 0L, Cy = 0L },
                new DocumentFormat.OpenXml.Drawing.ChildOffset { X = 0L, Y = 0L },
                new DocumentFormat.OpenXml.Drawing.ChildExtents { Cx = 0L, Cy = 0L }
            )
        );
    }

    /// <summary>
    /// 创建文本形状（无填充背景）
    /// </summary>
    private DocumentFormat.OpenXml.Presentation.Shape CreateTextShape(
        uint id, string name, string text,
        long x, long y, long cx, long cy,
        int fontSize, bool bold, string colorHex,
        DocumentFormat.OpenXml.Drawing.TextAnchoringTypeValues anchor)
    {
        // 创建ShapeProperties，显式设置NoFill避免黑色填充
        var shapeProperties = new DocumentFormat.OpenXml.Presentation.ShapeProperties();
        shapeProperties.Append(new DocumentFormat.OpenXml.Drawing.Transform2D(
            new DocumentFormat.OpenXml.Drawing.Offset { X = x, Y = y },
            new DocumentFormat.OpenXml.Drawing.Extents { Cx = cx, Cy = cy }));
        shapeProperties.Append(new DocumentFormat.OpenXml.Drawing.PresetGeometry(
            new DocumentFormat.OpenXml.Drawing.AdjustValueList()) { Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle });
        shapeProperties.Append(new DocumentFormat.OpenXml.Drawing.NoFill());  // 关键：设置无填充

        return new DocumentFormat.OpenXml.Presentation.Shape(
            new DocumentFormat.OpenXml.Presentation.NonVisualShapeProperties(
                new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties { Id = id, Name = name },
                new DocumentFormat.OpenXml.Presentation.NonVisualShapeDrawingProperties(),
                new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties()),
            shapeProperties,
            new DocumentFormat.OpenXml.Presentation.TextBody(
                new DocumentFormat.OpenXml.Drawing.BodyProperties { Anchor = anchor },
                new DocumentFormat.OpenXml.Drawing.ListStyle(),
                new DocumentFormat.OpenXml.Drawing.Paragraph(
                    new DocumentFormat.OpenXml.Drawing.ParagraphProperties { Alignment = DocumentFormat.OpenXml.Drawing.TextAlignmentTypeValues.Center },
                    new DocumentFormat.OpenXml.Drawing.Run(
                        new DocumentFormat.OpenXml.Drawing.RunProperties(
                            new DocumentFormat.OpenXml.Drawing.SolidFill(
                                new DocumentFormat.OpenXml.Drawing.RgbColorModelHex { Val = colorHex }))
                        {
                            Language = "zh-CN",
                            FontSize = fontSize * 100,
                            Bold = bold
                        },
                        new DocumentFormat.OpenXml.Drawing.Text(text)),
                    new DocumentFormat.OpenXml.Drawing.EndParagraphRunProperties { Language = "zh-CN" }))
        );
    }

    /// <summary>
    /// 获取模板颜色配置（十六进制）
    /// </summary>
    private (string title, string text) GetTemplateColorsHex(string template)
    {
        return template switch
        {
            "business" => ("003366", "333333"),  // 商务蓝 - 深蓝色标题，深灰色文字
            "medical" => ("006633", "333333"),   // 医疗绿 - 深绿色标题，深灰色文字
            "simple" => ("333333", "444444"),    // 简约白 - 深灰色标题和文字
            _ => ("333333", "444444")            // 默认深灰色
        };
    }

    #endregion

    #region Word报告生成

    /// <summary>
    /// 生成Word报告大纲
    /// 根据选中的会话内容，使用大模型生成Word报告的章节结构
    /// </summary>
    [HttpPost("word/outline")]
    public async Task<IActionResult> GenerateWordOutline([FromBody] WordOutlineRequest request)
    {
        try
        {
            if (request.SessionIds == null || request.SessionIds.Count == 0)
            {
                return Ok(ApiResponse<WordOutlineResponse>.Fail("请选择至少一个会话"));
            }

            // 获取选中会话的消息
            var sessions = await _db.AiSessions
                .Where(s => request.SessionIds.Contains(s.Id))
                .Include(s => s.Messages)
                .ToListAsync();

            if (!sessions.Any())
            {
                return Ok(ApiResponse<WordOutlineResponse>.Fail("未找到选中的会话"));
            }

            // 构建对话内容摘要
            var contentBuilder = new StringBuilder();
            // 扩展元组，添加ChartImages字段用于收集已保存的图片
            var chartMessages = new List<(long MessageId, string Title, string ChartConfig, string? ChartImages)>();

            foreach (var session in sessions)
            {
                contentBuilder.AppendLine($"### 会话：{session.Title}");
                foreach (var msg in session.Messages.OrderBy(m => m.CreatedAt))
                {
                    if (msg.Role == "user")
                    {
                        contentBuilder.AppendLine($"问题：{msg.Content}");
                    }
                    else if (msg.Role == "assistant")
                    {
                        contentBuilder.AppendLine($"回答：{msg.Content}");
                        // 收集有图表配置或图表截图的消息（支持新旧两种模式）
                        // 新模式：DefaultChartsConfig + ChartImages
                        // 旧模式：ChartConfig
                        if (!string.IsNullOrEmpty(msg.ChartConfig) ||
                            !string.IsNullOrEmpty(msg.DefaultChartsConfig) ||
                            !string.IsNullOrEmpty(msg.ChartImages))
                        {
                            chartMessages.Add((
                                msg.Id,
                                msg.Content?.Split('\n').FirstOrDefault() ?? "图表",
                                msg.ChartConfig ?? msg.DefaultChartsConfig ?? "",
                                msg.ChartImages  // 添加已保存的图片路径
                            ));
                        }
                    }
                }
                contentBuilder.AppendLine();
            }

            // 构建Prompt让大模型生成Word报告大纲
            var systemPrompt = @"你是一个专业的数据分析报告撰写助手。根据用户提供的对话历史和主题，生成一个结构清晰、内容详实的Word报告大纲。

要求：
1. 报告应包含摘要部分，概述整体分析结论
2. 合理划分章节，每个章节要有详细的内容描述
3. 如果有数据表格，在合适的位置插入表格章节（type: table）或图表章节（type: chart）
4. 最后一章是总结与建议（type: conclusion）
5. 内容要比PPT更详细，适合阅读
6. [重要] 已保存的图表截图中每张图片对应一个独立的分析维度，必须为每张图片创建单独的chart或table章节，标题使用图片标题
7. [重要] 包含图片的章节的title必须与已保存的图表截图中列出的图片标题完全一致，这样才能自动匹配嵌入对应图片
8. [重要] 每张图片只用于一个章节，不要多章节共用同一张图片
9. 返回JSON格式，不要有其他内容

JSON格式示例：
{
  ""title"": ""报告标题"",
  ""subtitle"": ""副标题（可选）"",
  ""abstract"": ""报告摘要，简述主要发现和结论"",
  ""chapters"": [
    {
      ""order"": 1,
      ""title"": ""背景与概述"",
      ""type"": ""text"",
      ""content"": ""详细的章节正文内容...""
    },
    {
      ""order"": 2,
      ""title"": ""【使用可用图表列表中的图片标题】"",
      ""type"": ""chart"",
      ""content"": ""图表分析说明..."",
      ""messageId"": 123
    },
    {
      ""order"": 3,
      ""title"": ""【使用可用图表列表中的图片标题】"",
      ""type"": ""table"",
      ""content"": ""表格说明文字..."",
      ""messageId"": 123
    },
    {
      ""order"": 4,
      ""title"": ""总结与建议"",
      ""type"": ""conclusion"",
      ""content"": ""总结性内容...""
    }
  ]
}";

            // 收集有已保存图片的消息（用于告知AI有哪些图片可用）
            var wordMessagesWithImages = new List<(long MessageId, string Title, List<string> ImagePaths)>();
            foreach (var cm in chartMessages.Where(c => !string.IsNullOrEmpty(c.ChartImages)))
            {
                try
                {
                    var imagePaths = JsonSerializer.Deserialize<List<string>>(cm.ChartImages!);
                    if (imagePaths != null && imagePaths.Count > 0)
                    {
                        wordMessagesWithImages.Add((cm.MessageId, cm.Title, imagePaths));
                    }
                }
                catch { /* 忽略解析错误 */ }
            }

            // 构建已保存图片的描述（提取图片标题，与PPT一致）
            var wordSavedImagesInfo = new StringBuilder();
            if (wordMessagesWithImages.Count > 0)
            {
                wordSavedImagesInfo.AppendLine("已保存的图表截图（这些图片将自动嵌入到对应的图表章节中）：");
                foreach (var mi in wordMessagesWithImages)
                {
                    wordSavedImagesInfo.AppendLine($"- MessageId: {mi.MessageId}, 标题: {mi.Title}, 图片数量: {mi.ImagePaths.Count}张");
                    foreach (var path in mi.ImagePaths)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(path);
                        var parts = fileName.Split('_');
                        var chartTitle = parts.Length >= 2 ? string.Join("", parts.Skip(1).Take(parts.Length - 3)) : fileName;
                        wordSavedImagesInfo.AppendLine($"  - 图片标题: {chartTitle} (文件名: {fileName})");
                    }
                }
            }
            else
            {
                wordSavedImagesInfo.AppendLine("已保存的图表截图：无（用户尚未保存任何图表截图）");
            }

            var userPrompt = $@"请根据以下对话历史，生成主题为""{request.Title}""的Word报告大纲。

{(string.IsNullOrEmpty(request.Idea) ? "" : $"用户要求：{request.Idea}\n\n")}对话历史：
{contentBuilder}

可用的图表/表格数据（可在大纲中引用messageId）：
{string.Join("\n", chartMessages.Select(c => $"- MessageId: {c.MessageId}, 标题: {c.Title}"))}

{wordSavedImagesInfo}

请生成Word报告大纲（JSON格式）：";

            var messages = new List<LlmMessage>
            {
                LlmMessage.System(systemPrompt),
                LlmMessage.User(userPrompt)
            };

            var llmResponse = await _llmService.ChatAsync(messages, new LlmOptions
            {
                Temperature = 0.7,
                MaxTokens = 8192,
                BusinessType = AiBusinessType.DocGen  // Word生成使用文档生成配置
            });

            if (!llmResponse.Success)
            {
                return Ok(ApiResponse<WordOutlineResponse>.Fail($"大模型调用失败: {llmResponse.Error}"));
            }

            // 解析JSON响应
            var content = llmResponse.Content;
            if (content.Contains("```"))
            {
                content = Regex.Replace(content, @"```json\s*", "");
                content = Regex.Replace(content, @"```\s*", "");
            }
            content = content.Trim();

            var outline = JsonSerializer.Deserialize<WordOutlineResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (outline == null)
            {
                return Ok(ApiResponse<WordOutlineResponse>.Fail("大纲解析失败"));
            }

            // 补充表格/图表数据和图片URL（智能匹配）
            var wordUsedImageIndices = new HashSet<string>();
            foreach (var chapter in outline.Chapters.Where(c => (c.Type == "table" || c.Type == "chart") && c.MessageId.HasValue))
            {
                try
                {
                    var msg = await _db.AiMessages.FindAsync(chapter.MessageId!.Value);
                    if (msg != null)
                    {
                        if (!string.IsNullOrEmpty(msg.ChartImages))
                        {
                            try
                            {
                                var chartImages = JsonSerializer.Deserialize<List<string>>(msg.ChartImages);
                                if (chartImages != null && chartImages.Count > 0)
                                {
                                    var matchedImage = MatchBestImage(chapter.Title, chartImages, wordUsedImageIndices, _logger);
                                    chapter.ChartImageUrls = matchedImage != null ? new List<string> { matchedImage } : null;
                                    _logger.LogInformation("Word大纲匹配图片: Type={Type}, Title={ChapterTitle}, Matched={Matched}",
                                        chapter.Type, chapter.Title, matchedImage ?? "无");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "解析ChartImages失败: MessageId={MessageId}", chapter.MessageId);
                            }
                        }

                        if (!string.IsNullOrEmpty(msg.Sql))
                        {
                            var datasource = await _db.Datasources.FindAsync(request.DatasourceId);
                            if (datasource != null)
                            {
                                var data = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, msg.Sql);
                                if (data.Count > 0)
                                {
                                    chapter.TableData = data;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "获取数据失败: MessageId={MessageId}", chapter.MessageId);
                }
            }

            outline.SystemPrompt = systemPrompt;
            outline.UserPrompt = userPrompt;

            return Ok(ApiResponse<WordOutlineResponse>.Success(outline));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成Word报告大纲失败");
            return Ok(ApiResponse<WordOutlineResponse>.Fail($"生成失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 生成Word文件
    /// 根据大纲生成DOCX文件并返回base64
    /// </summary>
    [HttpPost("word/generate")]
    public async Task<IActionResult> GenerateWordFile([FromBody] WordGenerateRequest request)
    {
        try
        {
            if (request.Outline?.Chapters == null || request.Outline.Chapters.Count == 0)
            {
                return Ok(ApiResponse<string>.Fail("Word报告大纲不能为空"));
            }

            // 获取数据源连接（用于执行SQL获取表格数据）
            var datasource = request.DatasourceId > 0
                ? await _db.Datasources.FindAsync(request.DatasourceId)
                : null;

            // 为表格/图表章节获取数据和图片
            foreach (var chapter in request.Outline.Chapters.Where(c =>
                (c.Type == "table" || c.Type == "chart") && c.MessageId.HasValue))
            {
                try
                {
                    var message = await _db.AiMessages.FindAsync(chapter.MessageId!.Value);
                    if (message != null)
                    {
                        // 优先使用前端传来的已匹配图片URL（来自大纲阶段的智能匹配）
                        if (chapter.ChartImageUrls != null && chapter.ChartImageUrls.Count > 0)
                        {
                            var imagePath = chapter.ChartImageUrls[0];
                            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                            if (System.IO.File.Exists(fullPath))
                            {
                                var imageBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
                                chapter.ChartImageBase64 = Convert.ToBase64String(imageBytes);
                                _logger.LogInformation("使用前端匹配的图表截图: Type={Type}, MessageId={MessageId}, Path={Path}", chapter.Type, chapter.MessageId, imagePath);
                            }
                        }
                        // 如果前端没传匹配图片，回退到数据库中的图片
                        else if ((chapter.Type == "chart" || chapter.Type == "table") && !string.IsNullOrEmpty(message.ChartImages))
                        {
                            var chartImages = JsonSerializer.Deserialize<List<string>>(message.ChartImages);
                            if (chartImages != null && chartImages.Count > 0)
                            {
                                var matchedPath = MatchBestImage(chapter.Title, chartImages, new HashSet<string>(), _logger);
                                var imagePath = matchedPath ?? chartImages[0];
                                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                                if (System.IO.File.Exists(fullPath))
                                {
                                    var imageBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
                                    chapter.ChartImageBase64 = Convert.ToBase64String(imageBytes);
                                    chapter.ChartImageUrls = new List<string> { imagePath };
                                    _logger.LogInformation("使用后端匹配的图表截图: Type={Type}, MessageId={MessageId}, Path={Path}", chapter.Type, chapter.MessageId, imagePath);
                                }
                            }
                        }

                        if (chapter.TableData == null && !string.IsNullOrEmpty(message.Sql) && datasource != null)
                        {
                            var data = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, message.Sql);
                            if (data.Count > 0)
                            {
                                chapter.TableData = data;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "获取数据失败: MessageId={MessageId}", chapter.MessageId);
                }
            }

            // 使用OpenXML生成DOCX
            var docxBytes = await GenerateDocxAsync(request.Outline, request.Template);

            // 返回base64编码的文件内容
            var base64 = Convert.ToBase64String(docxBytes);
            return Ok(ApiResponse<string>.Success(base64, "Word报告生成成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成Word文件失败");
            return Ok(ApiResponse<string>.Fail($"生成失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 使用OpenXML SDK生成DOCX文件
    /// </summary>
    private async Task<byte[]> GenerateDocxAsync(WordOutlineResponse outline, string template)
    {
        await Task.CompletedTask;

        using var ms = new MemoryStream();

        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            // 创建主文档部分
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = new DocumentFormat.OpenXml.Wordprocessing.Body();

            // 获取模板样式
            var (titleColor, headingColor, textColor) = GetWordTemplateColors(template);

            // 添加报告标题
            AddWordTitle(body, outline.Title, titleColor);

            // 添加副标题（如果有）
            if (!string.IsNullOrEmpty(outline.Subtitle))
            {
                AddWordSubtitle(body, outline.Subtitle, headingColor);
            }

            // 添加日期
            AddWordParagraph(body, $"生成日期：{DateTime.Now:yyyy年MM月dd日}", textColor, 12, false, true);
            AddWordEmptyLine(body);

            // 添加摘要（如果有）
            if (!string.IsNullOrEmpty(outline.Abstract))
            {
                AddWordHeading(body, "摘 要", 1, headingColor);
                AddWordParagraph(body, outline.Abstract, textColor, 12, false, false);
                AddWordEmptyLine(body);
            }

            // 添加章节
            foreach (var chapter in outline.Chapters.OrderBy(c => c.Order))
            {
                // 章节标题
                AddWordHeading(body, $"{chapter.Order}. {chapter.Title}", 1, headingColor);

                // 章节内容
                if (!string.IsNullOrEmpty(chapter.Content))
                {
                    // 按段落分割
                    var paragraphs = chapter.Content.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var para in paragraphs)
                    {
                        AddWordParagraph(body, para.Trim(), textColor, 12, false, false);
                    }
                }

                // 如果是图表或表格类型且有图片，嵌入图片
                if ((chapter.Type == "chart" || chapter.Type == "table") && !string.IsNullOrEmpty(chapter.ChartImageBase64))
                {
                    try
                    {
                        AddWordImage(mainPart, body, chapter.ChartImageBase64);
                        _logger.LogInformation("Word嵌入图表图片成功: Type={Type}, Title={Title}", chapter.Type, chapter.Title);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "嵌入图表图片失败");
                    }
                }

                // 如果是表格类型，还需要显示表格数据
                if ((chapter.Type == "table" || chapter.Type == "chart") && chapter.TableData != null && chapter.TableData.Count > 0)
                {
                    if (chapter.Type == "chart" && string.IsNullOrEmpty(chapter.ChartImageBase64))
                    {
                        // 图表类型没有图片时，提示数据表
                        AddWordParagraph(body, "【数据图表】（原图表数据如下表所示）", textColor, 10, true, true);
                    }
                    else if (chapter.Type == "table")
                    {
                        // 表格类型始终显示表格
                        AddWordParagraph(body, "【数据明细】", textColor, 10, true, true);
                    }
                    AddWordTable(body, chapter.TableData, textColor);
                }

                AddWordEmptyLine(body);

                // 处理子章节
                if (chapter.SubChapters != null)
                {
                    foreach (var subChapter in chapter.SubChapters.OrderBy(c => c.Order))
                    {
                        AddWordHeading(body, $"{chapter.Order}.{subChapter.Order} {subChapter.Title}", 2, headingColor);
                        if (!string.IsNullOrEmpty(subChapter.Content))
                        {
                            AddWordParagraph(body, subChapter.Content, textColor, 12, false, false);
                        }
                        // 如果是图表或表格类型且有图片，嵌入图片
                        if ((subChapter.Type == "chart" || subChapter.Type == "table") && !string.IsNullOrEmpty(subChapter.ChartImageBase64))
                        {
                            try
                            {
                                AddWordImage(mainPart, body, subChapter.ChartImageBase64);
                                _logger.LogInformation("Word嵌入子章节图表图片成功: Type={Type}, Title={Title}", subChapter.Type, subChapter.Title);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "嵌入子章节图表图片失败");
                            }
                        }

                        // 如果是表格类型，还需要显示表格数据
                        if ((subChapter.Type == "table" || subChapter.Type == "chart") && subChapter.TableData != null && subChapter.TableData.Count > 0)
                        {
                            if (subChapter.Type == "chart" && string.IsNullOrEmpty(subChapter.ChartImageBase64))
                            {
                                AddWordParagraph(body, "【数据图表】（原图表数据如下表所示）", textColor, 10, true, true);
                            }
                            else if (subChapter.Type == "table")
                            {
                                AddWordParagraph(body, "【数据明细】", textColor, 10, true, true);
                            }
                            AddWordTable(body, subChapter.TableData, textColor);
                        }
                    }
                }
            }

            mainPart.Document.Append(body);
            mainPart.Document.Save();
        }

        return ms.ToArray();
    }

    /// <summary>
    /// 获取Word模板颜色配置
    /// </summary>
    private (string title, string heading, string text) GetWordTemplateColors(string template)
    {
        return template switch
        {
            "formal" => ("003366", "003366", "333333"),    // 正式报告 - 深蓝色
            "simple" => ("333333", "444444", "555555"),    // 简约版 - 灰色
            "academic" => ("000000", "000000", "333333"),  // 学术版 - 黑色
            _ => ("003366", "003366", "333333")
        };
    }

    /// <summary>
    /// 添加Word标题
    /// </summary>
    private void AddWordTitle(DocumentFormat.OpenXml.Wordprocessing.Body body, string text, string color)
    {
        var para = new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
            new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties(
                new DocumentFormat.OpenXml.Wordprocessing.Justification { Val = DocumentFormat.OpenXml.Wordprocessing.JustificationValues.Center },
                new DocumentFormat.OpenXml.Wordprocessing.SpacingBetweenLines { After = "400" }
            ),
            new DocumentFormat.OpenXml.Wordprocessing.Run(
                new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                    new DocumentFormat.OpenXml.Wordprocessing.Bold(),
                    new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = "48" },  // 24磅
                    new DocumentFormat.OpenXml.Wordprocessing.Color { Val = color }
                ),
                new DocumentFormat.OpenXml.Wordprocessing.Text(text)
            )
        );
        body.Append(para);
    }

    /// <summary>
    /// 添加Word副标题
    /// </summary>
    private void AddWordSubtitle(DocumentFormat.OpenXml.Wordprocessing.Body body, string text, string color)
    {
        var para = new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
            new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties(
                new DocumentFormat.OpenXml.Wordprocessing.Justification { Val = DocumentFormat.OpenXml.Wordprocessing.JustificationValues.Center },
                new DocumentFormat.OpenXml.Wordprocessing.SpacingBetweenLines { After = "200" }
            ),
            new DocumentFormat.OpenXml.Wordprocessing.Run(
                new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                    new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = "32" },  // 16磅
                    new DocumentFormat.OpenXml.Wordprocessing.Color { Val = color }
                ),
                new DocumentFormat.OpenXml.Wordprocessing.Text(text)
            )
        );
        body.Append(para);
    }

    /// <summary>
    /// 添加Word章节标题
    /// </summary>
    private void AddWordHeading(DocumentFormat.OpenXml.Wordprocessing.Body body, string text, int level, string color)
    {
        var fontSize = level == 1 ? "32" : "28";  // 一级标题16磅，二级标题14磅
        var para = new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
            new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties(
                new DocumentFormat.OpenXml.Wordprocessing.SpacingBetweenLines { Before = "300", After = "200" }
            ),
            new DocumentFormat.OpenXml.Wordprocessing.Run(
                new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                    new DocumentFormat.OpenXml.Wordprocessing.Bold(),
                    new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = fontSize },
                    new DocumentFormat.OpenXml.Wordprocessing.Color { Val = color }
                ),
                new DocumentFormat.OpenXml.Wordprocessing.Text(text)
            )
        );
        body.Append(para);
    }

    /// <summary>
    /// 添加Word段落
    /// </summary>
    private void AddWordParagraph(DocumentFormat.OpenXml.Wordprocessing.Body body, string text, string color, int fontSize, bool bold, bool center)
    {
        var paraProps = new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties(
            new DocumentFormat.OpenXml.Wordprocessing.SpacingBetweenLines { Line = "360", LineRule = DocumentFormat.OpenXml.Wordprocessing.LineSpacingRuleValues.Auto }
        );
        if (center)
        {
            paraProps.Append(new DocumentFormat.OpenXml.Wordprocessing.Justification { Val = DocumentFormat.OpenXml.Wordprocessing.JustificationValues.Center });
        }

        var runProps = new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
            new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = (fontSize * 2).ToString() },
            new DocumentFormat.OpenXml.Wordprocessing.Color { Val = color }
        );
        if (bold)
        {
            runProps.Append(new DocumentFormat.OpenXml.Wordprocessing.Bold());
        }

        var para = new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
            paraProps,
            new DocumentFormat.OpenXml.Wordprocessing.Run(
                runProps,
                new DocumentFormat.OpenXml.Wordprocessing.Text(text)
            )
        );
        body.Append(para);
    }

    /// <summary>
    /// 添加空行
    /// </summary>
    private void AddWordEmptyLine(DocumentFormat.OpenXml.Wordprocessing.Body body)
    {
        body.Append(new DocumentFormat.OpenXml.Wordprocessing.Paragraph());
    }

    /// <summary>
    /// 添加图片到Word文档
    /// </summary>
    private void AddWordImage(DocumentFormat.OpenXml.Packaging.MainDocumentPart mainPart, DocumentFormat.OpenXml.Wordprocessing.Body body, string base64Image)
    {
        // 解码Base64图片
        var imageBytes = Convert.FromBase64String(base64Image);

        // 添加ImagePart（使用AddNewPart方法）
        var imagePart = mainPart.AddNewPart<DocumentFormat.OpenXml.Packaging.ImagePart>("image/png", "rIdImg" + Guid.NewGuid().ToString("N").Substring(0, 8));
        using (var stream = new MemoryStream(imageBytes))
        {
            imagePart.FeedData(stream);
        }

        // 获取relationship ID
        string relationshipId = mainPart.GetIdOfPart(imagePart);

        // 图片尺寸 (单位: EMUs, 1英寸=914400 EMUs)
        // 设置为16cm x 9cm (约6.3英寸 x 3.5英寸)
        const long imageWidthEmu = 5760000L;   // 约16cm
        const long imageHeightEmu = 3240000L;  // 约9cm

        // 创建Drawing元素
        var drawing = new DocumentFormat.OpenXml.Wordprocessing.Drawing(
            new DocumentFormat.OpenXml.Drawing.Wordprocessing.Inline(
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent { Cx = imageWidthEmu, Cy = imageHeightEmu },
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties { Id = 1U, Name = "图表图片" },
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.NonVisualGraphicFrameDrawingProperties(
                    new DocumentFormat.OpenXml.Drawing.GraphicFrameLocks { NoChangeAspect = true }),
                new DocumentFormat.OpenXml.Drawing.Graphic(
                    new DocumentFormat.OpenXml.Drawing.GraphicData(
                        new DocumentFormat.OpenXml.Drawing.Pictures.Picture(
                            new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureProperties(
                                new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualDrawingProperties { Id = 0U, Name = "ChartImage.png" },
                                new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureDrawingProperties()),
                            new DocumentFormat.OpenXml.Drawing.Pictures.BlipFill(
                                new DocumentFormat.OpenXml.Drawing.Blip { Embed = relationshipId },
                                new DocumentFormat.OpenXml.Drawing.Stretch(new DocumentFormat.OpenXml.Drawing.FillRectangle())),
                            new DocumentFormat.OpenXml.Drawing.Pictures.ShapeProperties(
                                new DocumentFormat.OpenXml.Drawing.Transform2D(
                                    new DocumentFormat.OpenXml.Drawing.Offset { X = 0L, Y = 0L },
                                    new DocumentFormat.OpenXml.Drawing.Extents { Cx = imageWidthEmu, Cy = imageHeightEmu }),
                                new DocumentFormat.OpenXml.Drawing.PresetGeometry(
                                    new DocumentFormat.OpenXml.Drawing.AdjustValueList()) { Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle }))
                    ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
            ) { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U }
        );

        // 创建段落并添加图片
        var paragraph = new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
            new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties(
                new DocumentFormat.OpenXml.Wordprocessing.Justification { Val = DocumentFormat.OpenXml.Wordprocessing.JustificationValues.Center }
            ),
            new DocumentFormat.OpenXml.Wordprocessing.Run(drawing)
        );

        body.Append(paragraph);
        AddWordEmptyLine(body);
    }

    /// <summary>
    /// 添加Word表格
    /// </summary>
    private void AddWordTable(DocumentFormat.OpenXml.Wordprocessing.Body body, List<Dictionary<string, object>> data, string textColor)
    {
        if (data.Count == 0) return;

        var table = new DocumentFormat.OpenXml.Wordprocessing.Table();

        // 表格属性
        var tableProps = new DocumentFormat.OpenXml.Wordprocessing.TableProperties(
            new DocumentFormat.OpenXml.Wordprocessing.TableBorders(
                new DocumentFormat.OpenXml.Wordprocessing.TopBorder { Val = DocumentFormat.OpenXml.Wordprocessing.BorderValues.Single, Size = 4 },
                new DocumentFormat.OpenXml.Wordprocessing.BottomBorder { Val = DocumentFormat.OpenXml.Wordprocessing.BorderValues.Single, Size = 4 },
                new DocumentFormat.OpenXml.Wordprocessing.LeftBorder { Val = DocumentFormat.OpenXml.Wordprocessing.BorderValues.Single, Size = 4 },
                new DocumentFormat.OpenXml.Wordprocessing.RightBorder { Val = DocumentFormat.OpenXml.Wordprocessing.BorderValues.Single, Size = 4 },
                new DocumentFormat.OpenXml.Wordprocessing.InsideHorizontalBorder { Val = DocumentFormat.OpenXml.Wordprocessing.BorderValues.Single, Size = 4 },
                new DocumentFormat.OpenXml.Wordprocessing.InsideVerticalBorder { Val = DocumentFormat.OpenXml.Wordprocessing.BorderValues.Single, Size = 4 }
            ),
            new DocumentFormat.OpenXml.Wordprocessing.TableWidth { Width = "5000", Type = DocumentFormat.OpenXml.Wordprocessing.TableWidthUnitValues.Pct }
        );
        table.Append(tableProps);

        // 表头行
        var headers = data[0].Keys.ToList();
        var headerRow = new DocumentFormat.OpenXml.Wordprocessing.TableRow();
        foreach (var header in headers)
        {
            var cell = new DocumentFormat.OpenXml.Wordprocessing.TableCell(
                new DocumentFormat.OpenXml.Wordprocessing.TableCellProperties(
                    new DocumentFormat.OpenXml.Wordprocessing.Shading { Fill = "E0E0E0" }
                ),
                new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                    new DocumentFormat.OpenXml.Wordprocessing.Run(
                        new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                            new DocumentFormat.OpenXml.Wordprocessing.Bold(),
                            new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = "20" }
                        ),
                        new DocumentFormat.OpenXml.Wordprocessing.Text(header)
                    )
                )
            );
            headerRow.Append(cell);
        }
        table.Append(headerRow);

        // 数据行（最多显示20行）
        foreach (var row in data.Take(20))
        {
            var dataRow = new DocumentFormat.OpenXml.Wordprocessing.TableRow();
            foreach (var header in headers)
            {
                var value = row.TryGetValue(header, out var v) ? v?.ToString() ?? "" : "";
                var cell = new DocumentFormat.OpenXml.Wordprocessing.TableCell(
                    new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                            new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                                new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = "20" }
                            ),
                            new DocumentFormat.OpenXml.Wordprocessing.Text(value)
                        )
                    )
                );
                dataRow.Append(cell);
            }
            table.Append(dataRow);
        }

        // 如果数据超过20行，添加提示
        if (data.Count > 20)
        {
            AddWordParagraph(body, $"（表格仅显示前20行，共{data.Count}行数据）", "888888", 10, false, false);
        }

        body.Append(table);
        AddWordEmptyLine(body);
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 构建系统Prompt
    /// </summary>
    private static string BuildSystemPrompt(string dbType, string schemaText, string? kpiContext = null, string? customPrompt = null)
    {
        var dbHint = dbType.ToLower() switch
        {
            "postgres" or "postgresql" => "PostgreSQL",
            "sqlserver" or "mssql" => "SQL Server",
            "mysql" => "MySQL",
            "doris" => "Apache Doris",
            _ => "SQL"
        };

        var sb = new StringBuilder();

        // ★★★ 元指令：字段使用规则 ★★★
        sb.AppendLine("# 📋 字段使用规则");
        sb.AppendLine();
        sb.AppendLine("1. **只能使用下面「可用字段清单」中列出的字段名**，字段名必须完全一致（区分大小写）");
        sb.AppendLine("2. **积极匹配**：用户描述的概念可能与字段名不完全一致，请仔细查找语义相近的字段");
        sb.AppendLine("   - 用户说\"手术级别\" → 查找 operationlevel、手术等级、级别 等相关字段");
        sb.AppendLine("   - 用户说\"科室\" → 查找 deptname、科室名称、department 等相关字段");
        sb.AppendLine("3. **尽力完成查询**：即使部分条件无法满足，也要用现有字段生成有意义的SQL");
        sb.AppendLine();
        sb.AppendLine("❌ 禁止的行为：");
        sb.AppendLine("- 臆造不存在的字段名（如清单中有deptname，不能写成dept_name）");
        sb.AppendLine("- 轻易放弃（应先尝试用现有字段完成查询）");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // 角色说明（简化）
        sb.AppendLine($"你是{dbHint}数据分析专家，根据用户需求生成SQL并推荐可视化图表。");
        sb.AppendLine();

        // 当前时间信息
        var now = DateTime.Now;
        sb.AppendLine("## 当前时间");
        sb.AppendLine($"- {now:yyyy年MM月dd日} | 本月：{now.Month}月 | 上月：{now.AddMonths(-1):yyyy年MM月}");
        sb.AppendLine();

        // ★ 可用字段清单（Schema放在禁令后面，让AI先记住规则）
        sb.AppendLine("## 可用字段清单（只能使用以下字段！）");
        sb.AppendLine(schemaText);

        // RAG增强
        if (!string.IsNullOrEmpty(kpiContext))
        {
            sb.AppendLine();
            sb.AppendLine("## 业务指标参考");
            sb.AppendLine(kpiContext);
        }

        sb.AppendLine();
        sb.AppendLine("## 输出格式（严格按此JSON格式返回，不要返回其他内容）");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"answer\": \"对用户问题的简要解释\",");
        sb.AppendLine("  \"detailSql\": \"SELECT 就诊日期,医院名称,科室名称,医生姓名,患者ID,费用金额 FROM 就诊表 WHERE 就诊日期 >= '@startDate' AND 就诊日期 <= '@endDate'\",");
        sb.AppendLine("  \"dateField\": \"就诊日期\",");
        sb.AppendLine("  \"hospitalField\": \"医院名称\",");
        sb.AppendLine("  \"dimensions\": [\"就诊日期\", \"医院名称\", \"科室名称\", \"医生姓名\"],");
        sb.AppendLine("  \"measures\": [");
        sb.AppendLine("    {\"field\": \"费用金额\", \"alias\": \"总费用\", \"agg\": \"SUM\"},");
        sb.AppendLine("    {\"field\": \"*\", \"alias\": \"就诊人次\", \"agg\": \"COUNT\"}");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"kpis\": [");
        sb.AppendLine("    {\"title\": \"总就诊人次\", \"sql\": \"SELECT COUNT(*) as value FROM (...) t\"},");
        sb.AppendLine("    {\"title\": \"总费用\", \"sql\": \"SELECT SUM(费用金额) as value FROM (...) t\", \"unit\": \"元\"},");
        sb.AppendLine("    {\"title\": \"均次费用\", \"sql\": \"SELECT ROUND(SUM(费用金额)/NULLIF(COUNT(*),0),2) as value FROM (...) t\", \"unit\": \"元\"}");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"defaultCharts\": [");
        sb.AppendLine("    {\"type\": \"line\", \"title\": \"按时间趋势\", \"groupBy\": \"就诊日期\", \"measure\": {\"field\": \"*\", \"agg\": \"COUNT\", \"alias\": \"人次\"}},");
        sb.AppendLine("    {\"type\": \"bar\", \"title\": \"按科室分布\", \"groupBy\": \"科室名称\", \"measure\": {\"field\": \"*\", \"agg\": \"COUNT\", \"alias\": \"人次\"}}");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## 字段说明");
        sb.AppendLine("- **detailSql**: 明细查询SQL，时间条件必须用 @startDate 和 @endDate 占位符");
        sb.AppendLine("- **dateField**: 日期字段原名（用于同比环比计算）");
        sb.AppendLine("- **hospitalField**: 医院/机构字段名，没有则设null");
        sb.AppendLine("- **dimensions**: 可分组的维度字段列表");
        sb.AppendLine("- **measures**: 可聚合的度量字段，含字段名、别名、聚合函数");
        sb.AppendLine("- **kpis**: KPI指标卡，sql中用(...)代替detailSql作为子查询");
        sb.AppendLine("- **defaultCharts**: 固定2个图表：时间趋势(line) + 科室分布(bar)");
        sb.AppendLine();
        sb.AppendLine("## 规则");
        sb.AppendLine("1. kpis的sql格式：SELECT 聚合函数(字段) as value FROM (...) t");
        sb.AppendLine("2. dimensions和measures中的字段必须是detailSql中SELECT输出的**别名**（AS后面的名称）");
        sb.AppendLine("3. 只生成SELECT查询，必须返回有效JSON，不要返回markdown或纯文本");
        sb.AppendLine("4. ★★★ 关键规则：kpis/defaultCharts的sql中，(...)会被替换为detailSql作为子查询。");
        sb.AppendLine("   外层查询只能引用detailSql中SELECT的**别名**，不能引用原始表的列名！");
        sb.AppendLine("   例如detailSql中写了 currentage AS 当前年龄，那么kpis的sql中必须用 当前年龄，不能用 currentage");
        sb.AppendLine("   因为子查询 FROM (...) t 的输出列名是别名，不是原始列名");

        // 用户自定义提示词
        if (!string.IsNullOrEmpty(customPrompt))
        {
            sb.AppendLine();
            sb.AppendLine("## 补充说明");
            sb.AppendLine(customPrompt);
        }

        return sb.ToString();
    }

    /// <summary>
    /// 构建RAG上下文（用于表选择和SQL生成）
    /// 使用统一检索服务，同时检索KPI指标和知识库文档
    /// 注意：不再按数据源过滤，因为知识库包含通用业务知识
    /// </summary>
    private async Task<string?> BuildRagContextAsync(string question)
    {
        try
        {
            // 检查RAG是否启用
            var ragEnabledStr = await _configService.GetAsync(ConfigKeys.RagEnabled, "true");
            _logger.LogInformation("RAG配置检查: ai.rag.enabled = {Value}", ragEnabledStr);
            var ragEnabled = ragEnabledStr?.ToLower() == "true";

            if (!ragEnabled)
            {
                _logger.LogInformation("RAG检索增强已禁用，跳过知识库检索");
                return null;
            }

            // 获取RAG配置参数
            var topKStr = await _configService.GetAsync(ConfigKeys.RagTopK, "4");
            var minScoreStr = await _configService.GetAsync(ConfigKeys.RagMinScore, "0.6");
            var topK = int.TryParse(topKStr, out var k) ? k : 4;
            var minScore = float.TryParse(minScoreStr, out var s) ? s : 0.6f;

            _logger.LogInformation("RAG检索参数: topK={TopK}, minScore={MinScore}, question={Question}",
                topK, minScore, question.Length > 50 ? question[..50] + "..." : question);

            // ★ 不传入datasourceId，检索全局知识库
            var ragContext = await _unifiedSearch.GetRagContextAsync(question, null, topK, minScore);

            if (string.IsNullOrEmpty(ragContext))
            {
                _logger.LogInformation("RAG检索返回空结果");
                return null;
            }

            _logger.LogInformation("RAG检索成功，上下文长度: {Length} 字符", ragContext.Length);
            return ragContext;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "检索知识库失败");
            return null;
        }
    }

    /// <summary>
    /// 模式分类 - 判断用户问题属于哪种模式
    /// bi: 指标统计分析（如"统计门诊量"、"分析收入趋势"）
    /// hz360: 患者360查询（如"查询张三的就诊记录"、"找一下身份证号xxx的患者"）
    /// internetsearch: 通用问答（如"什么是DRG"、"如何计算药占比"）
    /// </summary>
    private async Task<ModeClassifyResult> ClassifyModeAsync(string question)
    {
        var result = new ModeClassifyResult { Mode = "bi", Reason = "默认模式" };

        try
        {
            var prompt = $@"你是一个智能分类助手。请分析用户的问题，判断它属于以下哪种类型：

1. bi - 指标统计分析：用户想要查询统计数据、分析趋势、生成图表等。
   例如：统计本月门诊量、分析各科室收入、查询住院人次趋势

2. hz360 - 患者360查询：用户想要查询特定患者的信息，通常会提到患者姓名、身份证号、住院号等。
   例如：查询张三的就诊记录、找一下身份证号320xxx的患者、住院号12345的病人信息

3. internetsearch - 通用问答：用户询问概念解释、操作指南、政策法规等非数据查询问题。
   例如：什么是DRG、如何计算药占比、医保政策有哪些变化

4. report - 报表生成：用户想要生成结构化的报表、明细表、汇总表，通常包含报表、日报、月报、年报、统计表、明细表、汇总表、台账、花名册等关键词，期望以表格形式展示多列多行数据。
   例如：生成本月门诊日报表、2026年住院费用汇总表、各科室收入月报、门诊明细台账

用户问题：{question}

请以JSON格式返回分类结果：
{{{{
  ""mode"": ""bi或hz360或internetsearch或report"",
  ""reason"": ""分类理由"",
  ""patientIdentifier"": ""如果是hz360模式提取患者标识否则为null""
}}}}

只返回JSON，不要其他内容。";

            var messages = new List<LlmMessage> { LlmMessage.User(prompt) };
            var llmResponse = await _llmService.ChatAsync(messages, new LlmOptions
            {
                Temperature = 0.1,
                MaxTokens = 500,
                BusinessType = AiBusinessType.Bi  // 模式分类使用BI配置
            });

            if (llmResponse.Success && !string.IsNullOrEmpty(llmResponse.Content))
            {
                // 解析JSON响应
                var content = llmResponse.Content;
                if (content.Contains("```"))
                {
                    content = Regex.Replace(content, @"```json\s*", "");
                    content = Regex.Replace(content, @"```\s*", "");
                }

                var jsonMatch = Regex.Match(content, @"\{[\s\S]*\}", RegexOptions.Multiline);
                if (jsonMatch.Success)
                {
                    var doc = JsonDocument.Parse(jsonMatch.Value);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("mode", out var mode))
                        result.Mode = mode.GetString() ?? "bi";
                    if (root.TryGetProperty("reason", out var reason))
                        result.Reason = reason.GetString() ?? "";
                    if (root.TryGetProperty("patientIdentifier", out var patientId) && patientId.ValueKind != JsonValueKind.Null)
                        result.PatientIdentifier = patientId.GetString();
                }
            }

            _logger.LogInformation("模式分类结果: {Mode}, 理由: {Reason}", result.Mode, result.Reason);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "模式分类失败，使用默认bi模式");
        }

        return result;
    }

    /// <summary>
    /// 使用AI生成SQL查询患者360 - 让AI理解用户问题并生成正确的WHERE条件
    /// 返回：患者列表、执行的SQL、AI提示词、AI响应
    /// </summary>
    private async Task<(List<Patient360Info> Patients, string? Sql, string AiPrompt, string AiResponse)> QueryPatient360WithAiAsync(string userQuestion, long datasourceId)
    {
        var patients = new List<Patient360Info>();
        string? executedSql = null;
        string aiPrompt = "";
        string aiResponse = "";

        try
        {
            var datasource = await _db.Datasources.FindAsync(datasourceId);
            if (datasource == null)
            {
                return (patients, null, "错误：数据源不存在", "");
            }

            // 获取数据源中的所有表
            var allTables = await _schemaService.GetTablesAsync(datasourceId);

            // 检测患者相关的表或视图
            string? patientTable = null;
            string[] candidateTables = { "ai_360", "v_patient_360", "患者360", "患者信息", "patient_info", "ods_patient", "患者基本信息" };

            foreach (var candidate in candidateTables)
            {
                var found = allTables.FirstOrDefault(t => t.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                {
                    patientTable = found.Name;
                    break;
                }
            }

            if (patientTable == null)
            {
                var matchedTable = allTables.FirstOrDefault(t =>
                    t.Name.Contains("患者", StringComparison.OrdinalIgnoreCase) ||
                    t.Name.Contains("patient", StringComparison.OrdinalIgnoreCase) ||
                    (t.Comment ?? "").Contains("患者"));
                if (matchedTable != null)
                {
                    patientTable = matchedTable.Name;
                }
            }

            if (patientTable == null)
            {
                return (patients, null, "错误：未找到患者相关的表或视图", "");
            }

            // 获取表的字段信息
            var columns = await _schemaService.GetColumnsAsync(datasourceId, patientTable);

            // 构建字段信息给AI
            var columnDescriptions = columns.Select(c =>
                $"  - {c.Name}: {c.DataType}" + (string.IsNullOrEmpty(c.Comment) ? "" : $" ({c.Comment})")).ToList();

            // 让AI生成SQL
            aiPrompt = $@"你是一个SQL生成助手。请根据用户的问题生成MySQL查询的WHERE条件。

## 数据表: `{patientTable}`

## 可用字段:
{string.Join("\n", columnDescriptions.Take(40))}

## 重要字段说明:
- 身份证尾号查询：应该使用 身份证号 字段，用 LIKE '%尾号值' 匹配
- 门诊号/住院号查询：通常对应 就诊号 或 病案号 字段
- 姓名查询：使用 患者姓名 字段，用 LIKE '%姓名%' 匹配
- 电话尾号查询：使用 电话 字段，用 LIKE '%尾号值' 匹配

## 用户问题:
{userQuestion}

## 输出要求:
请返回JSON格式，包含以下字段：
{{
  ""whereClause"": ""WHERE条件（不含WHERE关键字）"",
  ""explanation"": ""简要说明你如何理解用户的查询意图""
}}

注意：
1. 只返回JSON，不要有其他内容
2. 字段名需要用反引号包裹
3. 如果用户提到""尾号""，应该用 LIKE '%值' 而不是 LIKE '%尾号值%'
4. 如果用户提到门诊号或住院号，应该查询 就诊号 字段
5. 字符串值要用单引号包裹
6. 不要添加时间范围条件（如出生日期、就诊日期等），除非用户明确要求
7. 患者360查询只需要根据患者标识（姓名、身份证号、就诊号等）进行查询";

            // 调用AI（使用患者360业务配置）
            var messages = new List<LlmMessage> { LlmMessage.User(aiPrompt) };
            var llmResponse = await _llmService.ChatAsync(messages, new LlmOptions { Temperature = 0.1, MaxTokens = 1000, BusinessType = AiBusinessType.Hz360 });
            aiResponse = llmResponse.Success ? llmResponse.Content : $"AI调用失败: {llmResponse.Error}";

            // 解析AI响应
            string whereClause = "";
            string explanation = "";
            try
            {
                // 提取JSON部分
                var jsonMatch = System.Text.RegularExpressions.Regex.Match(aiResponse, @"\{[\s\S]*\}");
                if (jsonMatch.Success)
                {
                    var json = System.Text.Json.JsonDocument.Parse(jsonMatch.Value);
                    whereClause = json.RootElement.GetProperty("whereClause").GetString() ?? "";
                    explanation = json.RootElement.TryGetProperty("explanation", out var exp) ? exp.GetString() ?? "" : "";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "解析AI响应失败，回退到简单搜索");
                // 回退到简单搜索
                whereClause = $"`患者姓名` LIKE '%{userQuestion.Replace("'", "''")}%'";
            }

            if (string.IsNullOrWhiteSpace(whereClause))
            {
                return (patients, null, aiPrompt, "AI未能生成有效的WHERE条件");
            }

            // 构建SELECT字段
            var selectFields = new List<string>();
            string? patientIdCol = FindColumn(columns, "患者ID", "PatientId", "patient_id", "病人ID", "患者id");
            string? patientNameCol = FindColumn(columns, "患者姓名", "PatientName", "patient_name", "姓名");
            string? genderCol = FindColumn(columns, "性别", "Gender", "sex");
            string? ageCol = FindColumn(columns, "年龄", "Age");
            string? birthDateCol = FindColumn(columns, "出生日期", "BirthDate", "birth_date", "生日");
            string? idCardCol = FindColumn(columns, "身份证号", "IdCard", "id_card", "证件号");
            string? phoneCol = FindColumn(columns, "联系电话", "Phone", "电话", "手机号");
            string? visitDateCol = FindColumn(columns, "最近就诊日期", "LastVisitDate", "就诊日期");
            string? deptCol = FindColumn(columns, "就诊科室", "LastDepartment", "科室");
            string? diagCol = FindColumn(columns, "诊断", "Diagnosis", "LastDiagnosis", "主诊断");
            string? detailUrlCol = FindColumn(columns, "患者360链接", "DetailUrl", "360链接", "链接");

            selectFields.Add(!string.IsNullOrEmpty(patientIdCol) ? $"`{patientIdCol}` as PatientId" : "'' as PatientId");
            selectFields.Add(!string.IsNullOrEmpty(patientNameCol) ? $"`{patientNameCol}` as PatientName" : "'' as PatientName");
            if (!string.IsNullOrEmpty(genderCol)) selectFields.Add($"`{genderCol}` as Gender");
            if (!string.IsNullOrEmpty(ageCol)) selectFields.Add($"`{ageCol}` as Age");
            if (!string.IsNullOrEmpty(birthDateCol)) selectFields.Add($"`{birthDateCol}` as BirthDate");
            if (!string.IsNullOrEmpty(idCardCol)) selectFields.Add($"`{idCardCol}` as IdCard");
            if (!string.IsNullOrEmpty(phoneCol)) selectFields.Add($"`{phoneCol}` as Phone");
            if (!string.IsNullOrEmpty(visitDateCol)) selectFields.Add($"`{visitDateCol}` as LastVisitDate");
            if (!string.IsNullOrEmpty(deptCol)) selectFields.Add($"`{deptCol}` as LastDepartment");
            if (!string.IsNullOrEmpty(diagCol)) selectFields.Add($"`{diagCol}` as LastDiagnosis");
            if (!string.IsNullOrEmpty(detailUrlCol)) selectFields.Add($"`{detailUrlCol}` as DetailUrl");

            // 构建完整SQL
            executedSql = $@"SELECT {string.Join(", ", selectFields)}
FROM `{patientTable}`
WHERE {whereClause}
LIMIT 20";

            _logger.LogInformation("患者360查询SQL（AI生成）: {Sql}", executedSql);

            var data = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, executedSql);

            // 构建AI响应（包含解释和SQL）
            var responseBuilder = new System.Text.StringBuilder();
            responseBuilder.AppendLine($"**AI理解**: {explanation}");
            responseBuilder.AppendLine();
            responseBuilder.AppendLine("**生成的SQL**:");
            responseBuilder.AppendLine("```sql");
            responseBuilder.AppendLine(executedSql);
            responseBuilder.AppendLine("```");
            responseBuilder.AppendLine();
            responseBuilder.AppendLine($"**查询结果**: {data.Count} 条记录");
            aiResponse = responseBuilder.ToString();

            foreach (var row in data)
            {
                var patientId = row.TryGetValue("PatientId", out var pid) ? pid?.ToString() ?? "" : "";
                var detailUrl = row.TryGetValue("DetailUrl", out var url) ? url?.ToString() : null;

                var patient = new Patient360Info
                {
                    PatientId = patientId,
                    PatientName = row.TryGetValue("PatientName", out var pname) ? pname?.ToString() ?? "" : "",
                    Gender = row.TryGetValue("Gender", out var gender) ? gender?.ToString() : null,
                    Age = row.TryGetValue("Age", out var age) && age != null && int.TryParse(age.ToString(), out var ageVal) ? ageVal : null,
                    BirthDate = row.TryGetValue("BirthDate", out var birthDate) && birthDate != null && DateTime.TryParse(birthDate.ToString(), out var bd) ? bd : null,
                    IdCard = MaskIdCard(row.TryGetValue("IdCard", out var idcard) ? idcard?.ToString() : null),
                    Phone = MaskPhone(row.TryGetValue("Phone", out var phone) ? phone?.ToString() : null),
                    LastVisitDate = row.TryGetValue("LastVisitDate", out var visitDate) && visitDate != null && DateTime.TryParse(visitDate.ToString(), out var dt) ? dt : null,
                    LastDepartment = row.TryGetValue("LastDepartment", out var dept) ? dept?.ToString() : null,
                    LastDiagnosis = row.TryGetValue("LastDiagnosis", out var diag) ? diag?.ToString() : null,
                    DetailUrl = !string.IsNullOrEmpty(detailUrl) ? detailUrl : $"/patient360/{patientId}"
                };
                patients.Add(patient);
            }
        }
        catch (Exception ex)
        {
            aiResponse = $"执行异常: {ex.Message}";
            _logger.LogError(ex, "患者360查询失败（AI模式）: {Question}", userQuestion);
        }

        return (patients, executedSql, aiPrompt, aiResponse);
    }

    /// <summary>
    /// 患者360查询（带诊断信息） - 返回患者列表、执行的SQL和诊断信息（旧方法，保留兼容）
    /// </summary>
    private async Task<(List<Patient360Info> Patients, string? Sql, string Diagnostics)> QueryPatient360WithDiagnosticsAsync(string patientIdentifier, long datasourceId)
    {
        var patients = new List<Patient360Info>();
        var diagnostics = new System.Text.StringBuilder();
        string? executedSql = null;

        try
        {
            diagnostics.AppendLine($"## 患者360查询诊断");
            diagnostics.AppendLine($"- 搜索关键词: {patientIdentifier}");
            diagnostics.AppendLine($"- 数据源ID: {datasourceId}");
            diagnostics.AppendLine();

            var datasource = await _db.Datasources.FindAsync(datasourceId);
            if (datasource == null)
            {
                diagnostics.AppendLine("❌ 错误: 数据源不存在");
                _logger.LogWarning("患者360查询：数据源不存在 {DatasourceId}", datasourceId);
                return (patients, null, diagnostics.ToString());
            }
            diagnostics.AppendLine($"- 数据源类型: {datasource.Type}");

            // 获取数据源中的所有表
            var allTables = await _schemaService.GetTablesAsync(datasourceId);
            diagnostics.AppendLine($"- 数据源表数量: {allTables.Count}");

            // 检测患者相关的表或视图（按优先级）
            string? patientTable = null;
            string[] candidateTables = { "ai_360", "v_patient_360", "患者360", "患者信息", "patient_info", "ods_patient", "患者基本信息" };

            diagnostics.AppendLine();
            diagnostics.AppendLine("### 表检测");
            diagnostics.AppendLine($"优先检测的表: {string.Join(", ", candidateTables)}");

            foreach (var candidate in candidateTables)
            {
                var found = allTables.FirstOrDefault(t => t.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                {
                    patientTable = found.Name;
                    diagnostics.AppendLine($"✅ 找到匹配表: {patientTable}");
                    break;
                }
            }

            // 如果没有找到预定义的表，尝试模糊匹配
            if (patientTable == null)
            {
                var matchedTable = allTables.FirstOrDefault(t =>
                    t.Name.Contains("患者", StringComparison.OrdinalIgnoreCase) ||
                    t.Name.Contains("patient", StringComparison.OrdinalIgnoreCase) ||
                    (t.Comment ?? "").Contains("患者"));

                if (matchedTable != null)
                {
                    patientTable = matchedTable.Name;
                    diagnostics.AppendLine($"✅ 模糊匹配到表: {patientTable}");
                }
            }

            if (patientTable == null)
            {
                diagnostics.AppendLine();
                diagnostics.AppendLine("❌ 未找到患者相关的表或视图");
                diagnostics.AppendLine();
                diagnostics.AppendLine("可用表列表（前30张）:");
                foreach (var t in allTables.Take(30))
                {
                    diagnostics.AppendLine($"  - {t.Name} ({t.Comment ?? "无注释"})");
                }
                _logger.LogWarning("患者360查询：数据源 {DatasourceId} 中未找到患者相关的表或视图", datasourceId);
                return (patients, null, diagnostics.ToString());
            }

            _logger.LogInformation("患者360查询：使用表 {Table} 查询患者 {Identifier}", patientTable, patientIdentifier);

            // 获取表的字段信息
            var columns = await _schemaService.GetColumnsAsync(datasourceId, patientTable);
            diagnostics.AppendLine();
            diagnostics.AppendLine($"### 表结构: {patientTable}");
            diagnostics.AppendLine($"字段数量: {columns.Count}");
            diagnostics.AppendLine();
            diagnostics.AppendLine("| 字段名 | 类型 | 注释 |");
            diagnostics.AppendLine("|--------|------|------|");
            foreach (var col in columns.Take(20))
            {
                diagnostics.AppendLine($"| {col.Name} | {col.DataType} | {col.Comment ?? ""} |");
            }
            if (columns.Count > 20) diagnostics.AppendLine($"| ... | ... | (还有{columns.Count - 20}个字段) |");

            // 智能映射字段
            string? patientIdCol = FindColumn(columns, "患者ID", "PatientId", "patient_id", "病人ID", "病人编号");
            string? patientNameCol = FindColumn(columns, "患者姓名", "PatientName", "patient_name", "姓名", "病人姓名", "name");
            string? genderCol = FindColumn(columns, "性别", "Gender", "sex");
            string? ageCol = FindColumn(columns, "年龄", "Age");
            string? idCardCol = FindColumn(columns, "身份证号", "IdCard", "id_card", "证件号", "身份证");
            string? phoneCol = FindColumn(columns, "联系电话", "Phone", "电话", "手机号", "mobile");
            string? visitDateCol = FindColumn(columns, "最近就诊日期", "LastVisitDate", "就诊日期", "visit_date", "最后就诊");
            string? deptCol = FindColumn(columns, "就诊科室", "LastDepartment", "科室", "department", "dept");
            string? diagCol = FindColumn(columns, "诊断", "Diagnosis", "LastDiagnosis", "主诊断", "诊断名称");
            string? inpatientNoCol = FindColumn(columns, "住院号", "inpatient_no", "住院号码");
            string? outpatientNoCol = FindColumn(columns, "门诊号", "outpatient_no", "门诊号码");

            diagnostics.AppendLine();
            diagnostics.AppendLine("### 字段映射结果");
            diagnostics.AppendLine($"- 患者ID: {patientIdCol ?? "❌未找到"}");
            diagnostics.AppendLine($"- 患者姓名: {patientNameCol ?? "❌未找到"}");
            diagnostics.AppendLine($"- 身份证号: {idCardCol ?? "❌未找到"}");
            diagnostics.AppendLine($"- 住院号: {inpatientNoCol ?? "❌未找到"}");
            diagnostics.AppendLine($"- 门诊号: {outpatientNoCol ?? "❌未找到"}");
            diagnostics.AppendLine($"- 性别: {genderCol ?? "未找到"}");
            diagnostics.AppendLine($"- 年龄: {ageCol ?? "未找到"}");

            // 构建SELECT字段列表
            var selectFields = new List<string>();
            if (!string.IsNullOrEmpty(patientIdCol)) selectFields.Add($"`{patientIdCol}` as PatientId");
            else selectFields.Add("'' as PatientId");

            if (!string.IsNullOrEmpty(patientNameCol)) selectFields.Add($"`{patientNameCol}` as PatientName");
            else selectFields.Add("'' as PatientName");

            if (!string.IsNullOrEmpty(genderCol)) selectFields.Add($"`{genderCol}` as Gender");
            if (!string.IsNullOrEmpty(ageCol)) selectFields.Add($"`{ageCol}` as Age");
            if (!string.IsNullOrEmpty(idCardCol)) selectFields.Add($"`{idCardCol}` as IdCard");
            if (!string.IsNullOrEmpty(phoneCol)) selectFields.Add($"`{phoneCol}` as Phone");
            if (!string.IsNullOrEmpty(visitDateCol)) selectFields.Add($"`{visitDateCol}` as LastVisitDate");
            if (!string.IsNullOrEmpty(deptCol)) selectFields.Add($"`{deptCol}` as LastDepartment");
            if (!string.IsNullOrEmpty(diagCol)) selectFields.Add($"`{diagCol}` as LastDiagnosis");

            // 构建WHERE条件
            var whereConditions = new List<string>();
            var escapedIdentifier = patientIdentifier.Replace("'", "''");

            if (!string.IsNullOrEmpty(patientNameCol))
                whereConditions.Add($"`{patientNameCol}` LIKE '%{escapedIdentifier}%'");
            if (!string.IsNullOrEmpty(idCardCol))
                whereConditions.Add($"`{idCardCol}` LIKE '%{escapedIdentifier}%'");
            if (!string.IsNullOrEmpty(inpatientNoCol))
                whereConditions.Add($"`{inpatientNoCol}` LIKE '%{escapedIdentifier}%'");
            if (!string.IsNullOrEmpty(outpatientNoCol))
                whereConditions.Add($"`{outpatientNoCol}` LIKE '%{escapedIdentifier}%'");

            if (whereConditions.Count == 0)
            {
                diagnostics.AppendLine();
                diagnostics.AppendLine("❌ 未找到可搜索的字段（姓名、身份证、住院号、门诊号）");
                _logger.LogWarning("患者360查询：表 {Table} 中未找到可搜索的字段", patientTable);
                return (patients, null, diagnostics.ToString());
            }

            // 构建完整SQL
            executedSql = $@"SELECT {string.Join(", ", selectFields)}
FROM `{patientTable}`
WHERE {string.Join(" OR ", whereConditions)}
LIMIT 20";

            diagnostics.AppendLine();
            diagnostics.AppendLine("### 执行的SQL");
            diagnostics.AppendLine("```sql");
            diagnostics.AppendLine(executedSql);
            diagnostics.AppendLine("```");

            _logger.LogInformation("患者360查询SQL: {Sql}", executedSql);

            var data = await ExecuteSqlAsync(datasource.Type, datasource.ConnString, executedSql);

            diagnostics.AppendLine();
            diagnostics.AppendLine($"### 查询结果: {data.Count} 条记录");

            _logger.LogInformation("患者360查询结果: {Count} 条记录", data.Count);

            foreach (var row in data)
            {
                var patientId = row.TryGetValue("PatientId", out var pid) ? pid?.ToString() ?? "" : "";
                var patient = new Patient360Info
                {
                    PatientId = patientId,
                    PatientName = row.TryGetValue("PatientName", out var pname) ? pname?.ToString() ?? "" : "",
                    Gender = row.TryGetValue("Gender", out var gender) ? gender?.ToString() : null,
                    Age = row.TryGetValue("Age", out var age) && age != null && int.TryParse(age.ToString(), out var ageVal) ? ageVal : null,
                    IdCard = MaskIdCard(row.TryGetValue("IdCard", out var idcard) ? idcard?.ToString() : null),
                    Phone = MaskPhone(row.TryGetValue("Phone", out var phone) ? phone?.ToString() : null),
                    LastVisitDate = row.TryGetValue("LastVisitDate", out var visitDate) && visitDate != null && DateTime.TryParse(visitDate.ToString(), out var dt) ? dt : null,
                    LastDepartment = row.TryGetValue("LastDepartment", out var dept) ? dept?.ToString() : null,
                    LastDiagnosis = row.TryGetValue("LastDiagnosis", out var diag) ? diag?.ToString() : null,
                    DetailUrl = $"/patient360/{patientId}"
                };
                patients.Add(patient);
            }
        }
        catch (Exception ex)
        {
            diagnostics.AppendLine();
            diagnostics.AppendLine($"❌ 执行异常: {ex.Message}");
            _logger.LogError(ex, "患者360查询失败: {Identifier}", patientIdentifier);
        }

        return (patients, executedSql, diagnostics.ToString());
    }

    /// <summary>
    /// 智能查找匹配的字段名
    /// </summary>
    private static string? FindColumn(List<Bi.Application.Services.ColumnInfo> columns, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            // 精确匹配字段名
            var col = columns.FirstOrDefault(c => c.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase));
            if (col != null) return col.Name;

            // 匹配字段注释
            col = columns.FirstOrDefault(c => (c.Comment ?? "").Contains(candidate, StringComparison.OrdinalIgnoreCase));
            if (col != null) return col.Name;
        }

        // 模糊匹配字段名
        foreach (var candidate in candidates)
        {
            var col = columns.FirstOrDefault(c => c.Name.Contains(candidate, StringComparison.OrdinalIgnoreCase));
            if (col != null) return col.Name;
        }

        return null;
    }

    /// <summary>
    /// 身份证号脱敏
    /// </summary>
    private static string? MaskIdCard(string? idCard)
    {
        if (string.IsNullOrEmpty(idCard) || idCard.Length < 10) return idCard;
        return idCard.Substring(0, 6) + "********" + idCard.Substring(idCard.Length - 4);
    }

    /// <summary>
    /// 手机号脱敏
    /// </summary>
    private static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrEmpty(phone) || phone.Length < 7) return phone;
        return phone.Substring(0, 3) + "****" + phone.Substring(phone.Length - 4);
    }

    /// <summary>
    /// 解析LLM响应
    /// </summary>
    private static AiChatResponse ParseLlmResponse(string content)
    {
        var response = new AiChatResponse();

        try
        {
            // 先清理markdown代码块标记
            var cleanedContent = content;
            if (cleanedContent.Contains("```"))
            {
                cleanedContent = Regex.Replace(cleanedContent, @"```json\s*", "");
                cleanedContent = Regex.Replace(cleanedContent, @"```\s*", "");
            }

            // 尝试提取JSON
            var jsonMatch = Regex.Match(cleanedContent, @"\{[\s\S]*\}", RegexOptions.Multiline);
            if (jsonMatch.Success)
            {
                var json = jsonMatch.Value;
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("answer", out var answer))
                    response.Answer = answer.GetString();

                if (root.TryGetProperty("sql", out var sql))
                {
                    var sqlStr = sql.GetString();
                    // 清理SQL中可能的markdown反引号
                    if (!string.IsNullOrEmpty(sqlStr))
                    {
                        sqlStr = sqlStr.Trim();
                        // 移除首尾的sql代码块标记
                        if (sqlStr.StartsWith("```sql", StringComparison.OrdinalIgnoreCase))
                            sqlStr = sqlStr.Substring(6);
                        else if (sqlStr.StartsWith("```"))
                            sqlStr = sqlStr.Substring(3);
                        if (sqlStr.EndsWith("```"))
                            sqlStr = sqlStr.Substring(0, sqlStr.Length - 3);
                        response.Sql = sqlStr.Trim();
                    }
                }

                if (root.TryGetProperty("chartType", out var chartType))
                    response.ChartType = chartType.GetString();

                // 解析明细SQL（新版下钻模式）
                if (root.TryGetProperty("detailSql", out var detailSql))
                    response.DetailSql = CleanSqlString(detailSql.GetString() ?? "");

                // 解析医院字段
                if (root.TryGetProperty("hospitalField", out var hospitalField))
                    response.HospitalField = hospitalField.GetString();

                // 解析日期字段（用于同比环比计算和时间参数替换）
                if (root.TryGetProperty("dateField", out var dateField))
                    response.DateField = dateField.GetString();

                // 解析维度字段列表
                if (root.TryGetProperty("dimensions", out var dims) && dims.ValueKind == JsonValueKind.Array)
                {
                    response.Dimensions = dims.EnumerateArray()
                        .Select(d => d.GetString() ?? "")
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();
                }

                // 解析度量字段列表
                if (root.TryGetProperty("measures", out var measuresArr) && measuresArr.ValueKind == JsonValueKind.Array)
                {
                    response.Measures = measuresArr.EnumerateArray()
                        .Select(m => new MeasureField
                        {
                            Field = m.TryGetProperty("field", out var f) ? f.GetString() ?? "" : "",
                            Alias = m.TryGetProperty("alias", out var a) ? a.GetString() ?? "" : "",
                            Agg = m.TryGetProperty("agg", out var ag) ? ag.GetString() ?? "SUM" : "SUM"
                        })
                        .ToList();
                }

                // 解析KPIs并转换为queries，同时保存原始配置用于刷新
                if (root.TryGetProperty("kpis", out var kpis) && kpis.ValueKind == JsonValueKind.Array)
                {
                    response.Queries ??= new List<QueryItem>();
                    response.KpiConfigs ??= new List<KpiConfig>();  // 保存KPI配置用于刷新

                    foreach (var kpi in kpis.EnumerateArray())
                    {
                        var kpiSqlTemplate = kpi.TryGetProperty("sql", out var ks) ? ks.GetString() ?? "" : "";
                        var kpiTitle = kpi.TryGetProperty("title", out var kt) ? kt.GetString() ?? "" : "";

                        // 保存KPI配置（保留原始SQL模板）
                        response.KpiConfigs.Add(new KpiConfig
                        {
                            Title = kpiTitle,
                            SqlTemplate = kpiSqlTemplate
                        });

                        // 用明细SQL替换占位符生成实际SQL
                        var kpiSql = kpiSqlTemplate;
                        if (!string.IsNullOrEmpty(response.DetailSql) && kpiSql.Contains("(...)"))
                        {
                            kpiSql = kpiSql.Replace("(...)", $"({response.DetailSql})");
                        }
                        response.Queries.Add(new QueryItem
                        {
                            Type = "kpi",
                            Title = kpiTitle,
                            Sql = CleanSqlString(kpiSql),
                            Field = "value"
                        });
                    }
                }

                // 解析默认图表配置并转换为queries，同时保存原始配置用于刷新
                if (root.TryGetProperty("defaultCharts", out var defaultCharts) && defaultCharts.ValueKind == JsonValueKind.Array)
                {
                    response.Queries ??= new List<QueryItem>();
                    response.DefaultChartsConfig ??= new List<DefaultChartConfig>();

                    foreach (var chart in defaultCharts.EnumerateArray())
                    {
                        var defaultChartType = chart.TryGetProperty("type", out var ct) ? ct.GetString() ?? "bar" : "bar";
                        var defaultChartTitle = chart.TryGetProperty("title", out var cti) ? cti.GetString() ?? "" : "";
                        var defaultGroupBy = chart.TryGetProperty("groupBy", out var gb) ? gb.GetString() ?? "" : "";

                        // 从measure中获取聚合信息
                        var measureField = "*";
                        var measureAgg = "COUNT";
                        var measureAlias = "数量";
                        if (chart.TryGetProperty("measure", out var measure))
                        {
                            measureField = measure.TryGetProperty("field", out var mf) ? mf.GetString() ?? "*" : "*";
                            measureAgg = measure.TryGetProperty("agg", out var ma) ? ma.GetString() ?? "COUNT" : "COUNT";
                            measureAlias = measure.TryGetProperty("alias", out var mal) ? mal.GetString() ?? "数量" : "数量";
                        }

                        // 保存图表配置用于后续刷新
                        response.DefaultChartsConfig.Add(new DefaultChartConfig
                        {
                            Type = defaultChartType,
                            Title = defaultChartTitle,
                            GroupBy = defaultGroupBy,
                            Measure = new MeasureField { Field = measureField, Agg = measureAgg, Alias = measureAlias }
                        });

                        // 基于明细SQL生成聚合查询
                        if (!string.IsNullOrEmpty(response.DetailSql) && !string.IsNullOrEmpty(defaultGroupBy))
                        {
                            var aggExpr = measureField == "*" ? $"{measureAgg}(*)" : $"{measureAgg}({measureField})";
                            var chartSql = $"SELECT {defaultGroupBy}, {aggExpr} as {measureAlias} FROM ({response.DetailSql}) t GROUP BY {defaultGroupBy} ORDER BY {measureAlias} DESC LIMIT 50";
                            response.Queries.Add(new QueryItem
                            {
                                Type = defaultChartType,
                                Title = defaultChartTitle,
                                Sql = chartSql
                            });
                        }
                    }
                }

                // 兼容旧版queries数组
                if (root.TryGetProperty("queries", out var queries) && queries.ValueKind == JsonValueKind.Array)
                {
                    response.Queries ??= new List<QueryItem>();
                    var oldQueries = queries.EnumerateArray()
                        .Select(q => new QueryItem
                        {
                            Type = q.TryGetProperty("type", out var t) ? t.GetString() ?? "bar" : "bar",
                            Title = q.TryGetProperty("title", out var ti) ? ti.GetString() ?? "" : "",
                            Sql = CleanSqlString(q.TryGetProperty("sql", out var s) ? s.GetString() ?? "" : ""),
                            Field = q.TryGetProperty("field", out var f) ? f.GetString() : null
                        })
                        .Where(q => !string.IsNullOrEmpty(q.Sql))
                        .ToList();
                    response.Queries.AddRange(oldQueries);
                }

                // 兼容旧版chartConfig
                if (root.TryGetProperty("chartConfig", out var config))
                {
                    response.ChartConfig = new ChartConfigSuggestion();

                    if (config.TryGetProperty("dimensions", out var configDims))
                    {
                        response.ChartConfig.Dimensions = configDims.EnumerateArray()
                            .Select(d => d.GetString() ?? "")
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToList();
                    }

                    if (config.TryGetProperty("measures", out var measures))
                    {
                        response.ChartConfig.Measures = measures.EnumerateArray()
                            .Select(m => new MeasureSuggestion
                            {
                                Field = m.TryGetProperty("field", out var f) ? f.GetString() ?? "" : "",
                                AggType = m.TryGetProperty("aggType", out var a) ? a.GetString() ?? "sum" : "sum",
                                Alias = m.TryGetProperty("alias", out var al) ? al.GetString() : null
                            })
                            .ToList();
                    }

                    if (config.TryGetProperty("title", out var title))
                        response.ChartConfig.Title = title.GetString();
                }
            }
            else
            {
                // 无法解析JSON，将整个内容作为answer
                response.Answer = content;
                response.Error = "AI返回格式不正确，无法解析SQL";
            }
        }
        catch (Exception ex)
        {
            response.Answer = content;
            response.Error = $"解析AI响应失败: {ex.Message}";
        }

        return response;
    }

    /// <summary>
    /// 清理SQL字符串，移除markdown代码块标记和SQL注释
    /// 移除注释是为了防止子查询拼接时注释影响后续语法（如 -- 注释会把 ) t 也注释掉）
    /// ★ 同时替换中文标点为英文标点（AI模型如qwen3.5-plus有时会输出中文标点）
    /// </summary>
    private static string CleanSqlString(string sql)
    {
        if (string.IsNullOrEmpty(sql)) return sql;

        sql = sql.Trim();

        // 移除markdown代码块标记
        if (sql.StartsWith("```sql", StringComparison.OrdinalIgnoreCase))
            sql = sql.Substring(6);
        else if (sql.StartsWith("```"))
            sql = sql.Substring(3);
        if (sql.EndsWith("```"))
            sql = sql.Substring(0, sql.Length - 3);

        // ★ 替换中文标点为英文标点（AI有时会输出中文标点导致SQL语法错误）
        // 注意：只替换SQL语法相关的标点，不替换字符串字面量内的中文标点
        sql = ReplaceChinPunctuationOutsideStrings(sql);

        // 移除SQL单行注释（-- 开头的注释）
        // 注意：需要保留字符串中的 -- ，所以只移除行尾的注释
        sql = Regex.Replace(sql, @"--[^\r\n]*", " ");

        // 移除SQL多行注释（/* ... */）
        sql = Regex.Replace(sql, @"/\*[\s\S]*?\*/", " ");

        // 清理多余空白
        sql = Regex.Replace(sql, @"\s+", " ");

        return sql.Trim();
    }

    /// <summary>
    /// 替换SQL中字符串字面量之外的中文标点为英文标点
    /// 避免影响字符串内的中文内容（如 WHERE name = '张三，李四'）
    /// </summary>
    private static string ReplaceChinPunctuationOutsideStrings(string sql)
    {
        var sb = new StringBuilder(sql.Length);
        bool inSingleQuote = false;
        bool inDoubleQuote = false;

        for (int i = 0; i < sql.Length; i++)
        {
            char c = sql[i];

            // 跟踪是否在字符串字面量内
            if (c == '\'' && !inDoubleQuote)
            {
                // 检查是否是转义的引号 ''
                if (i + 1 < sql.Length && sql[i + 1] == '\'')
                {
                    sb.Append(c);
                    sb.Append(sql[i + 1]);
                    i++;
                    continue;
                }
                inSingleQuote = !inSingleQuote;
                sb.Append(c);
                continue;
            }
            if (c == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
                sb.Append(c);
                continue;
            }

            // 在字符串外部，替换中文标点
            if (!inSingleQuote && !inDoubleQuote)
            {
                switch (c)
                {
                    case '，': sb.Append(','); continue;  // 中文逗号 → 英文逗号
                    case '（': sb.Append('('); continue;  // 中文左括号 → 英文左括号
                    case '）': sb.Append(')'); continue;  // 中文右括号 → 英文右括号
                    case '；': sb.Append(';'); continue;  // 中文分号 → 英文分号
                    case '＝': sb.Append('='); continue;  // 中文等号 → 英文等号
                    case '＞': sb.Append('>'); continue;  // 中文大于 → 英文大于
                    case '＜': sb.Append('<'); continue;  // 中文小于 → 英文小于
                    case '＊': sb.Append('*'); continue;  // 中文星号 → 英文星号
                }
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>
    /// 验证SQL是否安全（仅允许SELECT）
    /// </summary>
    private static bool IsSafeSelectSql(string sql)
    {
        var normalized = sql.Trim().ToUpper();

        // 必须以SELECT开头
        if (!normalized.StartsWith("SELECT"))
            return false;

        // 禁止危险关键字
        var dangerousKeywords = new[] { "INSERT", "UPDATE", "DELETE", "DROP", "TRUNCATE", "ALTER", "CREATE", "EXEC", "EXECUTE", "GRANT", "REVOKE" };
        foreach (var keyword in dangerousKeywords)
        {
            if (Regex.IsMatch(normalized, $@"\b{keyword}\b"))
                return false;
        }

        return true;
    }

    #endregion

    #region SQL字段验证

    /// <summary>
    /// 验证AI生成的SQL中的字段是否存在于表结构中
    /// </summary>
    /// <param name="response">AI响应（包含SQL）</param>
    /// <param name="datasourceId">数据源ID</param>
    /// <param name="tableNames">使用的表名列表</param>
    /// <returns>验证结果：(是否通过, 不存在的字段列表)</returns>
    private async Task<(bool IsValid, List<string> InvalidFields)> ValidateSqlFieldsAsync(
        AiChatResponse response,
        long datasourceId,
        List<string> tableNames)
    {
        // 收集所有SQL
        var allSqls = new List<string>();

        if (!string.IsNullOrEmpty(response.DetailSql))
            allSqls.Add(response.DetailSql);

        if (!string.IsNullOrEmpty(response.Sql))
            allSqls.Add(response.Sql);

        if (response.Queries != null)
        {
            allSqls.AddRange(response.Queries
                .Where(q => !string.IsNullOrEmpty(q.Sql))
                .Select(q => q.Sql!));
        }

        if (allSqls.Count == 0)
            return (true, new List<string>());

        // ★ 首先检查SQL中使用的表是否都在允许的表列表中
        // 如果使用了未选中的表，动态获取这些表的字段
        var actualTablesToCheck = new HashSet<string>(tableNames, StringComparer.OrdinalIgnoreCase);
        foreach (var sql in allSqls)
        {
            var usedTables = SqlFieldExtractor.ExtractTableNames(sql);
            foreach (var tableName in usedTables)
            {
                if (!actualTablesToCheck.Contains(tableName))
                {
                    _logger.LogWarning("SQL使用了未选中的表: {TableName}，将其加入验证范围", tableName);
                    actualTablesToCheck.Add(tableName);
                }
            }
        }

        // 获取所有相关表的字段列表
        var allValidFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _logger.LogDebug("SQL字段验证：检查 {TableCount} 张表的字段", actualTablesToCheck.Count);
        foreach (var tableName in actualTablesToCheck)
        {
            try
            {
                var columns = await _schemaService.GetColumnsAsync(datasourceId, tableName);
                _logger.LogDebug("表 {TableName} 有 {ColumnCount} 个字段", tableName, columns.Count);
                foreach (var col in columns)
                {
                    allValidFields.Add(col.Name);
                }
            }
            catch (Exception ex)
            {
                // 表可能不存在，记录警告但继续验证
                _logger.LogWarning("获取表 {TableName} 的字段失败: {Error}", tableName, ex.Message);
            }
        }
        _logger.LogDebug("SQL字段验证：共有 {FieldCount} 个有效字段", allValidFields.Count);

        // 验证所有SQL中的字段（传入实际使用的表名列表用于过滤）
        var allInvalidFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ★ 获取detailSql用于替换KPI模板中的(...)占位符
        var detailSql = response.DetailSql;

        foreach (var sql in allSqls)
        {
            // ★ 如果SQL包含(...)占位符且有detailSql，先替换成完整SQL再验证
            // 这样才能检测子查询别名作用域问题
            var sqlToValidate = sql;
            if (!string.IsNullOrEmpty(detailSql) && sql.Contains("(...)"))
            {
                sqlToValidate = sql.Replace("(...)", $"({detailSql})");
                _logger.LogDebug("SQL字段验证：KPI模板替换后: {Sql}", sqlToValidate.Length > 200 ? sqlToValidate[..200] : sqlToValidate);
            }

            // 先提取SQL中使用的所有字段，用于调试
            var extractedFields = SqlFieldExtractor.ExtractFields(sqlToValidate, actualTablesToCheck);
            _logger.LogDebug("SQL字段验证：从SQL中提取了 {FieldCount} 个字段: {Fields}",
                extractedFields.Count, string.Join(", ", extractedFields));

            var invalidFields = SqlFieldExtractor.ValidateFields(sqlToValidate, allValidFields, actualTablesToCheck);
            if (invalidFields.Count > 0)
            {
                _logger.LogWarning("SQL字段验证：发现 {Count} 个无效字段: {Fields}（SQL: {Sql}）",
                    invalidFields.Count, string.Join(", ", invalidFields),
                    sqlToValidate.Length > 100 ? sqlToValidate[..100] : sqlToValidate);
            }
            foreach (var field in invalidFields)
            {
                allInvalidFields.Add(field);
            }
        }

        return (allInvalidFields.Count == 0, allInvalidFields.ToList());
    }

    /// <summary>
    /// 构建SQL字段修正提示，让AI重新生成正确的SQL
    /// </summary>
    private string BuildSqlCorrectionPrompt(
        string originalQuestion,
        string originalResponse,
        List<string> invalidFields,
        string schemaText)
    {
        var sb = new StringBuilder();
        sb.AppendLine("【SQL字段错误修正】");
        sb.AppendLine();
        sb.AppendLine("你之前生成的SQL包含了表结构中不存在的字段，请重新生成正确的SQL。");
        sb.AppendLine();
        sb.AppendLine($"❌ 不存在的字段：{string.Join(", ", invalidFields)}");
        sb.AppendLine();
        sb.AppendLine("## 可用的表结构（只能使用这些字段）：");
        sb.AppendLine(schemaText);
        sb.AppendLine();
        sb.AppendLine("## 原始用户问题：");
        sb.AppendLine(originalQuestion);
        sb.AppendLine();
        sb.AppendLine("## 你之前的错误回答：");
        sb.AppendLine(originalResponse);
        sb.AppendLine();
        sb.AppendLine("请重新生成正确的JSON响应，确保所有SQL中的字段都来自上面的表结构。");
        sb.AppendLine("如果你需要的字段在表结构中不存在，请使用最接近的可用字段或从SQL中移除该条件。");
        sb.AppendLine();
        sb.AppendLine("★ 特别注意：kpis的sql会用detailSql作为子查询（FROM (...) t）。");
        sb.AppendLine("外层查询只能引用detailSql中SELECT的**别名**（AS后面的名称），不能引用原始表列名！");
        sb.AppendLine("例如detailSql中写了 currentage AS 当前年龄，kpis的sql必须用 当前年龄，不能用 currentage。");

        return sb.ToString();
    }

    /// <summary>
    /// SQL语法预验证：试执行SQL（LIMIT 1），收集语法错误
    /// 只捕获语法错误（Syntax error），忽略运行时错误（如超时、数据不存在等）
    /// </summary>
    private async Task<List<(string SqlType, string Sql, string Error)>> PreValidateSqlSyntaxAsync(
        AiChatResponse response, string dbType, string connString)
    {
        var errors = new List<(string SqlType, string Sql, string Error)>();

        // 1. 验证 detailSql
        if (!string.IsNullOrEmpty(response.DetailSql))
        {
            var testSql = $"SELECT * FROM ({response.DetailSql}) __syntax_check__ LIMIT 1";
            var error = await TryExecuteSqlForSyntaxCheck(dbType, connString, testSql);
            if (error != null && IsSyntaxError(error))
            {
                errors.Add(("detailSql", response.DetailSql, error));
                // detailSql有语法错误，KPI/Chart SQL大概率也会失败（因为它们包含detailSql作为子查询）
                // 不需要再验证其他SQL了，直接返回让AI修正
                _logger.LogWarning("detailSql语法错误: {Error}", error);
                return errors;
            }
        }

        // 2. 验证 Queries 中的 KPI/Chart SQL
        if (response.Queries != null)
        {
            foreach (var query in response.Queries)
            {
                if (string.IsNullOrEmpty(query.Sql)) continue;
                if (!string.IsNullOrEmpty(query.Error)) continue; // 已有错误，跳过

                var testSql = query.Sql;
                // KPI SQL通常是聚合查询，不需要加LIMIT
                // 但为了安全，如果不是聚合查询就加LIMIT
                if (!testSql.Contains("COUNT(", StringComparison.OrdinalIgnoreCase) &&
                    !testSql.Contains("SUM(", StringComparison.OrdinalIgnoreCase) &&
                    !testSql.Contains("AVG(", StringComparison.OrdinalIgnoreCase) &&
                    !testSql.Contains("MAX(", StringComparison.OrdinalIgnoreCase) &&
                    !testSql.Contains("MIN(", StringComparison.OrdinalIgnoreCase))
                {
                    if (!testSql.Contains("LIMIT", StringComparison.OrdinalIgnoreCase))
                        testSql += " LIMIT 1";
                }

                var error = await TryExecuteSqlForSyntaxCheck(dbType, connString, testSql);
                if (error != null && IsSyntaxError(error))
                {
                    errors.Add((query.Type ?? "query", query.Sql, error));
                    _logger.LogWarning("{Type} SQL语法错误: {Error}", query.Type, error);
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// 尝试执行SQL进行语法检查，返回错误信息（null表示成功）
    /// </summary>
    private async Task<string?> TryExecuteSqlForSyntaxCheck(string dbType, string connString, string sql)
    {
        try
        {
            using var conn = CreateConnection(dbType, connString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 10; // 较短的超时，只是语法检查
            using var reader = await cmd.ExecuteReaderAsync();
            // 不需要读取数据，能打开reader说明语法正确
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// 判断错误是否为SQL语法错误（非运行时错误）
    /// </summary>
    private static bool IsSyntaxError(string errorMessage)
    {
        var syntaxPatterns = new[]
        {
            "syntax error",           // 通用
            "Syntax error",           // Doris
            "SQL syntax",             // MySQL
            "parse error",            // 通用
            "error when parsing",     // Doris - "Please check your sql, we meet an error when parsing"
            "Unexpected",             // Doris
            "Encountered:",           // Doris
            "Unknown column",         // MySQL - 列不存在也算（子查询别名问题）
            "doesn't exist",          // 表/列不存在
            "Unknown function",       // 未知函数
            "not found",              // 函数/列不存在
            "Unresolved",             // Doris - 未解析的列
            "Please check your sql",  // Doris - 通用SQL检查错误
        };

        return syntaxPatterns.Any(p => errorMessage.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 构建SQL语法修正提示，将语法错误信息发送给AI修正
    /// </summary>
    private string BuildSqlSyntaxCorrectionPrompt(
        string originalQuestion,
        AiChatResponse response,
        List<(string SqlType, string Sql, string Error)> syntaxErrors,
        string schemaText,
        string dbType)
    {
        var sb = new StringBuilder();
        sb.AppendLine("【SQL语法错误修正】");
        sb.AppendLine();
        sb.AppendLine($"你之前生成的SQL在 {dbType} 数据库上执行时出现了语法错误，请修正后重新生成。");
        sb.AppendLine();

        foreach (var (sqlType, sql, error) in syntaxErrors)
        {
            sb.AppendLine($"❌ **{sqlType}** 语法错误:");
            sb.AppendLine($"  SQL: {(sql.Length > 500 ? sql[..500] + "..." : sql)}");
            sb.AppendLine($"  错误: {error}");
            sb.AppendLine();
        }

        // 针对常见错误给出提示
        var allErrors = string.Join(" ", syntaxErrors.Select(e => e.Error));
        if (allErrors.Contains("GROUP_CONCAT", StringComparison.OrdinalIgnoreCase) ||
            allErrors.Contains("SEPARATOR", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("💡 提示：当前数据库可能不支持 GROUP_CONCAT(... SEPARATOR ...) 语法。");
            sb.AppendLine("  - Doris/StarRocks 使用: GROUP_CONCAT(字段) 不带 SEPARATOR 关键字");
            sb.AppendLine("  - 或者改用其他方式实现，如去掉诊断列表拼接");
            sb.AppendLine();
        }

        if (allErrors.Contains("Unknown column", StringComparison.OrdinalIgnoreCase) ||
            allErrors.Contains("Unresolved", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("💡 提示：外层查询引用子查询字段时，必须使用子查询SELECT中的别名（AS后面的名称），不能使用原始表列名。");
            sb.AppendLine();
        }

        sb.AppendLine("## 可用的表结构：");
        sb.AppendLine(schemaText);
        sb.AppendLine();
        sb.AppendLine("## 原始用户问题：");
        sb.AppendLine(originalQuestion);
        sb.AppendLine();
        sb.AppendLine("请重新生成正确的JSON响应，修正所有SQL语法错误。");
        sb.AppendLine("注意：必须严格使用 " + dbType + " 数据库支持的SQL语法。");
        sb.AppendLine("★ kpis的sql中(...)会被替换为detailSql，外层只能用detailSql中SELECT的别名。");

        return sb.ToString();
    }

    #endregion

    #region SQL执行

    /// <summary>
    /// 执行SQL查询
    /// </summary>
    private static async Task<List<Dictionary<string, object?>>> ExecuteSqlAsync(string dbType, string connString, string sql)
    {
        using var conn = CreateConnection(dbType, connString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 30;

        using var reader = await cmd.ExecuteReaderAsync();
        var result = new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            result.Add(row);
        }

        return result;
    }

    private static DbConnection CreateConnection(string type, string connString)
    {
        return type.ToLower() switch
        {
            "postgres" or "postgresql" => new NpgsqlConnection(connString),
            "sqlserver" or "mssql" => new SqlConnection(connString),
            "mysql" => new MySqlConnection(EnsureMySqlConnStringParams(connString)),
            "doris" => new MySqlConnection(EnsureMySqlConnStringParams(connString)),  // Doris使用MySQL协议
            _ => throw new ArgumentException($"不支持的数据源类型: {type}")
        };
    }

    private static string EnsureMySqlConnStringParams(string connString)
    {
        if (connString.Contains("ConnectionReset", StringComparison.OrdinalIgnoreCase))
            return connString;
        return connString.TrimEnd(';') + ";ConnectionReset=false;AllowUserVariables=true;CharSet=utf8mb4";
    }

    #endregion

    #region 同比环比计算辅助方法

    /// <summary>
    /// 替换SQL中的时间参数（支持占位符和日期字面量）
    /// </summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="startDate">开始日期</param>
    /// <param name="endDate">结束日期</param>
    /// <param name="dateField">日期字段名（可选，用于替换日期字面量）</param>
    /// <returns>替换后的SQL</returns>
    private static string ReplaceDateParameters(string sql, DateTime startDate, DateTime endDate, string? dateField = null)
    {
        if (string.IsNullOrEmpty(sql)) return sql;

        var result = sql;
        var startStr = startDate.ToString("yyyy-MM-dd");
        var endStr = endDate.ToString("yyyy-MM-dd");

        // 1. 替换 @startDate 和 @endDate 占位符
        result = Regex.Replace(result, @"'@startDate'", $"'{startStr}'", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"@startDate", $"'{startStr}'", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"'@endDate'", $"'{endStr}'", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"@endDate", $"'{endStr}'", RegexOptions.IgnoreCase);

        // 2. 替换日期字面量（当有dateField时）
        // 匹配模式: dateField >= 'YYYY-MM-DD' 或 dateField <= 'YYYY-MM-DD'
        // 或 BETWEEN 'YYYY-MM-DD' AND 'YYYY-MM-DD'
        if (!string.IsNullOrEmpty(dateField))
        {
            // 转义字段名用于正则匹配
            var escapedField = Regex.Escape(dateField);

            // 替换 dateField >= 'YYYY-MM-DD' 格式（开始日期）
            result = Regex.Replace(result,
                $@"({escapedField}\s*>=?\s*)'(\d{{4}}-\d{{2}}-\d{{2}})'",
                $"$1'{startStr}'",
                RegexOptions.IgnoreCase);

            // 替换 dateField <= 'YYYY-MM-DD' 格式（结束日期）
            result = Regex.Replace(result,
                $@"({escapedField}\s*<=?\s*)'(\d{{4}}-\d{{2}}-\d{{2}})'",
                $"$1'{endStr}'",
                RegexOptions.IgnoreCase);

            // 替换 BETWEEN 'YYYY-MM-DD' AND 'YYYY-MM-DD' 格式
            result = Regex.Replace(result,
                $@"({escapedField}\s+BETWEEN\s+)'(\d{{4}}-\d{{2}}-\d{{2}})'\s+AND\s+'(\d{{4}}-\d{{2}}-\d{{2}})'",
                $"$1'{startStr}' AND '{endStr}'",
                RegexOptions.IgnoreCase);
        }

        // 3. 通用日期字面量替换（当没有dateField或字段名不匹配时的后备方案）
        // 匹配常见日期字段名 + 日期比较
        var commonDateFields = new[] { "rq", "date", "日期", "time", "时间", "createdat", "updatedat",
            "dischargedate", "admissiondate", "出院日期", "入院日期", "就诊日期", "登记日期",
            "recorddate", "visitdate", "operatedate", "examdate", "reportdate",
            "诊断日期", "检查日期", "报告日期", "手术日期", "开始日期", "结束日期" };

        foreach (var field in commonDateFields)
        {
            var escapedField = Regex.Escape(field);

            // 替换 field >= 'YYYY-MM-DD' 格式（带可选时间部分）
            result = Regex.Replace(result,
                $@"({escapedField}\s*>=?\s*)'(\d{{4}}-\d{{2}}-\d{{2}})(?:\s+\d{{2}}:\d{{2}}:\d{{2}})?'",
                $"$1'{startStr}'",
                RegexOptions.IgnoreCase);

            // 替换 field <= 'YYYY-MM-DD' 格式（带可选时间部分）
            result = Regex.Replace(result,
                $@"({escapedField}\s*<=?\s*)'(\d{{4}}-\d{{2}}-\d{{2}})(?:\s+\d{{2}}:\d{{2}}:\d{{2}})?'",
                $"$1'{endStr}'",
                RegexOptions.IgnoreCase);

            // 替换 BETWEEN 格式（带可选时间部分）
            result = Regex.Replace(result,
                $@"({escapedField}\s+BETWEEN\s+)'(\d{{4}}-\d{{2}}-\d{{2}})(?:\s+\d{{2}}:\d{{2}}:\d{{2}})?'\s+AND\s+'(\d{{4}}-\d{{2}}-\d{{2}})(?:\s+\d{{2}}:\d{{2}}:\d{{2}})?'",
                $"$1'{startStr}' AND '{endStr}'",
                RegexOptions.IgnoreCase);
        }

        // 4. ★ 通用模式：任意字段名+日期比较（最后的后备方案）
        // 匹配模式: 任意中英文字段名 >= 'YYYY-MM-DD'
        result = Regex.Replace(result,
            @"([\w\u4e00-\u9fa5]+\s*>=\s*)'(\d{4}-\d{2}-\d{2})(?:\s+\d{2}:\d{2}:\d{2})?'",
            $"$1'{startStr}'",
            RegexOptions.IgnoreCase);

        result = Regex.Replace(result,
            @"([\w\u4e00-\u9fa5]+\s*<=\s*)'(\d{4}-\d{2}-\d{2})(?:\s+\d{2}:\d{2}:\d{2})?'",
            $"$1'{endStr}'",
            RegexOptions.IgnoreCase);

        // 匹配模式: 任意中英文字段名 BETWEEN 'YYYY-MM-DD' AND 'YYYY-MM-DD'
        result = Regex.Replace(result,
            @"([\w\u4e00-\u9fa5]+\s+BETWEEN\s+)'(\d{4}-\d{2}-\d{2})(?:\s+\d{2}:\d{2}:\d{2})?'\s+AND\s+'(\d{4}-\d{2}-\d{2})(?:\s+\d{2}:\d{2}:\d{2})?'",
            $"$1'{startStr}' AND '{endStr}'",
            RegexOptions.IgnoreCase);

        return result;
    }

    /// <summary>
    /// 从问题文本中解析时间范围（格式：（时间范围：2025-01-01 至 2025-01-31））
    /// </summary>
    private (DateTime? StartDate, DateTime? EndDate) ParseDateRangeFromQuestion(string question)
    {
        // 匹配格式：（时间范围：YYYY-MM-DD 至 YYYY-MM-DD）
        var match = Regex.Match(question, @"[（\(]时间范围[：:](\d{4}-\d{2}-\d{2})\s*[至到-]\s*(\d{4}-\d{2}-\d{2})[）\)]");
        if (match.Success)
        {
            if (DateTime.TryParse(match.Groups[1].Value, out var start) &&
                DateTime.TryParse(match.Groups[2].Value, out var end))
            {
                return (start, end);
            }
        }
        return (null, null);
    }

    /// <summary>
    /// 提取问题文本的干净标题（去掉时间范围后缀）
    /// </summary>
    private static string ExtractCleanTitle(string question, int maxLength = 50)
    {
        if (string.IsNullOrEmpty(question)) return question;

        // 移除时间范围后缀：（时间范围：YYYY-MM-DD 至 YYYY-MM-DD）
        var cleaned = Regex.Replace(question, @"\s*[（\(]时间范围[：:][^）\)]*[）\)]", "", RegexOptions.IgnoreCase);
        cleaned = cleaned.Trim();

        // 截断到最大长度
        if (cleaned.Length > maxLength)
        {
            cleaned = cleaned.Substring(0, maxLength) + "...";
        }

        return cleaned;
    }

    /// <summary>
    /// 计算同比时间范围（去年同期）
    /// </summary>
    private (DateTime StartDate, DateTime EndDate) CalculateYoyDateRange(DateTime startDate, DateTime endDate)
    {
        // 同比：去年同期
        return (startDate.AddYears(-1), endDate.AddYears(-1));
    }

    /// <summary>
    /// 计算环比时间范围（上一周期）
    /// </summary>
    private (DateTime StartDate, DateTime EndDate) CalculateMomDateRange(DateTime startDate, DateTime endDate)
    {
        // 计算周期长度
        var periodDays = (endDate - startDate).Days + 1;

        // 环比：往前推相同天数
        var momEndDate = startDate.AddDays(-1);
        var momStartDate = momEndDate.AddDays(-(periodDays - 1));

        return (momStartDate, momEndDate);
    }

    /// <summary>
    /// 从维度字段中找到日期字段
    /// </summary>
    private string? FindDateField(List<string> dimensions)
    {
        // 日期字段关键词
        var dateKeywords = new[] { "日期", "时间", "date", "time", "年", "月" };

        foreach (var dim in dimensions)
        {
            if (dateKeywords.Any(kw => dim.Contains(kw, StringComparison.OrdinalIgnoreCase)))
            {
                return dim;
            }
        }
        return null;
    }

    /// <summary>
    /// 替换SQL中的日期条件，生成同期查询SQL
    /// 改进：不依赖字段名参数，直接在SQL中查找并替换日期格式的值
    /// </summary>
    private string ReplaceDateCondition(string sql, string dateField, DateTime origStart, DateTime origEnd, DateTime newStart, DateTime newEnd)
    {
        // 格式化日期
        var origStartStr = origStart.ToString("yyyy-MM-dd");
        var origEndStr = origEnd.ToString("yyyy-MM-dd");
        var newStartStr = newStart.ToString("yyyy-MM-dd");
        var newEndStr = newEnd.ToString("yyyy-MM-dd");

        var result = sql;

        // 改进策略：直接查找并替换日期值，不依赖字段名
        // 这样无论SQL中使用的是原字段名(regdate)还是别名(挂号日期)都能匹配

        // 1. 替换开始日期（各种可能的格式）
        // 格式: >= 'yyyy-MM-dd' 或 >= 'yyyy-MM-dd HH:mm:ss'
        result = Regex.Replace(result,
            $@">=\s*'{Regex.Escape(origStartStr)}(\s+\d{{2}}:\d{{2}}:\d{{2}})?'",
            $">= '{newStartStr}$1'",
            RegexOptions.IgnoreCase);

        // 格式: > 'yyyy-MM-dd' (不带等号)
        result = Regex.Replace(result,
            $@">\s*'{Regex.Escape(origStartStr)}(\s+\d{{2}}:\d{{2}}:\d{{2}})?'",
            $"> '{newStartStr}$1'",
            RegexOptions.IgnoreCase);

        // 2. 替换结束日期
        // 格式: <= 'yyyy-MM-dd' 或 <= 'yyyy-MM-dd HH:mm:ss'
        result = Regex.Replace(result,
            $@"<=\s*'{Regex.Escape(origEndStr)}(\s+\d{{2}}:\d{{2}}:\d{{2}})?'",
            $"<= '{newEndStr}$1'",
            RegexOptions.IgnoreCase);

        // 格式: < 'yyyy-MM-dd' (不带等号)
        result = Regex.Replace(result,
            $@"<\s*'{Regex.Escape(origEndStr)}(\s+\d{{2}}:\d{{2}}:\d{{2}})?'",
            $"< '{newEndStr}$1'",
            RegexOptions.IgnoreCase);

        // 3. 处理BETWEEN格式
        result = Regex.Replace(result,
            $@"BETWEEN\s+'{Regex.Escape(origStartStr)}'\s+AND\s+'{Regex.Escape(origEndStr)}'",
            $"BETWEEN '{newStartStr}' AND '{newEndStr}'",
            RegexOptions.IgnoreCase);

        _logger.LogDebug("ReplaceDateCondition: 原日期 {OldStart}~{OldEnd} -> 新日期 {NewStart}~{NewEnd}, SQL替换{Changed}",
            origStartStr, origEndStr, newStartStr, newEndStr, result != sql ? "成功" : "失败");

        return result;
    }

    /// <summary>
    /// 计算增长率
    /// </summary>
    private decimal? CalculateRate(decimal? currentValue, decimal? previousValue)
    {
        if (!currentValue.HasValue || !previousValue.HasValue || previousValue.Value == 0)
            return null;

        return Math.Round((currentValue.Value - previousValue.Value) / previousValue.Value * 100, 2);
    }

    /// <summary>
    /// 从KPI查询结果中提取数值
    /// </summary>
    private decimal? ExtractKpiValue(List<Dictionary<string, object?>>? data, string fieldName = "value")
    {
        if (data == null || data.Count == 0) return null;

        var row = data[0];
        if (row.TryGetValue(fieldName, out var value) && value != null)
        {
            if (decimal.TryParse(value.ToString(), out var result))
            {
                return result;
            }
        }
        return null;
    }

    /// <summary>
    /// 为KPI查询添加同比环比数据
    /// </summary>
    private async Task<QueryItem> CalculateKpiYoyMomAsync(
        QueryItem kpi,
        string baseSql,
        string dateField,
        DateTime startDate,
        DateTime endDate,
        string dbType,
        string connString)
    {
        var currentValue = ExtractKpiValue(kpi.Data, kpi.Field ?? "value");

        if (!currentValue.HasValue || string.IsNullOrEmpty(dateField))
        {
            // 没有当前值或日期字段，跳过计算
            return kpi;
        }

        try
        {
            // 计算同比（去年同期）
            var (yoyStart, yoyEnd) = CalculateYoyDateRange(startDate, endDate);
            var yoySql = ReplaceDateCondition(kpi.Sql, dateField, startDate, endDate, yoyStart, yoyEnd);

            if (yoySql != kpi.Sql)  // 确保SQL确实被替换了
            {
                var yoyData = await ExecuteSqlAsync(dbType, connString, yoySql);
                kpi.Yoy = ExtractKpiValue(yoyData, kpi.Field ?? "value");
                kpi.YoyRate = CalculateRate(currentValue, kpi.Yoy);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "计算同比失败: {Title}", kpi.Title);
        }

        try
        {
            // 计算环比（上一周期）
            var (momStart, momEnd) = CalculateMomDateRange(startDate, endDate);
            var momSql = ReplaceDateCondition(kpi.Sql, dateField, startDate, endDate, momStart, momEnd);

            if (momSql != kpi.Sql)  // 确保SQL确实被替换了
            {
                var momData = await ExecuteSqlAsync(dbType, connString, momSql);
                kpi.Mom = ExtractKpiValue(momData, kpi.Field ?? "value");
                kpi.MomRate = CalculateRate(currentValue, kpi.Mom);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "计算环比失败: {Title}", kpi.Title);
        }

        return kpi;
    }

    #endregion
}

/// <summary>
/// 测试RAG检索请求
/// </summary>
public class TestRagRequest
{
    /// <summary>
    /// 查询文本
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// 数据源ID（可选）
    /// </summary>
    public long? DatasourceId { get; set; }

    /// <summary>
    /// 覆盖TopK配置
    /// </summary>
    public int? TopK { get; set; }

    /// <summary>
    /// 覆盖MinScore配置
    /// </summary>
    public float? MinScore { get; set; }
}

/// <summary>
/// 模式分类结果
/// </summary>
public class ModeClassifyResult
{
    /// <summary>
    /// 模式：bi-指标统计, hz360-患者360, internetsearch-通用问答, report-智能报表
    /// </summary>
    public string Mode { get; set; } = "bi";

    /// <summary>
    /// 分类理由
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 患者标识（仅hz360模式）
    /// </summary>
    public string? PatientIdentifier { get; set; }
}

/// <summary>
/// 更新会话标题请求
/// </summary>
public class UpdateSessionTitleRequest
{
    /// <summary>
    /// 新标题
    /// </summary>
    public string Title { get; set; } = string.Empty;
}

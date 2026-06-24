using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bi.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bi.Application.Services;

/// <summary>
/// DeepSeek LLM服务实现
/// 封装DeepSeek V3 API，支持流式输出
/// 优先从数据库配置读取，回退到appsettings.json
/// </summary>
public class DeepSeekLlmService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DeepSeekLlmService> _logger;
    private readonly IConfigService _configService;
    private readonly IConfiguration _configuration;

    // 缓存配置，避免每次请求都查询数据库（按业务类型缓存）
    private readonly Dictionary<AiBusinessType, (string apiKey, string baseUrl, string model, DateTime expiry)> _configCache = new();
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public DeepSeekLlmService(
        HttpClient httpClient,
        IConfiguration config,
        IConfigService configService,
        ILogger<DeepSeekLlmService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configService = configService;
        _configuration = config;
    }

    /// <summary>
    /// 获取配置（支持按业务类型获取，优先数据库，回退appsettings）
    /// </summary>
    /// <param name="businessType">业务类型</param>
    private async Task<(string apiKey, string baseUrl, string model)> GetConfigAsync(AiBusinessType businessType = AiBusinessType.Default)
    {
        // 检查缓存是否有效
        if (_configCache.TryGetValue(businessType, out var cached) && DateTime.UtcNow < cached.expiry)
        {
            return (cached.apiKey, cached.baseUrl, cached.model);
        }

        string provider;
        string model;

        // 判断是否使用业务独立配置
        var useDefault = true;
        if (businessType != AiBusinessType.Default)
        {
            var useDefaultKey = businessType switch
            {
                AiBusinessType.Bi => ConfigKeys.BizBiUseDefault,
                AiBusinessType.Hz360 => ConfigKeys.BizHz360UseDefault,
                AiBusinessType.Search => ConfigKeys.BizSearchUseDefault,
                AiBusinessType.DocGen => ConfigKeys.BizDocGenUseDefault,
                _ => null
            };

            if (useDefaultKey != null)
            {
                useDefault = await _configService.GetAsync<bool>(useDefaultKey, true);
            }
        }

        if (!useDefault && businessType != AiBusinessType.Default)
        {
            // 使用业务独立配置
            var providerKey = businessType switch
            {
                AiBusinessType.Bi => ConfigKeys.BizBiProvider,
                AiBusinessType.Hz360 => ConfigKeys.BizHz360Provider,
                AiBusinessType.Search => ConfigKeys.BizSearchProvider,
                AiBusinessType.DocGen => ConfigKeys.BizDocGenProvider,
                _ => ConfigKeys.LlmProvider
            };
            var modelKey = businessType switch
            {
                AiBusinessType.Bi => ConfigKeys.BizBiModel,
                AiBusinessType.Hz360 => ConfigKeys.BizHz360Model,
                AiBusinessType.Search => ConfigKeys.BizSearchModel,
                AiBusinessType.DocGen => ConfigKeys.BizDocGenModel,
                _ => ConfigKeys.LlmModel
            };

            provider = await _configService.GetAsync(providerKey, "deepseek") ?? "deepseek";
            model = await _configService.GetAsync(modelKey, "deepseek-chat") ?? "deepseek-chat";

            _logger.LogInformation("业务[{BusinessType}]使用独立配置: 服务商={Provider}, 模型={Model}",
                businessType, provider, model);
        }
        else
        {
            // 使用通用配置
            provider = await _configService.GetAsync(ConfigKeys.LlmProvider, "deepseek") ?? "deepseek";
            model = await _configService.GetAsync(ConfigKeys.LlmModel, "deepseek-chat") ?? "deepseek-chat";
        }

        // 根据服务商获取对应的API Key（添加 Kimi/Moonshot）
        var apiKeyConfigKey = provider switch
        {
            "deepseek" => "ai.llm.apiKey.deepseek",
            "qwen" => "ai.llm.apiKey.qwen",
            "zhipu" => "ai.llm.apiKey.zhipu",
            "kimi" => "ai.llm.apiKey.kimi",  // Kimi/Moonshot
            _ => ConfigKeys.LlmApiKey
        };

        var apiKey = await _configService.GetAsync(apiKeyConfigKey);

        // 如果独立Key不存在，尝试获取通用Key
        if (string.IsNullOrEmpty(apiKey))
        {
            apiKey = await _configService.GetAsync(ConfigKeys.LlmApiKey);
        }

        // 根据服务商获取正确的BaseUrl（添加 Kimi/Moonshot）
        var baseUrl = provider switch
        {
            "deepseek" => "https://api.deepseek.com",
            "qwen" => "https://dashscope.aliyuncs.com/compatible-mode/v1",
            "zhipu" => "https://open.bigmodel.cn/api/paas/v4",
            "kimi" => "https://api.moonshot.cn/v1",  // Kimi/Moonshot
            _ => await _configService.GetAsync(ConfigKeys.LlmBaseUrl) ?? "https://api.deepseek.com"
        };

        // 如果数据库配置为空，回退到appsettings
        var section = _configuration.GetSection("DeepSeek");
        apiKey = string.IsNullOrEmpty(apiKey) ? section["ApiKey"] : apiKey;
        if (string.IsNullOrEmpty(baseUrl))
        {
            baseUrl = section["BaseUrl"] ?? "https://api.deepseek.com";
        }
        model = string.IsNullOrEmpty(model) ? (section["Model"] ?? "deepseek-chat") : model;

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException($"未配置{provider}的API Key，请在系统配置中设置");
        }

        // 更新缓存
        _configCache[businessType] = (apiKey, baseUrl, model, DateTime.UtcNow.Add(_cacheDuration));

        return (apiKey, baseUrl, model);
    }
    
    /// <summary>
    /// 发送聊天请求，获取完整响应
    /// 支持deepseek-reasoner模型的思维链输出
    /// </summary>
    public async Task<LlmResponse> ChatAsync(List<LlmMessage> messages, LlmOptions? options = null)
    {
        try
        {
            // 根据业务类型获取配置
            var businessType = options?.BusinessType ?? AiBusinessType.Default;
            var (apiKey, baseUrl, model) = await GetConfigAsync(businessType);

            // 判断是否为reasoner模型
            var isReasonerModel = model.Contains("reasoner", StringComparison.OrdinalIgnoreCase);

            // 构建请求（reasoner模型使用特殊构建）
            var requestBody = isReasonerModel
                ? BuildReasonerRequest(messages, options, model, stream: false)
                : BuildRequest(messages, options, model, stream: false);
            var jsonContent = JsonSerializer.Serialize(requestBody, JsonOptions);

            // ★ 打印关键参数，验证thinking/temperature是否正确
            // 从JSON末尾截取以确保看到thinking参数（messages太长会截断）
            var bodyTail = jsonContent.Length > 200 ? jsonContent[^200..] : jsonContent;
            _logger.LogInformation("LLM请求尾部: {Tail}", bodyTail);
            _logger.LogInformation("调用LLM: 模型={Model}, 是否Reasoner={IsReasoner}, 业务类型={BusinessType}",
                model, isReasonerModel, businessType);

            // 构建API URL（处理baseUrl已包含/v1的情况）
            var apiUrl = baseUrl.TrimEnd('/').EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                ? $"{baseUrl.TrimEnd('/')}/chat/completions"
                : $"{baseUrl.TrimEnd('/')}/v1/chat/completions";

            // 使用HttpRequestMessage避免流复制问题
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(httpRequest);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("LLM API错误: 模型={Model}, 状态={Status}, 响应={Response}", model, response.StatusCode, responseText);
                return new LlmResponse { Success = false, Error = $"API错误: {response.StatusCode} - {responseText}" };
            }

            // ★ 增加详细日志，便于调试不同模型的返回格式
            _logger.LogInformation("LLM原始响应(前500字符): {Preview}", responseText.Length > 500 ? responseText[..500] : responseText);

            var result = JsonSerializer.Deserialize<DeepSeekResponse>(responseText, JsonOptions);
            if (result?.Choices == null || result.Choices.Count == 0)
            {
                _logger.LogWarning("LLM响应无有效Choices: {Response}", responseText.Length > 200 ? responseText[..200] : responseText);
                return new LlmResponse { Success = false, Error = "无有效响应" };
            }

            var message = result.Choices[0].Message;
            var content = message?.Content ?? "";
            var reasoningContent = message?.ReasoningContent;

            // ★ Kimi K2.5 思考模式下，content 可能为空而 reasoning_content 包含全部输出
            // 此时尝试从 reasoning_content 末尾提取 JSON 块作为兜底
            if (string.IsNullOrWhiteSpace(content) && !string.IsNullOrWhiteSpace(reasoningContent))
            {
                _logger.LogWarning("LLM content为空但reasoning_content有内容(长度={Len})，尝试从reasoning_content提取JSON", reasoningContent.Length);
                // 尝试提取最后一个完整的 ```json ... ``` 块
                var jsonBlockMatch = System.Text.RegularExpressions.Regex.Match(
                    reasoningContent,
                    @"```json\s*([\s\S]*?)```",
                    System.Text.RegularExpressions.RegexOptions.RightToLeft);
                if (jsonBlockMatch.Success)
                {
                    content = jsonBlockMatch.Value;
                    _logger.LogInformation("从reasoning_content提取到JSON块(前200字符): {Preview}", content.Length > 200 ? content[..200] : content);
                }
                else
                {
                    // 尝试提取最后一个 { } 块
                    var jsonObjMatch = System.Text.RegularExpressions.Regex.Match(
                        reasoningContent, @"\{[\s\S]*\}",
                        System.Text.RegularExpressions.RegexOptions.RightToLeft);
                    if (jsonObjMatch.Success)
                    {
                        content = jsonObjMatch.Value;
                        _logger.LogInformation("从reasoning_content提取到JSON对象(前200字符): {Preview}", content.Length > 200 ? content[..200] : content);
                    }
                }
            }

            _logger.LogInformation("LLM响应内容(前300字符): {Preview}", content.Length > 300 ? content[..300] : content);

            // 如果是reasoner模型且有思维链输出，记录日志
            if (isReasonerModel && !string.IsNullOrEmpty(reasoningContent))
            {
                _logger.LogDebug("Reasoner思维链输出长度: {Length}", reasoningContent.Length);
            }

            return new LlmResponse
            {
                Content = content,
                TotalTokens = result.Usage?.TotalTokens,
                PromptTokens = result.Usage?.PromptTokens,
                CompletionTokens = result.Usage?.CompletionTokens,
                Model = result.Model,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeepSeek调用失败");
            return new LlmResponse { Success = false, Error = ex.Message };
        }
    }
    
    /// <summary>
    /// 发送聊天请求，流式返回响应
    /// 支持deepseek-reasoner模型（只返回最终content，不返回思维链）
    /// </summary>
    public async IAsyncEnumerable<string> ChatStreamAsync(
        List<LlmMessage> messages,
        LlmOptions? options = null)
    {
        // 根据业务类型获取配置
        var businessType = options?.BusinessType ?? AiBusinessType.Default;
        var (apiKey, baseUrl, model) = await GetConfigAsync(businessType);

        // 判断是否为reasoner模型
        var isReasonerModel = model.Contains("reasoner", StringComparison.OrdinalIgnoreCase);

        var cancellationToken = CancellationToken.None;
        var request = isReasonerModel
            ? BuildReasonerRequest(messages, options, model, stream: true)
            : BuildRequest(messages, options, model, stream: true);
        var jsonContent = JsonSerializer.Serialize(request, JsonOptions);

        _logger.LogInformation("流式LLM请求体(前300): {Body}", jsonContent.Length > 300 ? jsonContent[..300] : jsonContent);
        _logger.LogInformation("流式调用LLM: 模型={Model}, 是否Reasoner={IsReasoner}", model, isReasonerModel);

        // 构建API URL（处理baseUrl已包含/v1的情况）
        var apiUrl = baseUrl.TrimEnd('/').EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? $"{baseUrl.TrimEnd('/')}/chat/completions"
            : $"{baseUrl.TrimEnd('/')}/v1/chat/completions";

        // 使用HttpRequestMessage避免流复制问题
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("DeepSeek流式API错误: {Error}", error);
            yield return $"[ERROR] {response.StatusCode}: {error}";
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        // 对于Reasoner模型，先输出思维链，再输出最终内容
        // 但我们只返回最终的content，思维链内容在reasoning_content中
        var isInReasoningPhase = isReasonerModel;  // Reasoner模型先输出reasoning_content

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line[6..]; // 去掉 "data: " 前缀
            if (data == "[DONE]") break;

            var chunk = JsonSerializer.Deserialize<DeepSeekStreamChunk>(data, JsonOptions);
            var delta = chunk?.Choices?.FirstOrDefault()?.Delta;

            if (delta == null) continue;

            // 对于Reasoner模型，优先检查content（最终回答）
            // reasoning_content是思维链，我们不输出到用户界面
            if (!string.IsNullOrEmpty(delta.Content))
            {
                yield return delta.Content;
            }
            // 如果需要显示思维过程，可以取消下面的注释
            // else if (!string.IsNullOrEmpty(delta.ReasoningContent))
            // {
            //     yield return $"[思考] {delta.ReasoningContent}";
            // }
        }
    }

    /// <summary>
    /// 构建请求对象（普通模型）
    /// </summary>
    private object BuildRequest(List<LlmMessage> messages, LlmOptions? options, string defaultModel, bool stream)
    {
        var model = options?.Model ?? defaultModel;

        // ★ Kimi K2.5 虚拟模型名处理
        // kimi-k2.5-instant → kimi-k2.5 + thinking:disabled
        // Moonshot官方API参数格式：thinking: {"type":"enabled"} 或 {"type":"disabled"}
        var isKimiInstant = model.Equals("kimi-k2.5-instant", StringComparison.OrdinalIgnoreCase);
        var isKimi25 = model.Equals("kimi-k2.5", StringComparison.OrdinalIgnoreCase) || isKimiInstant;

        // ★ Qwen3.5 Plus 虚拟模型名处理
        // qwen3.5-plus-instant → qwen3.5-plus + enable_thinking:false
        // 阿里云API参数：enable_thinking: true/false（布尔值）
        var isQwenInstant = model.Equals("qwen3.5-plus-instant", StringComparison.OrdinalIgnoreCase);
        var isQwen35Plus = model.Equals("qwen3.5-plus", StringComparison.OrdinalIgnoreCase) || isQwenInstant;

        var actualModel = isKimiInstant ? "kimi-k2.5"
            : isQwenInstant ? "qwen3.5-plus"
            : model;

        // ★ temperature 处理
        //   Kimi: thinking enabled→1.0, disabled→0.6
        //   Qwen3.5 Plus: 无特殊限制，使用用户配置
        //   其他: 使用用户配置或默认0.7
        var isKimiModel = actualModel.StartsWith("kimi-", StringComparison.OrdinalIgnoreCase);
        var temperature = isKimiModel
            ? (isKimiInstant ? 0.6 : 1.0)
            : (options?.Temperature ?? 0.7);

        // ★ Kimi K2.5：thinking 对象参数
        if (isKimi25)
        {
            var thinkingType = isKimiInstant ? "disabled" : "enabled";
            return new
            {
                model = actualModel,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }),
                temperature = temperature,
                max_tokens = 32768,
                stream = stream,
                stop = options?.StopSequences,
                thinking = new { type = thinkingType }
            };
        }

        // ★ Qwen3.5 Plus：enable_thinking 布尔参数
        // 默认开启思考模式，instant 模式关闭
        if (isQwen35Plus)
        {
            var enableThinking = !isQwenInstant;
            return new
            {
                model = actualModel,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }),
                temperature = temperature,
                max_tokens = enableThinking ? 32768 : (options?.MaxTokens ?? 4096),
                stream = stream,
                stop = options?.StopSequences,
                enable_thinking = enableThinking
            };
        }

        return new
        {
            model = actualModel,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            temperature = temperature,
            max_tokens = options?.MaxTokens ?? 4096,
            stream = stream,
            stop = options?.StopSequences
        };
    }

    /// <summary>
    /// 构建Reasoner模型请求对象
    /// deepseek-reasoner不支持temperature、top_p等参数，只支持max_tokens
    /// </summary>
    private object BuildReasonerRequest(List<LlmMessage> messages, LlmOptions? options, string defaultModel, bool stream)
    {
        return new
        {
            model = options?.Model ?? defaultModel,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            max_tokens = options?.MaxTokens ?? 32768,  // Reasoner默认32K，最大64K
            stream = stream
            // 注意：Reasoner模型不支持temperature、top_p、presence_penalty、frequency_penalty
        };
    }

    /// <summary>
    /// 发送带工具调用的聊天请求（Function Calling）
    /// </summary>
    public async Task<LlmToolResponse> ChatWithToolsAsync(List<LlmMessage> messages, List<LlmTool> tools, LlmOptions? options = null)
    {
        try
        {
            var businessType = options?.BusinessType ?? AiBusinessType.Default;
            var (apiKey, baseUrl, model) = await GetConfigAsync(businessType);

            // 构建带工具的请求
            var requestBody = BuildToolsRequest(messages, tools, options, model);
            var jsonContent = JsonSerializer.Serialize(requestBody, JsonOptions);

            _logger.LogInformation("调用 LLM Function Calling: 模型={Model}, 工具数={ToolCount}, 业务类型={BusinessType}",
                model, tools.Count, businessType);

            var apiUrl = baseUrl.TrimEnd('/').EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                ? $"{baseUrl.TrimEnd('/')}/chat/completions"
                : $"{baseUrl.TrimEnd('/')}/v1/chat/completions";

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(httpRequest);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("LLM API 错误: {Status} - {Response}", response.StatusCode, responseText);
                return new LlmToolResponse { Success = false, Error = $"API错误: {response.StatusCode} - {responseText}" };
            }

            var result = JsonSerializer.Deserialize<DeepSeekToolResponse>(responseText, JsonOptions);
            if (result?.Choices == null || result.Choices.Count == 0)
            {
                return new LlmToolResponse { Success = false, Error = "无有效响应" };
            }

            var choice = result.Choices[0];
            var toolResponse = new LlmToolResponse
            {
                Success = true,
                Content = choice.Message?.Content,
                FinishReason = choice.FinishReason,
                Model = result.Model,
                TotalTokens = result.Usage?.TotalTokens
            };

            // 解析工具调用
            if (choice.Message?.ToolCalls != null && choice.Message.ToolCalls.Count > 0)
            {
                toolResponse.ToolCalls = choice.Message.ToolCalls.Select(tc => new LlmToolCall
                {
                    Id = tc.Id ?? "",
                    Type = tc.Type ?? "function",
                    Function = new LlmFunctionCall
                    {
                        Name = tc.Function?.Name ?? "",
                        Arguments = tc.Function?.Arguments ?? "{}"
                    }
                }).ToList();
            }

            return toolResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM Function Calling 调用失败");
            return new LlmToolResponse { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// 构建带工具的请求对象
    /// </summary>
    private object BuildToolsRequest(List<LlmMessage> messages, List<LlmTool> tools, LlmOptions? options, string model)
    {
        var rawModel = options?.Model ?? model;

        // ★ Kimi K2.5 虚拟模型名处理
        var isKimiInstant = rawModel.Equals("kimi-k2.5-instant", StringComparison.OrdinalIgnoreCase);
        var isKimi25 = rawModel.Equals("kimi-k2.5", StringComparison.OrdinalIgnoreCase) || isKimiInstant;

        // ★ Qwen3.5 Plus 虚拟模型名处理
        var isQwenInstant = rawModel.Equals("qwen3.5-plus-instant", StringComparison.OrdinalIgnoreCase);
        var isQwen35Plus = rawModel.Equals("qwen3.5-plus", StringComparison.OrdinalIgnoreCase) || isQwenInstant;

        var actualModel = isKimiInstant ? "kimi-k2.5"
            : isQwenInstant ? "qwen3.5-plus"
            : rawModel;

        // ★ temperature 处理
        var isKimiModel = actualModel.StartsWith("kimi-", StringComparison.OrdinalIgnoreCase);
        var temperature = isKimiModel
            ? (isKimiInstant ? 0.6 : 1.0)
            : (options?.Temperature ?? 0.7);

        var toolList = tools.Select(t => new
        {
            type = t.Type,
            function = new
            {
                name = t.Function.Name,
                description = t.Function.Description,
                parameters = t.Function.Parameters
            }
        });

        // ★ Kimi K2.5：thinking 对象参数
        if (isKimi25)
        {
            var thinkingType = isKimiInstant ? "disabled" : "enabled";
            return new
            {
                model = actualModel,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }),
                tools = toolList,
                temperature = temperature,
                max_tokens = 32768,
                thinking = new { type = thinkingType }
            };
        }

        // ★ Qwen3.5 Plus：enable_thinking 布尔参数
        if (isQwen35Plus)
        {
            var enableThinking = !isQwenInstant;
            return new
            {
                model = actualModel,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }),
                tools = toolList,
                temperature = temperature,
                max_tokens = enableThinking ? 32768 : (options?.MaxTokens ?? 4096),
                enable_thinking = enableThinking
            };
        }

        return new
        {
            model = actualModel,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            tools = toolList,
            temperature = temperature,
            max_tokens = options?.MaxTokens ?? 4096
        };
    }
}

#region DeepSeek响应模型

/// <summary>
/// DeepSeek API响应
/// </summary>
internal class DeepSeekResponse
{
    public string? Id { get; set; }
    public string? Object { get; set; }
    public string? Model { get; set; }
    public List<DeepSeekChoice>? Choices { get; set; }
    public DeepSeekUsage? Usage { get; set; }
}

internal class DeepSeekChoice
{
    public int Index { get; set; }
    public DeepSeekMessage? Message { get; set; }
    public string? FinishReason { get; set; }
}

internal class DeepSeekMessage
{
    public string? Role { get; set; }
    public string? Content { get; set; }
    /// <summary>
    /// Reasoner模型的思维链内容（deepseek-reasoner专用）
    /// </summary>
    public string? ReasoningContent { get; set; }
}

internal class DeepSeekUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

/// <summary>
/// DeepSeek流式响应块
/// </summary>
internal class DeepSeekStreamChunk
{
    public string? Id { get; set; }
    public List<DeepSeekStreamChoice>? Choices { get; set; }
}

internal class DeepSeekStreamChoice
{
    public int Index { get; set; }
    public DeepSeekDelta? Delta { get; set; }
    public string? FinishReason { get; set; }
}

internal class DeepSeekDelta
{
    public string? Role { get; set; }
    public string? Content { get; set; }
    /// <summary>
    /// Reasoner模型的思维链内容（deepseek-reasoner专用，流式输出时）
    /// </summary>
    public string? ReasoningContent { get; set; }
}

/// <summary>
/// 带工具调用的响应（Function Calling）
/// </summary>
internal class DeepSeekToolResponse
{
    public string? Id { get; set; }
    public string? Object { get; set; }
    public string? Model { get; set; }
    public List<DeepSeekToolChoice>? Choices { get; set; }
    public DeepSeekUsage? Usage { get; set; }
}

internal class DeepSeekToolChoice
{
    public int Index { get; set; }
    public DeepSeekToolMessage? Message { get; set; }
    public string? FinishReason { get; set; }
}

internal class DeepSeekToolMessage
{
    public string? Role { get; set; }
    public string? Content { get; set; }
    public List<DeepSeekToolCallItem>? ToolCalls { get; set; }
}

internal class DeepSeekToolCallItem
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public DeepSeekFunctionCall? Function { get; set; }
}

internal class DeepSeekFunctionCall
{
    public string? Name { get; set; }
    public string? Arguments { get; set; }
}

#endregion


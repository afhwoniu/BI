using Bi.Api.Models;
using Bi.Application.Services;
using Bi.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bi.Api.Controllers;

/// <summary>
/// 系统配置管理控制器
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ConfigController : ControllerBase
{
    private readonly IConfigService _configService;
    private readonly IAsrService _asrService;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(IConfigService configService, IAsrService asrService, ILogger<ConfigController> logger)
    {
        _configService = configService;
        _asrService = asrService;
        _logger = logger;
    }
    
    /// <summary>
    /// 获取所有配置分组
    /// </summary>
    [HttpGet("groups")]
    public ActionResult<ApiResponse<List<object>>> GetGroups()
    {
        var groups = new List<object>
        {
            new { Key = ConfigGroups.Basic, Name = "基础设置", Icon = "Setting" },
            new { Key = ConfigGroups.Ai, Name = "AI服务配置", Icon = "MagicStick" },
            new { Key = ConfigGroups.Security, Name = "安全配置", Icon = "Lock" },
            new { Key = ConfigGroups.Cache, Name = "缓存配置", Icon = "Cpu" },
            new { Key = ConfigGroups.Data, Name = "数据配置", Icon = "DataAnalysis" }
        };
        return Ok(ApiResponse<List<object>>.Success(groups));
    }
    
    /// <summary>
    /// 获取指定分组的配置列表
    /// </summary>
    [HttpGet("group/{group}")]
    public async Task<ActionResult<ApiResponse<List<ConfigDto>>>> GetByGroup(string group)
    {
        try
        {
            var configs = await _configService.GetByGroupAsync(group);

            // 转换为DTO，直接返回真实值（已在Service层解密）
            var dtos = configs.Select(c => new ConfigDto
            {
                Id = c.Id,
                ConfigKey = c.ConfigKey,
                ConfigValue = c.ConfigValue,  // 直接返回真实值
                ConfigGroup = c.ConfigGroup,
                ConfigType = c.ConfigType,
                IsEncrypted = c.IsEncrypted,
                DisplayName = c.DisplayName,
                Remark = c.Remark,
                SortOrder = c.SortOrder
            }).ToList();
            
            return Ok(ApiResponse<List<ConfigDto>>.Success(dtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取配置分组失败: {Group}", group);
            return Ok(ApiResponse<List<ConfigDto>>.Fail($"获取配置失败: {ex.Message}"));
        }
    }
    
    /// <summary>
    /// 获取单个配置值
    /// </summary>
    [HttpGet("{key}")]
    public async Task<ActionResult<ApiResponse<string>>> Get(string key)
    {
        var value = await _configService.GetAsync(key);
        return Ok(ApiResponse<string>.Success(value));
    }
    
    /// <summary>
    /// 批量保存配置
    /// </summary>
    [HttpPut("batch")]
    public async Task<ActionResult<ApiResponse<bool>>> SaveBatch([FromBody] List<ConfigDto> configs)
    {
        try
        {
            // 转换为实体
            var entities = configs.Select(c => new SysConfig
            {
                Id = c.Id,
                ConfigKey = c.ConfigKey,
                ConfigValue = c.ConfigValue,
                ConfigGroup = c.ConfigGroup,
                ConfigType = c.ConfigType,
                IsEncrypted = c.IsEncrypted,
                DisplayName = c.DisplayName,
                Remark = c.Remark,
                SortOrder = c.SortOrder
            }).ToList();
            
            await _configService.SaveBatchAsync(entities);

            // 清除ASR服务的配置缓存，确保立即生效
            _asrService.ClearCache();

            _logger.LogInformation("批量保存配置成功，共 {Count} 项", configs.Count);
            return Ok(ApiResponse<bool>.Success(true, "保存成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量保存配置失败");
            return Ok(ApiResponse<bool>.Fail($"保存失败: {ex.Message}"));
        }
    }
    
    /// <summary>
    /// 刷新配置缓存
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<bool>>> RefreshCache()
    {
        try
        {
            await _configService.RefreshCacheAsync();
            return Ok(ApiResponse<bool>.Success(true, "缓存已刷新"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新配置缓存失败");
            return Ok(ApiResponse<bool>.Fail($"刷新失败: {ex.Message}"));
        }
    }
    
    /// <summary>
    /// 获取开发说明
    /// </summary>
    [HttpGet("dev-notes")]
    public async Task<ActionResult<ApiResponse<string>>> GetDevNotes()
    {
        try
        {
            var notes = await _configService.GetAsync(ConfigKeys.DevNotes, "");
            return Ok(ApiResponse<string>.Success(notes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取开发说明失败");
            return Ok(ApiResponse<string>.Fail($"获取失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 保存开发说明
    /// </summary>
    [HttpPut("dev-notes")]
    public async Task<ActionResult<ApiResponse<bool>>> SaveDevNotes([FromBody] DevNotesRequest request)
    {
        try
        {
            await _configService.SetAsync(ConfigKeys.DevNotes, request.Content);
            _logger.LogInformation("开发说明已更新");
            return Ok(ApiResponse<bool>.Success(true, "保存成功"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存开发说明失败");
            return Ok(ApiResponse<bool>.Fail($"保存失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 测试AI服务连接（旧接口，保留兼容）
    /// </summary>
    [HttpPost("test-ai")]
    public async Task<ActionResult<ApiResponse<object>>> TestAiConnection()
    {
        try
        {
            var provider = await _configService.GetAsync(ConfigKeys.LlmProvider, "deepseek");
            var baseUrl = await _configService.GetAsync(ConfigKeys.LlmBaseUrl, "");
            var apiKey = await _configService.GetAsync(ConfigKeys.LlmApiKey, "");

            if (string.IsNullOrEmpty(apiKey))
            {
                return Ok(ApiResponse<object>.Fail("API Key未配置"));
            }

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var testUrl = $"{baseUrl}/v1/models";
            var response = await http.GetAsync(testUrl);
            if (response.IsSuccessStatusCode)
            {
                return Ok(ApiResponse<object>.Success(new { Provider = provider, Status = "连接成功" }));
            }
            else
            {
                return Ok(ApiResponse<object>.Fail($"连接失败: {response.StatusCode}"));
            }
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<object>.Fail($"测试失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 测试LLM连接（新接口，支持自定义参数）
    /// </summary>
    [HttpPost("test-llm")]
    public async Task<ActionResult<ApiResponse<LlmTestResult>>> TestLlm([FromBody] LlmTestRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ApiKey))
                return Ok(ApiResponse<LlmTestResult>.Fail("请输入API Key"));

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {request.ApiKey}");

            // ★ 虚拟模型名转换
            var isKimiInstant = request.Model.Equals("kimi-k2.5-instant", StringComparison.OrdinalIgnoreCase);
            var isKimi25 = request.Model.Equals("kimi-k2.5", StringComparison.OrdinalIgnoreCase) || isKimiInstant;
            var isQwenInstant = request.Model.Equals("qwen3.5-plus-instant", StringComparison.OrdinalIgnoreCase);
            var isQwen35Plus = request.Model.Equals("qwen3.5-plus", StringComparison.OrdinalIgnoreCase) || isQwenInstant;
            var actualModel = isKimiInstant ? "kimi-k2.5"
                : isQwenInstant ? "qwen3.5-plus"
                : request.Model;

            // ★ temperature 处理
            var isKimiModel = request.Provider == "kimi" || actualModel.StartsWith("kimi-");
            double temperature;
            if (isKimiInstant) temperature = 0.6;
            else if (isKimiModel) temperature = 1.0;
            else temperature = 0.7;

            // 构建测试请求
            var chatUrl = $"{request.BaseUrl.TrimEnd('/')}/chat/completions";
            object testMessage;
            if (isKimi25)
            {
                // Kimi K2.5：thinking 对象参数
                var thinkingType = isKimiInstant ? "disabled" : "enabled";
                testMessage = new
                {
                    model = actualModel,
                    messages = new[] { new { role = "user", content = "你好，请用一句话介绍自己。" } },
                    max_tokens = 200,
                    temperature = temperature,
                    thinking = new { type = thinkingType }
                };
            }
            else if (isQwen35Plus)
            {
                // Qwen3.5 Plus：enable_thinking 布尔参数
                testMessage = new
                {
                    model = actualModel,
                    messages = new[] { new { role = "user", content = "你好，请用一句话介绍自己。" } },
                    max_tokens = 200,
                    temperature = temperature,
                    enable_thinking = !isQwenInstant
                };
            }
            else
            {
                testMessage = new
                {
                    model = actualModel,
                    messages = new[] { new { role = "user", content = "你好，请用一句话介绍自己。" } },
                    max_tokens = 200,
                    temperature = temperature
                };
            }

            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(testMessage),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await http.PostAsync(chatUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // 解析响应获取AI回复
                var aiResponse = "";
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(responseText);
                    aiResponse = doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString() ?? "";
                }
                catch { aiResponse = "解析响应失败"; }

                return Ok(ApiResponse<LlmTestResult>.Success(new LlmTestResult
                {
                    Success = true,
                    Message = $"服务商 {request.Provider} 模型 {request.Model}（实际: {actualModel}）连接成功",
                    Response = aiResponse.Length > 200 ? aiResponse[..200] + "..." : aiResponse
                }));
            }
            else
            {
                return Ok(ApiResponse<LlmTestResult>.Success(new LlmTestResult
                {
                    Success = false,
                    Message = $"连接失败: HTTP {(int)response.StatusCode}",
                    Response = responseText.Length > 500 ? responseText[..500] : responseText
                }));
            }
        }
        catch (TaskCanceledException)
        {
            return Ok(ApiResponse<LlmTestResult>.Success(new LlmTestResult
            {
                Success = false,
                Message = "请求超时，请检查网络连接或API地址"
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<LlmTestResult>.Fail($"测试失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 测试Embedding连接
    /// </summary>
    [HttpPost("test-embedding")]
    public async Task<ActionResult<ApiResponse<EmbeddingTestResult>>> TestEmbedding([FromBody] LlmTestRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ApiKey))
                return Ok(ApiResponse<EmbeddingTestResult>.Fail("请输入API Key"));

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {request.ApiKey}");

            // 构建测试请求
            var embedUrl = $"{request.BaseUrl.TrimEnd('/')}/embeddings";
            var testMessage = new
            {
                model = request.Model,
                input = "测试文本"
            };

            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(testMessage),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await http.PostAsync(embedUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // 解析响应获取向量维度
                var dimensions = 0;
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(responseText);
                    var embedding = doc.RootElement
                        .GetProperty("data")[0]
                        .GetProperty("embedding");
                    dimensions = embedding.GetArrayLength();
                }
                catch { dimensions = 0; }

                return Ok(ApiResponse<EmbeddingTestResult>.Success(new EmbeddingTestResult
                {
                    Success = true,
                    Message = $"服务商 {request.Provider} 模型 {request.Model} 连接成功",
                    Dimensions = dimensions
                }));
            }
            else
            {
                return Ok(ApiResponse<EmbeddingTestResult>.Success(new EmbeddingTestResult
                {
                    Success = false,
                    Message = $"连接失败: HTTP {(int)response.StatusCode}"
                }));
            }
        }
        catch (TaskCanceledException)
        {
            return Ok(ApiResponse<EmbeddingTestResult>.Success(new EmbeddingTestResult
            {
                Success = false,
                Message = "请求超时，请检查网络连接或API地址"
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<EmbeddingTestResult>.Fail($"测试失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 测试ASR连接
    /// </summary>
    [HttpPost("test-asr")]
    public async Task<ActionResult<ApiResponse<AsrTestResult>>> TestAsr([FromBody] AsrTestRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ApiKey))
                return Ok(ApiResponse<AsrTestResult>.Fail("请输入API Key"));

            // 使用智谱AI的API验证Token有效性
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

            // 生成JWT Token（智谱AI格式）
            var token = GenerateZhipuJwtToken(request.ApiKey);
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            // 使用一个简单的音频转写测试（实际上我们只验证Token有效性）
            // 智谱ASR没有简单的验证接口，我们尝试调用模型列表接口
            var testUrl = "https://open.bigmodel.cn/api/paas/v4/models";

            var response = await http.GetAsync(testUrl);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return Ok(ApiResponse<AsrTestResult>.Success(new AsrTestResult
                {
                    Success = true,
                    Message = $"ASR服务商 {request.Provider} 模型 {request.Model} 连接成功"
                }));
            }
            else
            {
                return Ok(ApiResponse<AsrTestResult>.Success(new AsrTestResult
                {
                    Success = false,
                    Message = $"连接失败: HTTP {(int)response.StatusCode}，请检查API Key是否正确"
                }));
            }
        }
        catch (TaskCanceledException)
        {
            return Ok(ApiResponse<AsrTestResult>.Success(new AsrTestResult
            {
                Success = false,
                Message = "请求超时，请检查网络连接"
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<AsrTestResult>.Fail($"测试失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 生成智谱AI JWT Token
    /// </summary>
    private string GenerateZhipuJwtToken(string apiKey)
    {
        var parts = apiKey.Split('.');
        if (parts.Length != 2)
            throw new ArgumentException("API Key格式不正确，应为 id.secret 格式");

        var id = parts[0];
        var secret = parts[1];

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var exp = now + 3600 * 1000; // 1小时过期

        var header = System.Text.Json.JsonSerializer.Serialize(new { alg = "HS256", sign_type = "SIGN" });
        var payload = System.Text.Json.JsonSerializer.Serialize(new { api_key = id, exp, timestamp = now });

        var headerBase64 = Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(header));
        var payloadBase64 = Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(payload));

        var signatureInput = $"{headerBase64}.{payloadBase64}";
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var signatureBytes = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(signatureInput));
        var signature = Base64UrlEncode(signatureBytes);

        return $"{headerBase64}.{payloadBase64}.{signature}";
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

// 请求模型
public class LlmTestRequest
{
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
}

// LLM测试结果
public class LlmTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? Response { get; set; }
}

// Embedding测试结果
public class EmbeddingTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int? Dimensions { get; set; }
}

// ASR测试请求
public class AsrTestRequest
{
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string ApiKey { get; set; } = "";
}

// ASR测试结果
public class AsrTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}

// 开发说明请求
public class DevNotesRequest
{
    /// <summary>
    /// 开发说明内容（Markdown格式）
    /// </summary>
    public string Content { get; set; } = "";
}


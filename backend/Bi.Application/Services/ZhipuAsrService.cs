using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bi.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Bi.Application.Services;

/// <summary>
/// 智谱AI ASR语音识别服务
/// 支持GLM-4-Voice语音识别模型
/// </summary>
public class ZhipuAsrService : IAsrService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ZhipuAsrService> _logger;
    private readonly IConfigService _configService;
    private readonly IConfiguration _configuration;

    // 配置缓存
    private AsrConfig? _cachedConfig;
    private string? _cachedApiKey;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ZhipuAsrService(
        HttpClient httpClient,
        IConfiguration configuration,
        IConfigService configService,
        ILogger<ZhipuAsrService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// 检查ASR是否启用
    /// </summary>
    public async Task<bool> IsEnabledAsync()
    {
        var config = await GetConfigAsync();
        return config.Enabled;
    }

    /// <summary>
    /// 获取ASR配置
    /// </summary>
    public async Task<AsrConfig> GetConfigAsync()
    {
        if (DateTime.UtcNow < _cacheExpiry && _cachedConfig != null)
        {
            return _cachedConfig;
        }

        var enabled = await _configService.GetAsync<bool>(ConfigKeys.AsrEnabled, false);
        var provider = await _configService.GetAsync(ConfigKeys.AsrProvider, "zhipu");
        var model = await _configService.GetAsync(ConfigKeys.AsrModel, "glm-4-voice");
        var streamEnabled = await _configService.GetAsync<bool>(ConfigKeys.AsrStreamEnabled, true);
        var language = await _configService.GetAsync(ConfigKeys.AsrLanguage, "zh");

        // ★ 读取语音唤醒配置
        var wakeupEnabled = await _configService.GetAsync<bool>(ConfigKeys.VoiceWakeupEnabled, false);
        var wakeupWordsJson = await _configService.GetAsync(ConfigKeys.VoiceWakeupWords, "[\"你好助手\",\"嘿助手\",\"小助手\"]");
        var commandWordsJson = await _configService.GetAsync(ConfigKeys.VoiceCommandWords, "[\"执行\",\"发送\",\"查询\",\"开始\",\"分析\"]");

        var wakeupWords = new List<string> { "你好助手", "嘿助手", "小助手" };
        var commandWords = new List<string> { "执行", "发送", "查询", "开始", "分析" };
        try
        {
            if (!string.IsNullOrEmpty(wakeupWordsJson))
                wakeupWords = System.Text.Json.JsonSerializer.Deserialize<List<string>>(wakeupWordsJson) ?? wakeupWords;
            if (!string.IsNullOrEmpty(commandWordsJson))
                commandWords = System.Text.Json.JsonSerializer.Deserialize<List<string>>(commandWordsJson) ?? commandWords;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析语音唤醒词配置失败，使用默认值");
        }

        _cachedConfig = new AsrConfig
        {
            Enabled = enabled,
            Provider = provider ?? "zhipu",
            Model = model ?? "glm-4-voice",
            StreamEnabled = streamEnabled,
            Language = language ?? "zh",
            WebSocketUrl = "wss://open.bigmodel.cn/api/paas/v4/audio/transcriptions",
            // ★ 语音唤醒配置
            WakeupEnabled = wakeupEnabled,
            WakeupWords = wakeupWords,
            CommandWords = commandWords
        };

        _cacheExpiry = DateTime.UtcNow.Add(_cacheDuration);
        return _cachedConfig;
    }

    /// <summary>
    /// 获取API Key（优先独立配置，回退到智谱LLM Key）
    /// </summary>
    private async Task<string> GetApiKeyAsync()
    {
        if (DateTime.UtcNow < _cacheExpiry && _cachedApiKey != null)
        {
            return _cachedApiKey;
        }

        // 先尝试获取ASR专用Key
        var apiKey = await _configService.GetAsync(ConfigKeys.AsrApiKey);

        // 回退到智谱LLM Key
        if (string.IsNullOrEmpty(apiKey))
        {
            apiKey = await _configService.GetAsync("ai.llm.apiKey.zhipu");
        }

        // 再回退到通用LLM Key（如果当前服务商是智谱）
        if (string.IsNullOrEmpty(apiKey))
        {
            var provider = await _configService.GetAsync(ConfigKeys.LlmProvider);
            if (provider == "zhipu")
            {
                apiKey = await _configService.GetAsync(ConfigKeys.LlmApiKey);
            }
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("未配置ASR API Key，请在系统配置中设置智谱AI的API Key");
        }

        _cachedApiKey = apiKey;
        return apiKey;
    }

    /// <summary>
    /// 生成智谱API的JWT Token
    /// 智谱要求的格式: header.payload.signature (手动构建，不依赖JwtSecurityToken)
    /// </summary>
    private string GenerateJwtToken(string apiKey)
    {
        var parts = apiKey.Split('.');
        if (parts.Length != 2)
        {
            throw new ArgumentException("无效的智谱API Key格式，应为 'id.secret' 格式");
        }

        var keyId = parts[0];
        var secret = parts[1];

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var expiry = now + 3600 * 1000; // 1小时后过期

        // 智谱要求的header格式
        var header = new { alg = "HS256", sign_type = "SIGN" };
        // 智谱要求的payload格式
        var payload = new
        {
            api_key = keyId,
            exp = expiry,
            timestamp = now
        };

        // Base64Url编码
        var headerJson = JsonSerializer.Serialize(header);
        var payloadJson = JsonSerializer.Serialize(payload);

        var headerBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payloadBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

        // 签名: HMAC-SHA256(header.payload, secret)
        var message = $"{headerBase64}.{payloadBase64}";
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        var signatureBase64 = Base64UrlEncode(signatureBytes);

        return $"{headerBase64}.{payloadBase64}.{signatureBase64}";
    }

    /// <summary>
    /// Base64Url编码 (JWT标准格式)
    /// </summary>
    private static string Base64UrlEncode(byte[] input)
    {
        var base64 = Convert.ToBase64String(input);
        // 替换+为-, /为_, 去掉末尾的=
        return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>
    /// 上传音频文件进行转写
    /// 智谱ASR API: POST https://open.bigmodel.cn/api/paas/v4/audio/transcriptions
    /// </summary>
    public async Task<AsrResult> TranscribeAsync(byte[] audioData, string format = "wav")
    {
        try
        {
            var apiKey = await GetApiKeyAsync();
            var config = await GetConfigAsync();
            var token = GenerateJwtToken(apiKey);

            var requestUrl = "https://open.bigmodel.cn/api/paas/v4/audio/transcriptions";

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // 构建multipart表单
            var content = new MultipartFormDataContent();

            // 确定文件扩展名和MIME类型
            var (extension, mimeType) = format.ToLower() switch
            {
                "webm" => ("webm", "audio/webm"),
                "mp3" => ("mp3", "audio/mpeg"),
                "ogg" => ("ogg", "audio/ogg"),
                "flac" => ("flac", "audio/flac"),
                "m4a" => ("m4a", "audio/m4a"),
                _ => ("wav", "audio/wav")
            };

            var fileContent = new ByteArrayContent(audioData);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            content.Add(fileContent, "file", $"audio.{extension}");

            // 添加模型参数
            content.Add(new StringContent(config.Model), "model");

            // 语言参数（可选）
            if (!string.IsNullOrEmpty(config.Language))
            {
                content.Add(new StringContent(config.Language), "language");
            }

            request.Content = content;

            _logger.LogInformation("开始调用智谱ASR，模型: {Model}, 音频大小: {Size}KB",
                config.Model, audioData.Length / 1024);

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("智谱ASR调用失败: {Status} - {Body}", response.StatusCode, responseBody);
                return new AsrResult
                {
                    Success = false,
                    Error = $"ASR调用失败: {response.StatusCode} - {responseBody}"
                };
            }

            // 解析响应
            var result = JsonSerializer.Deserialize<ZhipuAsrResponse>(responseBody, JsonOptions);

            if (result == null)
            {
                return new AsrResult
                {
                    Success = false,
                    Error = "无法解析ASR响应"
                };
            }

            _logger.LogInformation("智谱ASR转写完成: {Text}", result.Text?.Length > 50
                ? result.Text[..50] + "..." : result.Text);

            return new AsrResult
            {
                Success = true,
                Text = result.Text ?? "",
                Duration = result.Duration
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ASR转写异常");
            return new AsrResult
            {
                Success = false,
                Error = $"ASR转写异常: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 清除配置缓存
    /// </summary>
    public void ClearCache()
    {
        _cachedConfig = null;
        _cachedApiKey = null;
        _cacheExpiry = DateTime.MinValue;
    }
}

/// <summary>
/// 智谱ASR响应结构
/// </summary>
internal class ZhipuAsrResponse
{
    /// <summary>
    /// 转写文本
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>
    /// 音频时长（秒）
    /// </summary>
    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    /// <summary>
    /// 任务ID
    /// </summary>
    [JsonPropertyName("task_id")]
    public string? TaskId { get; set; }
}

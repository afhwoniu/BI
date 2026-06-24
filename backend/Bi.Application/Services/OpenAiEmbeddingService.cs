using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bi.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bi.Application.Services;

/// <summary>
/// OpenAI Embedding服务实现
/// 优先从数据库配置读取，回退到appsettings.json
/// </summary>
public class OpenAiEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiEmbeddingService> _logger;
    private readonly IConfigService _configService;
    private readonly IConfiguration _configuration;

    // 缓存配置
    private string? _cachedApiKey;
    private string? _cachedBaseUrl;
    private string? _cachedModel;
    private int _cachedDimensions = 1536;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// 向量维度（text-embedding-3-small默认1536维）
    /// </summary>
    public int Dimensions => _cachedDimensions;

    public OpenAiEmbeddingService(
        HttpClient httpClient,
        IConfiguration configuration,
        IConfigService configService,
        ILogger<OpenAiEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configService = configService;
        _configuration = configuration;
    }

    /// <summary>
    /// 获取配置（优先数据库，回退appsettings）
    /// </summary>
    private async Task<(string apiKey, string baseUrl, string model, int dimensions)> GetConfigAsync()
    {
        // 检查缓存是否有效
        if (DateTime.UtcNow < _cacheExpiry && _cachedApiKey != null)
        {
            return (_cachedApiKey, _cachedBaseUrl!, _cachedModel!, _cachedDimensions);
        }

        // 从数据库配置读取
        var apiKey = await _configService.GetAsync(ConfigKeys.EmbeddingApiKey);
        var baseUrl = await _configService.GetAsync(ConfigKeys.EmbeddingBaseUrl);
        var model = await _configService.GetAsync(ConfigKeys.EmbeddingModel);
        var dimensionsStr = await _configService.GetAsync(ConfigKeys.EmbeddingDimension);

        // 如果数据库配置为空，回退到appsettings
        apiKey = string.IsNullOrEmpty(apiKey) ? _configuration["OpenAI:ApiKey"] : apiKey;
        baseUrl = string.IsNullOrEmpty(baseUrl) ? (_configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com") : baseUrl;
        model = string.IsNullOrEmpty(model) ? (_configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small") : model;
        var dimensions = int.TryParse(dimensionsStr, out var d) ? d : 1536;

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("未配置Embedding API Key，请在系统配置中设置");
        }

        // 更新缓存
        _cachedApiKey = apiKey;
        _cachedBaseUrl = baseUrl;
        _cachedModel = model;
        _cachedDimensions = dimensions;
        _cacheExpiry = DateTime.UtcNow.Add(_cacheDuration);

        return (apiKey, baseUrl, model, dimensions);
    }
    
    /// <summary>
    /// 生成单个文本的向量嵌入
    /// </summary>
    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var embeddings = await GetEmbeddingsAsync(new List<string> { text });
        return embeddings.FirstOrDefault() ?? Array.Empty<float>();
    }
    
    /// <summary>
    /// 批量生成文本的向量嵌入
    /// </summary>
    public async Task<List<float[]>> GetEmbeddingsAsync(List<string> texts)
    {
        if (texts == null || texts.Count == 0)
            return new List<float[]>();

        try
        {
            var (apiKey, baseUrl, model, dimensions) = await GetConfigAsync();

            // 设置认证头
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var request = new
            {
                model = model,
                input = texts,
                dimensions = dimensions
            };

            var jsonContent = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{baseUrl}/v1/embeddings", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI Embedding API错误: {StatusCode} - {Content}", response.StatusCode, responseContent);
                throw new Exception($"OpenAI API错误: {response.StatusCode}");
            }

            var result = JsonSerializer.Deserialize<OpenAiEmbeddingResponse>(responseContent, JsonOptions);
            if (result?.Data == null)
            {
                throw new Exception("OpenAI返回数据格式错误");
            }

            // 按index排序后返回
            return result.Data
                .OrderBy(d => d.Index)
                .Select(d => d.Embedding)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成向量嵌入失败");
            throw;
        }
    }
}

#region OpenAI响应模型

internal class OpenAiEmbeddingResponse
{
    public string? Object { get; set; }
    public List<OpenAiEmbeddingData>? Data { get; set; }
    public string? Model { get; set; }
    public OpenAiUsage? Usage { get; set; }
}

internal class OpenAiEmbeddingData
{
    public string? Object { get; set; }
    public int Index { get; set; }
    public float[] Embedding { get; set; } = Array.Empty<float>();
}

internal class OpenAiUsage
{
    public int PromptTokens { get; set; }
    public int TotalTokens { get; set; }
}

#endregion


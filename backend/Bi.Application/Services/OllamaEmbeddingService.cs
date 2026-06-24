using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bi.Application.Services;

/// <summary>
/// Ollama本地Embedding服务实现
/// 调用本地部署的Ollama服务生成向量
/// </summary>
public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaEmbeddingService> _logger;
    private readonly IConfigService _configService;
    private readonly IConfiguration _configuration;

    // 默认配置
    private const string DefaultBaseUrl = "http://localhost:11434";
    private const string DefaultModel = "bge-m3";
    private const int DefaultDimensions = 1024;
    // BGE-M3 上下文长度约8192 tokens，中文约1.5字符/token，保守估计用4000字符
    private const int MaxTextLength = 4000;

    // 配置缓存
    private string? _cachedBaseUrl;
    private string? _cachedModel;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public OllamaEmbeddingService(
        HttpClient httpClient,
        ILogger<OllamaEmbeddingService> logger,
        IConfigService configService,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configService = configService;
        _configuration = configuration;
        _httpClient.Timeout = TimeSpan.FromMinutes(5);  // 向量化可能较慢
    }

    /// <summary>
    /// 向量维度（BGE-M3为1024）
    /// </summary>
    public int Dimensions => DefaultDimensions;

    /// <summary>
    /// 获取配置（优先数据库配置，回退到appsettings）
    /// </summary>
    private async Task<(string baseUrl, string model)> GetConfigAsync()
    {
        if (_cacheExpiry > DateTime.UtcNow && _cachedBaseUrl != null && _cachedModel != null)
        {
            return (_cachedBaseUrl, _cachedModel);
        }

        // 从数据库配置读取
        var baseUrl = await _configService.GetAsync("ollama:base_url");
        var model = await _configService.GetAsync("ollama:embedding_model");

        // 回退到appsettings
        baseUrl = string.IsNullOrEmpty(baseUrl) ? (_configuration["Ollama:BaseUrl"] ?? DefaultBaseUrl) : baseUrl;
        model = string.IsNullOrEmpty(model) ? (_configuration["Ollama:EmbeddingModel"] ?? DefaultModel) : model;

        // 更新缓存
        _cachedBaseUrl = baseUrl;
        _cachedModel = model;
        _cacheExpiry = DateTime.UtcNow.Add(_cacheDuration);

        return (baseUrl, model);
    }

    /// <summary>
    /// 预处理文本：清理特殊字符、多余空白等
    /// </summary>
    private static string PreprocessText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // 替换多个连续空白为单个空格
        text = Regex.Replace(text, @"\s+", " ");
        // 移除控制字符（保留换行和制表符）
        text = Regex.Replace(text, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");
        // 移除零宽字符
        text = Regex.Replace(text, @"[\u200B-\u200D\uFEFF]", "");

        return text.Trim();
    }

    /// <summary>
    /// 生成单个文本的向量嵌入
    /// </summary>
    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<float>();

        try
        {
            var (baseUrl, model) = await GetConfigAsync();

            // 预处理文本
            var processedText = PreprocessText(text);
            if (string.IsNullOrWhiteSpace(processedText))
                return Array.Empty<float>();

            // 限制文本长度，防止超出模型上下文长度
            var truncatedText = processedText.Length > MaxTextLength
                ? processedText[..MaxTextLength]
                : processedText;

            if (processedText.Length > MaxTextLength)
            {
                _logger.LogWarning("文本过长({Length}字符)，已截断至{Max}字符", processedText.Length, MaxTextLength);
            }

            var request = new { model, prompt = truncatedText };
            var response = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/embeddings", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Ollama API错误: {StatusCode} - {Content}", response.StatusCode, errorContent);
                throw new Exception($"Ollama API错误: {response.StatusCode} - {errorContent}");
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();
            return result?.Embedding ?? Array.Empty<float>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama生成向量失败: {Text}", text.Length > 100 ? text[..100] + "..." : text);
            throw;
        }
    }

    /// <summary>
    /// 批量生成文本的向量嵌入
    /// Ollama不支持原生批量，逐个调用
    /// </summary>
    public async Task<List<float[]>> GetEmbeddingsAsync(List<string> texts)
    {
        if (texts == null || texts.Count == 0)
            return new List<float[]>();

        var results = new List<float[]>();
        foreach (var text in texts)
        {
            var embedding = await GetEmbeddingAsync(text);
            results.Add(embedding);
        }
        return results;
    }

    /// <summary>
    /// 检查Ollama服务是否可用
    /// </summary>
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var (baseUrl, _) = await GetConfigAsync();
            var response = await _httpClient.GetAsync($"{baseUrl}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Ollama Embedding响应模型
/// </summary>
internal class OllamaEmbeddingResponse
{
    public float[]? Embedding { get; set; }
}


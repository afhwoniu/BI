using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bi.Domain.Entities;
using Bi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bi.Application.Services;

/// <summary>
/// 系统配置服务实现 - 支持内存缓存、AES加密、热更新
/// </summary>
public class ConfigService : IConfigService
{
    private readonly BiDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigService> _logger;
    
    // 配置缓存（key -> value）
    private static readonly ConcurrentDictionary<string, string?> _cache = new();
    private static bool _isInitialized = false;
    private static readonly object _initLock = new();
    
    // AES加密密钥（从appsettings读取或使用默认值）
    private readonly byte[] _encryptionKey;
    private readonly byte[] _encryptionIv;
    
    public ConfigService(
        BiDbContext db,
        IConfiguration configuration,
        ILogger<ConfigService> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
        
        // 初始化加密密钥
        var keyString = configuration["Encryption:Key"] ?? "BiPlatformConfigKey2024!";
        _encryptionKey = SHA256.HashData(Encoding.UTF8.GetBytes(keyString))[..32];
        _encryptionIv = MD5.HashData(Encoding.UTF8.GetBytes(keyString));
    }
    
    public async Task<string?> GetAsync(string key, string? defaultValue = null)
    {
        await EnsureInitializedAsync();
        
        if (_cache.TryGetValue(key, out var cached))
            return cached ?? defaultValue;
        
        var config = await _db.SysConfigs.FirstOrDefaultAsync(c => c.ConfigKey == key);
        if (config == null)
            return defaultValue;
        
        var value = config.IsEncrypted ? Decrypt(config.ConfigValue) : config.ConfigValue;
        _cache[key] = value;
        return value ?? defaultValue;
    }
    
    public async Task<T?> GetAsync<T>(string key, T? defaultValue = default)
    {
        var value = await GetAsync(key, null);
        if (string.IsNullOrEmpty(value))
            return defaultValue;
        
        try
        {
            var type = typeof(T);
            if (type == typeof(string))
                return (T)(object)value;
            if (type == typeof(int))
                return (T)(object)int.Parse(value);
            if (type == typeof(long))
                return (T)(object)long.Parse(value);
            if (type == typeof(double))
                return (T)(object)double.Parse(value);
            if (type == typeof(bool))
                return (T)(object)bool.Parse(value);
            if (type == typeof(decimal))
                return (T)(object)decimal.Parse(value);
            
            // 复杂类型尝试JSON反序列化
            return JsonSerializer.Deserialize<T>(value);
        }
        catch
        {
            return defaultValue;
        }
    }
    
    public async Task SetAsync(string key, string? value, bool isEncrypted = false)
    {
        var config = await _db.SysConfigs.FirstOrDefaultAsync(c => c.ConfigKey == key);
        
        if (config == null)
        {
            config = new SysConfig
            {
                ConfigKey = key,
                ConfigValue = isEncrypted ? Encrypt(value) : value,
                IsEncrypted = isEncrypted,
                CreatedAt = DateTime.UtcNow
            };
            _db.SysConfigs.Add(config);
        }
        else
        {
            config.ConfigValue = isEncrypted ? Encrypt(value) : value;
            config.IsEncrypted = isEncrypted;
            config.UpdatedAt = DateTime.UtcNow;
        }
        
        await _db.SaveChangesAsync();
        
        // 更新缓存
        _cache[key] = value;
    }
    
    public async Task<List<SysConfig>> GetByGroupAsync(string group)
    {
        var configs = await _db.SysConfigs
            .Where(c => c.ConfigGroup == group)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();

        // 解密敏感配置，返回真实值
        foreach (var config in configs)
        {
            if (config.IsEncrypted)
            {
                config.ConfigValue = Decrypt(config.ConfigValue);
            }
        }

        return configs;
    }
    
    public async Task SaveBatchAsync(List<SysConfig> configs)
    {
        _logger.LogWarning("SaveBatchAsync: 开始保存 {Count} 项配置", configs.Count);

        foreach (var config in configs)
        {
            // 调试日志：记录 docgen 相关配置
            if (config.ConfigKey.StartsWith("docgen"))
            {
                _logger.LogWarning("SaveBatchAsync: 处理配置 key={Key}, value={Value}", config.ConfigKey, config.ConfigValue);
            }

            var existing = await _db.SysConfigs.FirstOrDefaultAsync(c => c.ConfigKey == config.ConfigKey);

            if (existing == null)
            {
                // 新增
                if (config.IsEncrypted && !string.IsNullOrEmpty(config.ConfigValue))
                {
                    config.ConfigValue = Encrypt(config.ConfigValue);
                }
                config.CreatedAt = DateTime.UtcNow;
                _db.SysConfigs.Add(config);
                _logger.LogWarning("SaveBatchAsync: 新增配置 key={Key}", config.ConfigKey);
            }
            else
            {
                // 更新（如果是加密字段且值未变化，跳过）
                if (existing.IsEncrypted && config.ConfigValue == "******")
                    continue;

                var oldValue = existing.ConfigValue;
                existing.ConfigValue = config.IsEncrypted ? Encrypt(config.ConfigValue) : config.ConfigValue;
                existing.DisplayName = config.DisplayName;
                existing.Remark = config.Remark;
                existing.SortOrder = config.SortOrder;
                existing.UpdatedAt = DateTime.UtcNow;

                if (config.ConfigKey.StartsWith("docgen"))
                {
                    _logger.LogWarning("SaveBatchAsync: 更新配置 key={Key}, oldValue={OldValue}, newValue={NewValue}",
                        config.ConfigKey, oldValue, existing.ConfigValue);
                }
            }
        }

        await _db.SaveChangesAsync();
        _logger.LogWarning("SaveBatchAsync: 数据库保存完成，开始刷新缓存");
        await RefreshCacheAsync();
    }
    
    public async Task RefreshCacheAsync()
    {
        _cache.Clear();
        var configs = await _db.SysConfigs.ToListAsync();
        foreach (var config in configs)
        {
            var value = config.IsEncrypted ? Decrypt(config.ConfigValue) : config.ConfigValue;
            _cache[config.ConfigKey] = value;

            // 调试日志：记录 docgen 相关配置
            if (config.ConfigKey.StartsWith("docgen"))
            {
                _logger.LogWarning("RefreshCacheAsync: 缓存配置 key={Key}, value={Value}", config.ConfigKey, value);
            }
        }
        _logger.LogWarning("配置缓存已刷新，共 {Count} 项", configs.Count);
    }

    public async Task InitializeDefaultsAsync()
    {
        // 获取已存在的配置键
        var existingKeys = await _db.SysConfigs.Select(c => c.ConfigKey).ToListAsync();
        var existingKeySet = new HashSet<string>(existingKeys);

        _logger.LogInformation("检查配置初始化，已存在 {Count} 项配置", existingKeys.Count);

        var defaults = new List<SysConfig>
        {
            // 基础设置
            new() { ConfigKey = ConfigKeys.SystemName, ConfigValue = "智能BI可视化系统", ConfigGroup = ConfigGroups.Basic, ConfigType = "string", DisplayName = "系统名称", SortOrder = 1 },
            new() { ConfigKey = ConfigKeys.SystemLogo, ConfigValue = "", ConfigGroup = ConfigGroups.Basic, ConfigType = "string", DisplayName = "系统Logo", Remark = "图片URL或Base64", SortOrder = 2 },
            new() { ConfigKey = ConfigKeys.SystemTheme, ConfigValue = "light", ConfigGroup = ConfigGroups.Basic, ConfigType = "string", DisplayName = "默认主题", Remark = "light/dark", SortOrder = 3 },
            new() { ConfigKey = ConfigKeys.SystemCopyright, ConfigValue = "© 2024 智能BI平台", ConfigGroup = ConfigGroups.Basic, ConfigType = "string", DisplayName = "版权信息", SortOrder = 4 },

            // AI服务配置
            new() { ConfigKey = ConfigKeys.LlmProvider, ConfigValue = "deepseek", ConfigGroup = ConfigGroups.Ai, ConfigType = "string", DisplayName = "LLM提供商", Remark = "deepseek/openai/azure", SortOrder = 1 },
            new() { ConfigKey = ConfigKeys.LlmApiKey, ConfigValue = _configuration["DeepSeek:ApiKey"] ?? "", ConfigGroup = ConfigGroups.Ai, ConfigType = "password", IsEncrypted = true, DisplayName = "LLM API Key", SortOrder = 2 },
            new() { ConfigKey = ConfigKeys.LlmBaseUrl, ConfigValue = _configuration["DeepSeek:BaseUrl"] ?? "https://api.deepseek.com", ConfigGroup = ConfigGroups.Ai, ConfigType = "string", DisplayName = "LLM API地址", SortOrder = 3 },
            new() { ConfigKey = ConfigKeys.LlmModel, ConfigValue = _configuration["DeepSeek:Model"] ?? "deepseek-chat", ConfigGroup = ConfigGroups.Ai, ConfigType = "string", DisplayName = "LLM模型", SortOrder = 4 },
            new() { ConfigKey = ConfigKeys.LlmTemperature, ConfigValue = "0.3", ConfigGroup = ConfigGroups.Ai, ConfigType = "number", DisplayName = "Temperature", Remark = "0-1，越低越稳定", SortOrder = 5 },
            new() { ConfigKey = ConfigKeys.LlmMaxTokens, ConfigValue = "2000", ConfigGroup = ConfigGroups.Ai, ConfigType = "number", DisplayName = "最大Token数", SortOrder = 6 },

            new() { ConfigKey = ConfigKeys.EmbeddingProvider, ConfigValue = "openai", ConfigGroup = ConfigGroups.Ai, ConfigType = "string", DisplayName = "Embedding提供商", Remark = "openai/azure", SortOrder = 7 },
            new() { ConfigKey = ConfigKeys.EmbeddingApiKey, ConfigValue = _configuration["OpenAI:ApiKey"] ?? "", ConfigGroup = ConfigGroups.Ai, ConfigType = "password", IsEncrypted = true, DisplayName = "Embedding API Key", SortOrder = 8 },
            new() { ConfigKey = ConfigKeys.EmbeddingBaseUrl, ConfigValue = _configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com", ConfigGroup = ConfigGroups.Ai, ConfigType = "string", DisplayName = "Embedding API地址", SortOrder = 9 },
            new() { ConfigKey = ConfigKeys.EmbeddingModel, ConfigValue = _configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small", ConfigGroup = ConfigGroups.Ai, ConfigType = "string", DisplayName = "Embedding模型", SortOrder = 10 },
            new() { ConfigKey = ConfigKeys.EmbeddingDimension, ConfigValue = "1536", ConfigGroup = ConfigGroups.Ai, ConfigType = "number", DisplayName = "向量维度", SortOrder = 11 },

            // RAG检索增强配置
            new() { ConfigKey = ConfigKeys.RagEnabled, ConfigValue = "true", ConfigGroup = ConfigGroups.Ai, ConfigType = "boolean", DisplayName = "启用RAG检索增强", Remark = "开启后AI分析会先检索知识库获取上下文", SortOrder = 12 },
            new() { ConfigKey = ConfigKeys.RagTopK, ConfigValue = "5", ConfigGroup = ConfigGroups.Ai, ConfigType = "number", DisplayName = "RAG检索数量", Remark = "每次检索返回的最大结果数", SortOrder = 13 },
            new() { ConfigKey = ConfigKeys.RagMinScore, ConfigValue = "0.5", ConfigGroup = ConfigGroups.Ai, ConfigType = "number", DisplayName = "RAG最低相似度", Remark = "0-1之间，低于此分数的结果会被过滤", SortOrder = 14 },

            // AI默认数据源配置
            new() { ConfigKey = ConfigKeys.AiDefaultDatasource, ConfigValue = "", ConfigGroup = ConfigGroups.Ai, ConfigType = "string", DisplayName = "默认AI数据源ID", Remark = "智能分析页面默认使用的数据源ID，留空则使用第一个数据源", SortOrder = 15 },

            // 语音识别(ASR)配置
            new() { ConfigKey = ConfigKeys.AsrEnabled, ConfigValue = "false", ConfigGroup = ConfigGroups.Ai, ConfigType = "boolean", DisplayName = "启用语音识别", Remark = "开启后可使用语音输入进行AI对话", SortOrder = 17 },
            new() { ConfigKey = ConfigKeys.AsrProvider, ConfigValue = "zhipu", ConfigGroup = ConfigGroups.Ai, ConfigType = "string", DisplayName = "ASR服务商", Remark = "zhipu=智谱AI", SortOrder = 18 },
            new() { ConfigKey = ConfigKeys.AsrApiKey, ConfigValue = "", ConfigGroup = ConfigGroups.Ai, ConfigType = "password", IsEncrypted = true, DisplayName = "ASR API Key", Remark = "语音识别API密钥（可复用智谱LLM的Key）", SortOrder = 19 },
            new() { ConfigKey = ConfigKeys.AsrBaseUrl, ConfigValue = "wss://open.bigmodel.cn/api/paas/v4/audio", ConfigGroup = ConfigGroups.Ai, ConfigType = "string", DisplayName = "ASR API地址", SortOrder = 20 },
            new() { ConfigKey = ConfigKeys.AsrModel, ConfigValue = "glm-4-voice", ConfigGroup = ConfigGroups.Ai, ConfigType = "string", DisplayName = "ASR模型", Remark = "glm-4-voice/glm-asr-2512", SortOrder = 21 },
            new() { ConfigKey = ConfigKeys.AsrStreamEnabled, ConfigValue = "true", ConfigGroup = ConfigGroups.Ai, ConfigType = "boolean", DisplayName = "启用流式识别", Remark = "实时转写，边说边显示文字", SortOrder = 22 },
            new() { ConfigKey = ConfigKeys.AsrLanguage, ConfigValue = "zh", ConfigGroup = ConfigGroups.Ai, ConfigType = "string", DisplayName = "识别语言", Remark = "zh=中文, en=英文", SortOrder = 23 },

            // 语音唤醒配置
            new() { ConfigKey = ConfigKeys.VoiceWakeupEnabled, ConfigValue = "false", ConfigGroup = ConfigGroups.Ai, ConfigType = "boolean", DisplayName = "启用语音唤醒", Remark = "开启后可通过唤醒词激活语音输入", SortOrder = 24 },
            new() { ConfigKey = ConfigKeys.VoiceWakeupWords, ConfigValue = "[\"你好助手\",\"嘿助手\",\"小助手\"]", ConfigGroup = ConfigGroups.Ai, ConfigType = "json", DisplayName = "唤醒词", Remark = "JSON数组格式", SortOrder = 25 },
            new() { ConfigKey = ConfigKeys.VoiceCommandWords, ConfigValue = "[\"执行\",\"发送\",\"查询\",\"开始\",\"分析\"]", ConfigGroup = ConfigGroups.Ai, ConfigType = "json", DisplayName = "指令词", Remark = "说出指令词自动发送", SortOrder = 26 },

            // ===== 业务AI模型配置 =====
            // 智能BI分析
            new() { ConfigKey = ConfigKeys.BizBiUseDefault, ConfigValue = "true", ConfigGroup = ConfigGroups.Ai, ConfigType = "boolean", DisplayName = "BI-使用默认配置", Remark = "使用通用LLM配置", SortOrder = 30 },
            new() { ConfigKey = ConfigKeys.BizBiProvider, ConfigValue = "deepseek", ConfigGroup = ConfigGroups.Ai, ConfigType = "string", DisplayName = "BI-服务商", SortOrder = 31 },
            new() { ConfigKey = ConfigKeys.BizBiModel, ConfigValue = "deepseek-chat", ConfigGroup = ConfigGroups.Ai, ConfigType = "string", DisplayName = "BI-模型", SortOrder = 32 },
            // 智能BI SQL验证
            new() { ConfigKey = ConfigKeys.BizBiSqlValidationEnabled, ConfigValue = "true", ConfigGroup = ConfigGroups.Ai, ConfigType = "boolean", DisplayName = "BI-启用SQL字段验证", Remark = "开启后会验证AI生成SQL中的字段是否存在于表结构中", SortOrder = 33 },
            new() { ConfigKey = ConfigKeys.BizBiSqlValidationMaxRetry, ConfigValue = "2", ConfigGroup = ConfigGroups.Ai, ConfigType = "number", DisplayName = "BI-SQL验证最大重试次数", Remark = "验证失败时AI自动修正的最大尝试次数", SortOrder = 34 },

            // 患者360
            new() { ConfigKey = ConfigKeys.BizHz360UseDefault, ConfigValue = "true", ConfigGroup = ConfigGroups.Ai, ConfigType = "boolean", DisplayName = "患者360-使用默认配置", SortOrder = 35 },
            new() { ConfigKey = ConfigKeys.BizHz360Provider, ConfigValue = "deepseek", ConfigGroup = ConfigGroups.Ai, ConfigType = "string", DisplayName = "患者360-服务商", SortOrder = 36 },
            new() { ConfigKey = ConfigKeys.BizHz360Model, ConfigValue = "deepseek-chat", ConfigGroup = ConfigGroups.Ai, ConfigType = "string", DisplayName = "患者360-模型", SortOrder = 37 },

            // AI检索增强
            new() { ConfigKey = ConfigKeys.BizSearchUseDefault, ConfigValue = "true", ConfigGroup = ConfigGroups.Ai, ConfigType = "boolean", DisplayName = "检索-使用默认配置", SortOrder = 40 },
            new() { ConfigKey = ConfigKeys.BizSearchProvider, ConfigValue = "deepseek", ConfigGroup = ConfigGroups.Ai, ConfigType = "string", DisplayName = "检索-服务商", SortOrder = 41 },
            new() { ConfigKey = ConfigKeys.BizSearchModel, ConfigValue = "deepseek-chat", ConfigGroup = ConfigGroups.Ai, ConfigType = "string", DisplayName = "检索-模型", SortOrder = 42 },

            // PPT/Word文档生成
            new() { ConfigKey = ConfigKeys.BizDocGenUseDefault, ConfigValue = "true", ConfigGroup = ConfigGroups.Ai, ConfigType = "boolean", DisplayName = "文档生成-使用默认配置", SortOrder = 45 },
            new() { ConfigKey = ConfigKeys.BizDocGenProvider, ConfigValue = "deepseek", ConfigGroup = ConfigGroups.Ai, ConfigType = "string", DisplayName = "文档生成-服务商", SortOrder = 46 },
            new() { ConfigKey = ConfigKeys.BizDocGenModel, ConfigValue = "deepseek-chat", ConfigGroup = ConfigGroups.Ai, ConfigType = "string", DisplayName = "文档生成-模型", SortOrder = 47 },

            // 安全配置
            new() { ConfigKey = ConfigKeys.TokenExpireHours, ConfigValue = "24", ConfigGroup = ConfigGroups.Security, ConfigType = "number", DisplayName = "Token过期时间(小时)", SortOrder = 1 },
            new() { ConfigKey = ConfigKeys.LoginFailLockCount, ConfigValue = "5", ConfigGroup = ConfigGroups.Security, ConfigType = "number", DisplayName = "登录失败锁定次数", SortOrder = 2 },
            new() { ConfigKey = ConfigKeys.LoginLockMinutes, ConfigValue = "30", ConfigGroup = ConfigGroups.Security, ConfigType = "number", DisplayName = "锁定时长(分钟)", SortOrder = 3 },
            new() { ConfigKey = ConfigKeys.PasswordStrength, ConfigValue = "medium", ConfigGroup = ConfigGroups.Security, ConfigType = "string", DisplayName = "密码强度要求", Remark = "low/medium/high", SortOrder = 4 },

            // 缓存配置
            new() { ConfigKey = ConfigKeys.CacheType, ConfigValue = "memory", ConfigGroup = ConfigGroups.Cache, ConfigType = "string", DisplayName = "缓存类型", Remark = "memory/redis", SortOrder = 1 },
            new() { ConfigKey = ConfigKeys.RedisConnection, ConfigValue = "", ConfigGroup = ConfigGroups.Cache, ConfigType = "string", DisplayName = "Redis连接字符串", SortOrder = 2 },
            new() { ConfigKey = ConfigKeys.DefaultCacheMinutes, ConfigValue = "30", ConfigGroup = ConfigGroups.Cache, ConfigType = "number", DisplayName = "默认缓存时间(分钟)", SortOrder = 3 },
            new() { ConfigKey = ConfigKeys.ChartCacheMinutes, ConfigValue = "5", ConfigGroup = ConfigGroups.Cache, ConfigType = "number", DisplayName = "图表缓存时间(分钟)", SortOrder = 4 },

            // 数据配置
            new() { ConfigKey = ConfigKeys.QueryTimeoutSeconds, ConfigValue = "30", ConfigGroup = ConfigGroups.Data, ConfigType = "number", DisplayName = "查询超时(秒)", SortOrder = 1 },
            new() { ConfigKey = ConfigKeys.PreviewRowCount, ConfigValue = "100", ConfigGroup = ConfigGroups.Data, ConfigType = "number", DisplayName = "预览数据行数", SortOrder = 2 },
            new() { ConfigKey = ConfigKeys.MaxExportRows, ConfigValue = "100000", ConfigGroup = ConfigGroups.Data, ConfigType = "number", DisplayName = "最大导出行数", SortOrder = 3 },
            new() { ConfigKey = ConfigKeys.AlertNotifyWebhookUrl, ConfigValue = "", ConfigGroup = ConfigGroups.Data, ConfigType = "string", DisplayName = "预警-默认Webhook地址", Remark = "当规则通知渠道为webhook且未配置订阅地址时使用", SortOrder = 10 },
            new() { ConfigKey = ConfigKeys.AlertNotifyWecomWebhook, ConfigValue = "", ConfigGroup = ConfigGroups.Data, ConfigType = "string", DisplayName = "预警-默认企微机器人地址", Remark = "当规则通知渠道为wecom且未配置订阅地址时使用", SortOrder = 11 },
        };

        // 过滤出缺失的配置项
        var missingConfigs = defaults.Where(d => !existingKeySet.Contains(d.ConfigKey)).ToList();

        if (missingConfigs.Count == 0)
        {
            _logger.LogInformation("所有配置项已存在，无需初始化");
            return;
        }

        _logger.LogInformation("发现 {Count} 项缺失的配置，开始补充...", missingConfigs.Count);

        // 加密敏感配置
        foreach (var config in missingConfigs)
        {
            if (config.IsEncrypted && !string.IsNullOrEmpty(config.ConfigValue))
            {
                config.ConfigValue = Encrypt(config.ConfigValue);
            }
            config.CreatedAt = DateTime.UtcNow;
        }

        await _db.SysConfigs.AddRangeAsync(missingConfigs);
        await _db.SaveChangesAsync();
        _logger.LogInformation("配置初始化完成，新增 {Count} 项", missingConfigs.Count);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;

        lock (_initLock)
        {
            if (_isInitialized) return;
            _isInitialized = true;
        }

        // 首次加载缓存
        await RefreshCacheAsync();
    }

    /// <summary>
    /// AES加密
    /// </summary>
    private string? Encrypt(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.IV = _encryptionIv;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// AES解密
    /// </summary>
    private string? Decrypt(string? cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        try
        {
            using var aes = Aes.Create();
            aes.Key = _encryptionKey;
            aes.IV = _encryptionIv;

            using var decryptor = aes.CreateDecryptor();
            var cipherBytes = Convert.FromBase64String(cipherText);
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解密配置失败，可能是未加密的旧数据");
            return cipherText; // 返回原值
        }
    }
}

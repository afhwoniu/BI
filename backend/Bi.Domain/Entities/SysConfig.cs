namespace Bi.Domain.Entities;

/// <summary>
/// 系统配置表 - 存储系统各项配置参数
/// </summary>
public class SysConfig
{
    /// <summary>
    /// 主键ID
    /// </summary>
    public long Id { get; set; }
    
    /// <summary>
    /// 配置键（唯一标识）
    /// </summary>
    public string ConfigKey { get; set; } = string.Empty;
    
    /// <summary>
    /// 配置值
    /// </summary>
    public string? ConfigValue { get; set; }
    
    /// <summary>
    /// 配置分组：basic-基础设置, ai-AI服务, security-安全配置, cache-缓存配置, data-数据配置
    /// </summary>
    public string ConfigGroup { get; set; } = "basic";
    
    /// <summary>
    /// 配置类型：string, number, boolean, json, password
    /// </summary>
    public string ConfigType { get; set; } = "string";
    
    /// <summary>
    /// 是否加密存储（敏感配置如API Key）
    /// </summary>
    public bool IsEncrypted { get; set; } = false;
    
    /// <summary>
    /// 配置名称（显示用）
    /// </summary>
    public string? DisplayName { get; set; }
    
    /// <summary>
    /// 配置说明
    /// </summary>
    public string? Remark { get; set; }
    
    /// <summary>
    /// 排序号
    /// </summary>
    public int SortOrder { get; set; } = 0;
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// 配置分组常量
/// </summary>
public static class ConfigGroups
{
    /// <summary>
    /// 基础设置
    /// </summary>
    public const string Basic = "basic";
    
    /// <summary>
    /// AI服务配置
    /// </summary>
    public const string Ai = "ai";
    
    /// <summary>
    /// 安全配置
    /// </summary>
    public const string Security = "security";
    
    /// <summary>
    /// 缓存配置
    /// </summary>
    public const string Cache = "cache";
    
    /// <summary>
    /// 数据配置
    /// </summary>
    public const string Data = "data";
}

/// <summary>
/// 配置键常量
/// </summary>
public static class ConfigKeys
{
    // ===== 基础设置 =====
    public const string SystemName = "system.name";
    public const string SystemLogo = "system.logo";
    public const string SystemTheme = "system.theme";
    public const string SystemCopyright = "system.copyright";
    
    // ===== AI服务配置 =====
    public const string LlmProvider = "ai.llm.provider";
    public const string LlmApiKey = "ai.llm.apiKey";
    public const string LlmBaseUrl = "ai.llm.baseUrl";
    public const string LlmModel = "ai.llm.model";
    public const string LlmTemperature = "ai.llm.temperature";
    public const string LlmMaxTokens = "ai.llm.maxTokens";
    
    public const string EmbeddingProvider = "ai.embedding.provider";
    public const string EmbeddingApiKey = "ai.embedding.apiKey";
    public const string EmbeddingBaseUrl = "ai.embedding.baseUrl";
    public const string EmbeddingModel = "ai.embedding.model";
    public const string EmbeddingDimension = "ai.embedding.dimension";

    // AI自定义提示词
    public const string AiCustomPrompt = "ai.customPrompt";

    /// <summary>
    /// 默认AI数据源ID（智能分析页面使用）
    /// </summary>
    public const string AiDefaultDatasource = "ai.default.datasource";

    // RAG检索配置
    /// <summary>
    /// 是否启用RAG检索增强
    /// </summary>
    public const string RagEnabled = "ai.rag.enabled";
    /// <summary>
    /// RAG检索返回的最大结果数
    /// </summary>
    public const string RagTopK = "ai.rag.topK";
    /// <summary>
    /// RAG检索最低相似度阈值
    /// </summary>
    public const string RagMinScore = "ai.rag.minScore";

    // ===== 语音识别(ASR)配置 =====
    /// <summary>
    /// 是否启用语音识别功能
    /// </summary>
    public const string AsrEnabled = "ai.asr.enabled";
    /// <summary>
    /// ASR服务提供商（zhipu=智谱AI）
    /// </summary>
    public const string AsrProvider = "ai.asr.provider";
    /// <summary>
    /// ASR API Key（使用智谱平台的API Key）
    /// </summary>
    public const string AsrApiKey = "ai.asr.apiKey";
    /// <summary>
    /// ASR API地址
    /// </summary>
    public const string AsrBaseUrl = "ai.asr.baseUrl";
    /// <summary>
    /// ASR模型名称
    /// </summary>
    public const string AsrModel = "ai.asr.model";
    /// <summary>
    /// 是否启用流式识别（实时转写）
    /// </summary>
    public const string AsrStreamEnabled = "ai.asr.streamEnabled";
    /// <summary>
    /// 语音识别语言（zh=中文, en=英文）
    /// </summary>
    public const string AsrLanguage = "ai.asr.language";

    // ===== 语音唤醒配置 =====
    /// <summary>
    /// 是否启用语音唤醒功能
    /// </summary>
    public const string VoiceWakeupEnabled = "ai.voice.wakeup.enabled";
    /// <summary>
    /// 唤醒词列表（JSON数组格式）
    /// </summary>
    public const string VoiceWakeupWords = "ai.voice.wakeup.words";
    /// <summary>
    /// 指令词列表（JSON数组格式，说出这些词自动发送）
    /// </summary>
    public const string VoiceCommandWords = "ai.voice.command.words";

    // ===== 业务AI模型配置 =====
    // 业务配置格式: ai.biz.{业务名}.{配置项}
    // 每个业务可配置: useDefault(是否使用默认配置), provider(服务商), model(模型)

    /// <summary>
    /// 智能BI分析 - 使用默认配置
    /// </summary>
    public const string BizBiUseDefault = "ai.biz.bi.useDefault";
    /// <summary>
    /// 智能BI分析 - 服务商
    /// </summary>
    public const string BizBiProvider = "ai.biz.bi.provider";
    /// <summary>
    /// 智能BI分析 - 模型
    /// </summary>
    public const string BizBiModel = "ai.biz.bi.model";

    // ===== 智能BI SQL验证配置 =====
    /// <summary>
    /// 是否启用SQL字段验证（AI生成SQL后验证字段是否存在于表结构中）
    /// </summary>
    public const string BizBiSqlValidationEnabled = "ai.biz.bi.sqlValidation.enabled";
    /// <summary>
    /// SQL验证失败时的最大重试次数（AI自动修正）
    /// </summary>
    public const string BizBiSqlValidationMaxRetry = "ai.biz.bi.sqlValidation.maxRetry";

    /// <summary>
    /// 患者360 - 使用默认配置
    /// </summary>
    public const string BizHz360UseDefault = "ai.biz.hz360.useDefault";
    /// <summary>
    /// 患者360 - 服务商
    /// </summary>
    public const string BizHz360Provider = "ai.biz.hz360.provider";
    /// <summary>
    /// 患者360 - 模型
    /// </summary>
    public const string BizHz360Model = "ai.biz.hz360.model";

    /// <summary>
    /// AI检索增强 - 使用默认配置
    /// </summary>
    public const string BizSearchUseDefault = "ai.biz.search.useDefault";
    /// <summary>
    /// AI检索增强 - 服务商
    /// </summary>
    public const string BizSearchProvider = "ai.biz.search.provider";
    /// <summary>
    /// AI检索增强 - 模型
    /// </summary>
    public const string BizSearchModel = "ai.biz.search.model";

    /// <summary>
    /// PPT/Word文档生成 - 使用默认配置
    /// </summary>
    public const string BizDocGenUseDefault = "ai.biz.docgen.useDefault";
    /// <summary>
    /// PPT/Word文档生成 - 服务商
    /// </summary>
    public const string BizDocGenProvider = "ai.biz.docgen.provider";
    /// <summary>
    /// PPT/Word文档生成 - 模型
    /// </summary>
    public const string BizDocGenModel = "ai.biz.docgen.model";

    // ===== 安全配置 =====
    public const string TokenExpireHours = "security.tokenExpireHours";
    public const string LoginFailLockCount = "security.loginFailLockCount";
    public const string LoginLockMinutes = "security.loginLockMinutes";
    public const string PasswordStrength = "security.passwordStrength";
    
    // ===== 缓存配置 =====
    public const string CacheType = "cache.type";
    public const string RedisConnection = "cache.redisConnection";
    public const string DefaultCacheMinutes = "cache.defaultMinutes";
    public const string ChartCacheMinutes = "cache.chartMinutes";
    
    // ===== 数据配置 =====
    public const string QueryTimeoutSeconds = "data.queryTimeoutSeconds";
    public const string PreviewRowCount = "data.previewRowCount";
    public const string MaxExportRows = "data.maxExportRows";

    // ===== 预警通知配置 =====
    public const string AlertNotifyWebhookUrl = "alert.notify.webhookUrl";
    public const string AlertNotifyWecomWebhook = "alert.notify.wecomWebhook";

    // ===== 系统内部配置 =====
    /// <summary>
    /// 开发说明（Markdown格式，用于记录系统开发要点）
    /// </summary>
    public const string DevNotes = "system.devNotes";
}

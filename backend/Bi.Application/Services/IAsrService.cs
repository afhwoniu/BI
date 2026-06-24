namespace Bi.Application.Services;

/// <summary>
/// ASR语音识别服务接口
/// </summary>
public interface IAsrService
{
    /// <summary>
    /// 检查ASR服务是否已启用
    /// </summary>
    Task<bool> IsEnabledAsync();

    /// <summary>
    /// 获取ASR配置信息
    /// </summary>
    Task<AsrConfig> GetConfigAsync();

    /// <summary>
    /// 上传音频文件进行转写（非流式）
    /// </summary>
    /// <param name="audioData">音频数据</param>
    /// <param name="format">音频格式（wav/mp3/webm等）</param>
    /// <returns>转写结果</returns>
    Task<AsrResult> TranscribeAsync(byte[] audioData, string format = "wav");

    /// <summary>
    /// 清除配置缓存（配置更新后调用）
    /// </summary>
    void ClearCache();
}

/// <summary>
/// ASR配置信息
/// </summary>
public class AsrConfig
{
    /// <summary>
    /// 是否启用ASR
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 服务提供商
    /// </summary>
    public string Provider { get; set; } = "zhipu";

    /// <summary>
    /// 模型名称
    /// </summary>
    public string Model { get; set; } = "glm-4-voice";

    /// <summary>
    /// 是否启用流式识别
    /// </summary>
    public bool StreamEnabled { get; set; } = true;

    /// <summary>
    /// 识别语言
    /// </summary>
    public string Language { get; set; } = "zh";

    /// <summary>
    /// WebSocket连接地址（前端使用）
    /// </summary>
    public string WebSocketUrl { get; set; } = string.Empty;

    // ===== 语音唤醒配置 =====
    /// <summary>
    /// 是否启用语音唤醒
    /// </summary>
    public bool WakeupEnabled { get; set; }

    /// <summary>
    /// 唤醒词列表（如：你好助手、小助手）
    /// </summary>
    public List<string> WakeupWords { get; set; } = new();

    /// <summary>
    /// 指令词列表（如：执行、发送，说出后自动提交）
    /// </summary>
    public List<string> CommandWords { get; set; } = new();
}

/// <summary>
/// ASR转写结果
/// </summary>
public class AsrResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 转写文本
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 音频时长(秒)
    /// </summary>
    public double? Duration { get; set; }

    /// <summary>
    /// 置信度(0-1)
    /// </summary>
    public double? Confidence { get; set; }
}


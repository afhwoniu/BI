using System.Text.Json;

namespace Bi.Application.Services;

/// <summary>
/// LLM大语言模型服务接口
/// 支持多种LLM提供商（DeepSeek、OpenAI等）
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// 发送聊天请求，获取完整响应
    /// </summary>
    /// <param name="messages">消息列表</param>
    /// <param name="options">可选参数</param>
    /// <returns>AI响应内容</returns>
    Task<LlmResponse> ChatAsync(List<LlmMessage> messages, LlmOptions? options = null);

    /// <summary>
    /// 发送聊天请求，流式返回响应
    /// </summary>
    /// <param name="messages">消息列表</param>
    /// <param name="options">可选参数</param>
    /// <returns>异步流式响应</returns>
    IAsyncEnumerable<string> ChatStreamAsync(List<LlmMessage> messages, LlmOptions? options = null);

    /// <summary>
    /// 发送带工具调用的聊天请求（Function Calling）
    /// </summary>
    /// <param name="messages">消息列表</param>
    /// <param name="tools">可用工具列表</param>
    /// <param name="options">可选参数</param>
    /// <returns>AI响应（可能包含工具调用）</returns>
    Task<LlmToolResponse> ChatWithToolsAsync(List<LlmMessage> messages, List<LlmTool> tools, LlmOptions? options = null);
}

/// <summary>
/// LLM消息
/// </summary>
public class LlmMessage
{
    /// <summary>
    /// 角色：system, user, assistant
    /// </summary>
    public string Role { get; set; } = "user";
    
    /// <summary>
    /// 消息内容
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    public static LlmMessage System(string content) => new() { Role = "system", Content = content };
    public static LlmMessage User(string content) => new() { Role = "user", Content = content };
    public static LlmMessage Assistant(string content) => new() { Role = "assistant", Content = content };
}

/// <summary>
/// AI业务类型枚举
/// </summary>
public enum AiBusinessType
{
    /// <summary>
    /// 默认（使用通用配置）
    /// </summary>
    Default = 0,

    /// <summary>
    /// 智能BI分析
    /// </summary>
    Bi = 1,

    /// <summary>
    /// 患者360
    /// </summary>
    Hz360 = 2,

    /// <summary>
    /// AI检索增强
    /// </summary>
    Search = 3,

    /// <summary>
    /// PPT/Word文档生成
    /// </summary>
    DocGen = 4
}

/// <summary>
/// LLM请求选项
/// </summary>
public class LlmOptions
{
    /// <summary>
    /// 模型名称（优先级最高，如果设置则覆盖业务配置）
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// 温度参数（0-2），越低越确定性
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// 最大生成token数
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// 停止序列
    /// </summary>
    public List<string>? StopSequences { get; set; }

    /// <summary>
    /// 业务类型（用于读取对应的业务AI配置）
    /// </summary>
    public AiBusinessType BusinessType { get; set; } = AiBusinessType.Default;
}

/// <summary>
/// LLM响应
/// </summary>
public class LlmResponse
{
    /// <summary>
    /// 响应内容
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// 使用的token数
    /// </summary>
    public int? TotalTokens { get; set; }
    
    /// <summary>
    /// 输入token数
    /// </summary>
    public int? PromptTokens { get; set; }
    
    /// <summary>
    /// 输出token数
    /// </summary>
    public int? CompletionTokens { get; set; }
    
    /// <summary>
    /// 使用的模型
    /// </summary>
    public string? Model { get; set; }
    
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; } = true;
    
    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; set; }
}

#region Function Calling 相关类型

/// <summary>
/// LLM 工具定义（用于 Function Calling）
/// </summary>
public class LlmTool
{
    /// <summary>
    /// 工具类型（固定为 "function"）
    /// </summary>
    public string Type { get; set; } = "function";

    /// <summary>
    /// 函数定义
    /// </summary>
    public LlmFunction Function { get; set; } = new();
}

/// <summary>
/// 函数定义
/// </summary>
public class LlmFunction
{
    /// <summary>
    /// 函数名称
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 函数描述
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// 参数定义（JSON Schema 格式）
    /// </summary>
    public JsonElement? Parameters { get; set; }
}

/// <summary>
/// 带工具调用的 LLM 响应
/// </summary>
public class LlmToolResponse
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 响应内容（纯文本回复时有值）
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// 工具调用列表（当 LLM 决定调用工具时有值）
    /// </summary>
    public List<LlmToolCall>? ToolCalls { get; set; }

    /// <summary>
    /// 完成原因：stop（正常结束）、tool_calls（需要调用工具）
    /// </summary>
    public string? FinishReason { get; set; }

    /// <summary>
    /// 使用的模型
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Token 使用情况
    /// </summary>
    public int? TotalTokens { get; set; }
}

/// <summary>
/// 工具调用
/// </summary>
public class LlmToolCall
{
    /// <summary>
    /// 工具调用 ID
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// 工具类型（固定为 "function"）
    /// </summary>
    public string Type { get; set; } = "function";

    /// <summary>
    /// 函数调用详情
    /// </summary>
    public LlmFunctionCall Function { get; set; } = new();
}

/// <summary>
/// 函数调用详情
/// </summary>
public class LlmFunctionCall
{
    /// <summary>
    /// 函数名称
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 函数参数（JSON 字符串）
    /// </summary>
    public string Arguments { get; set; } = "{}";
}

#endregion

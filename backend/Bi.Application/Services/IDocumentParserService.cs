namespace Bi.Application.Services;

/// <summary>
/// 文档解析服务接口
/// 负责将各种格式的文档解析为纯文本
/// </summary>
public interface IDocumentParserService
{
    /// <summary>
    /// 解析文档内容
    /// </summary>
    /// <param name="stream">文件流</param>
    /// <param name="fileType">文件类型（扩展名，如txt、md、pdf、docx）</param>
    /// <returns>解析结果</returns>
    Task<DocumentParseResult> ParseAsync(Stream stream, string fileType);
    
    /// <summary>
    /// 检查是否支持该文件类型
    /// </summary>
    bool IsSupported(string fileType);
}

/// <summary>
/// 文档解析结果
/// </summary>
public class DocumentParseResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// 解析后的纯文本内容
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// 按页分割的内容（PDF等分页文档）
    /// </summary>
    public List<PageContent>? Pages { get; set; }
    
    /// <summary>
    /// 文档元数据
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
    
    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static DocumentParseResult Ok(string content, List<PageContent>? pages = null)
    {
        return new DocumentParseResult
        {
            Success = true,
            Content = content,
            Pages = pages
        };
    }
    
    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static DocumentParseResult Fail(string error)
    {
        return new DocumentParseResult
        {
            Success = false,
            ErrorMessage = error
        };
    }
}

/// <summary>
/// 页面内容
/// </summary>
public class PageContent
{
    /// <summary>
    /// 页码（从1开始）
    /// </summary>
    public int PageNumber { get; set; }
    
    /// <summary>
    /// 页面文本内容
    /// </summary>
    public string Content { get; set; } = string.Empty;
}


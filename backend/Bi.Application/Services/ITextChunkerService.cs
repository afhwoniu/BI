namespace Bi.Application.Services;

/// <summary>
/// 文本分块服务接口
/// 将长文本分割成适合向量化的小块
/// </summary>
public interface ITextChunkerService
{
    /// <summary>
    /// 将文本分割成块
    /// </summary>
    /// <param name="text">原始文本</param>
    /// <param name="options">分块选项</param>
    /// <returns>分块列表</returns>
    List<TextChunk> ChunkText(string text, ChunkOptions? options = null);
    
    /// <summary>
    /// 将按页分割的文档分块
    /// </summary>
    /// <param name="pages">页面列表</param>
    /// <param name="options">分块选项</param>
    /// <returns>分块列表（包含页码信息）</returns>
    List<TextChunk> ChunkPages(List<PageContent> pages, ChunkOptions? options = null);
}

/// <summary>
/// 分块选项
/// </summary>
public class ChunkOptions
{
    /// <summary>
    /// 分块大小（字符数）
    /// </summary>
    public int ChunkSize { get; set; } = 500;
    
    /// <summary>
    /// 重叠大小（字符数）
    /// </summary>
    public int ChunkOverlap { get; set; } = 50;
    
    /// <summary>
    /// 分块策略
    /// </summary>
    public ChunkStrategy Strategy { get; set; } = ChunkStrategy.FixedSize;
    
    /// <summary>
    /// 段落分隔符（用于按段落分块）
    /// </summary>
    public string[] ParagraphSeparators { get; set; } = new[] { "\n\n", "\r\n\r\n" };
}

/// <summary>
/// 分块策略
/// </summary>
public enum ChunkStrategy
{
    /// <summary>
    /// 固定大小分块
    /// </summary>
    FixedSize,
    
    /// <summary>
    /// 按段落分块
    /// </summary>
    Paragraph,
    
    /// <summary>
    /// 按句子分块
    /// </summary>
    Sentence
}

/// <summary>
/// 文本块
/// </summary>
public class TextChunk
{
    /// <summary>
    /// 块索引（从0开始）
    /// </summary>
    public int Index { get; set; }
    
    /// <summary>
    /// 块内容
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// 内容长度
    /// </summary>
    public int Length => Content.Length;
    
    /// <summary>
    /// 所在页码（如果有）
    /// </summary>
    public int? PageNumber { get; set; }
    
    /// <summary>
    /// 章节标题（如果有）
    /// </summary>
    public string? SectionTitle { get; set; }
}


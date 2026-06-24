using System.Text.RegularExpressions;

namespace Bi.Application.Services;

/// <summary>
/// 文本分块服务实现
/// 支持固定大小、按段落、按句子等分块策略
/// </summary>
public class TextChunkerService : ITextChunkerService
{
    // 句子结束符
    private static readonly Regex SentenceEndRegex = new(@"[。！？.!?]\s*", RegexOptions.Compiled);
    
    /// <summary>
    /// 将文本分割成块
    /// </summary>
    public List<TextChunk> ChunkText(string text, ChunkOptions? options = null)
    {
        options ??= new ChunkOptions();
        
        if (string.IsNullOrWhiteSpace(text))
            return new List<TextChunk>();

        return options.Strategy switch
        {
            ChunkStrategy.Paragraph => ChunkByParagraph(text, options),
            ChunkStrategy.Sentence => ChunkBySentence(text, options),
            _ => ChunkByFixedSize(text, options)
        };
    }

    /// <summary>
    /// 将按页分割的文档分块
    /// </summary>
    public List<TextChunk> ChunkPages(List<PageContent> pages, ChunkOptions? options = null)
    {
        options ??= new ChunkOptions();
        var allChunks = new List<TextChunk>();
        var globalIndex = 0;

        foreach (var page in pages)
        {
            var pageChunks = ChunkText(page.Content, options);
            foreach (var chunk in pageChunks)
            {
                chunk.Index = globalIndex++;
                chunk.PageNumber = page.PageNumber;
                allChunks.Add(chunk);
            }
        }

        return allChunks;
    }

    /// <summary>
    /// 固定大小分块
    /// </summary>
    private static List<TextChunk> ChunkByFixedSize(string text, ChunkOptions options)
    {
        var chunks = new List<TextChunk>();
        var chunkSize = options.ChunkSize;
        var overlap = options.ChunkOverlap;
        var step = chunkSize - overlap;
        
        if (step <= 0) step = chunkSize;

        var index = 0;
        var start = 0;
        
        while (start < text.Length)
        {
            var length = Math.Min(chunkSize, text.Length - start);
            var content = text.Substring(start, length).Trim();
            
            if (!string.IsNullOrWhiteSpace(content))
            {
                chunks.Add(new TextChunk
                {
                    Index = index++,
                    Content = content
                });
            }
            
            start += step;
            if (start + overlap >= text.Length && start < text.Length)
            {
                // 最后一块
                break;
            }
        }

        return chunks;
    }

    /// <summary>
    /// 按段落分块
    /// 如果段落太少或单个段落太长，会使用回退策略
    /// </summary>
    private static List<TextChunk> ChunkByParagraph(string text, ChunkOptions options)
    {
        var chunks = new List<TextChunk>();

        // 尝试多种段落分隔符
        var separators = new[] { "\n\n", "\r\n\r\n", "\n", "\r\n" };
        string[] paragraphs = Array.Empty<string>();

        foreach (var sep in separators)
        {
            paragraphs = text.Split(new[] { sep }, StringSplitOptions.RemoveEmptyEntries);
            // 如果分出来的段落数量合理，就使用这个分隔符
            if (paragraphs.Length > 1) break;
        }

        // 如果还是只有1个段落或者段落都很长，回退到固定大小分块
        if (paragraphs.Length <= 1 || paragraphs.Any(p => p.Length > options.ChunkSize * 3))
        {
            return ChunkByFixedSize(text, options);
        }

        var currentChunk = new List<string>();
        var currentLength = 0;
        var index = 0;

        foreach (var para in paragraphs)
        {
            var trimmed = para.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            // 如果单个段落超过限制，需要拆分这个段落
            if (trimmed.Length > options.ChunkSize)
            {
                // 先保存之前累积的块
                if (currentChunk.Count > 0)
                {
                    chunks.Add(new TextChunk
                    {
                        Index = index++,
                        Content = string.Join("\n\n", currentChunk)
                    });
                    currentChunk.Clear();
                    currentLength = 0;
                }

                // 对这个长段落使用固定大小分块
                var subChunks = ChunkByFixedSize(trimmed, options);
                foreach (var subChunk in subChunks)
                {
                    subChunk.Index = index++;
                    chunks.Add(subChunk);
                }
                continue;
            }

            // 如果当前段落加入后超过限制，先保存当前块
            if (currentLength + trimmed.Length > options.ChunkSize && currentChunk.Count > 0)
            {
                chunks.Add(new TextChunk
                {
                    Index = index++,
                    Content = string.Join("\n\n", currentChunk)
                });

                // 保留最后一个段落作为重叠
                if (options.ChunkOverlap > 0 && currentChunk.Count > 0)
                {
                    var lastPara = currentChunk[^1];
                    currentChunk.Clear();
                    currentChunk.Add(lastPara);
                    currentLength = lastPara.Length;
                }
                else
                {
                    currentChunk.Clear();
                    currentLength = 0;
                }
            }

            currentChunk.Add(trimmed);
            currentLength += trimmed.Length;
        }

        // 保存最后一块
        if (currentChunk.Count > 0)
        {
            chunks.Add(new TextChunk
            {
                Index = index,
                Content = string.Join("\n\n", currentChunk)
            });
        }

        return chunks;
    }

    /// <summary>
    /// 按句子分块
    /// </summary>
    private static List<TextChunk> ChunkBySentence(string text, ChunkOptions options)
    {
        var chunks = new List<TextChunk>();
        var sentences = SentenceEndRegex.Split(text)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

        var currentChunk = new List<string>();
        var currentLength = 0;
        var index = 0;

        foreach (var sentence in sentences)
        {
            if (currentLength + sentence.Length > options.ChunkSize && currentChunk.Count > 0)
            {
                chunks.Add(new TextChunk
                {
                    Index = index++,
                    Content = string.Join(" ", currentChunk)
                });

                // 保留最后几个句子作为重叠
                var overlapLength = 0;
                var overlapSentences = new List<string>();
                for (int i = currentChunk.Count - 1; i >= 0 && overlapLength < options.ChunkOverlap; i--)
                {
                    overlapSentences.Insert(0, currentChunk[i]);
                    overlapLength += currentChunk[i].Length;
                }

                currentChunk = overlapSentences;
                currentLength = overlapLength;
            }

            currentChunk.Add(sentence);
            currentLength += sentence.Length;
        }

        if (currentChunk.Count > 0)
        {
            chunks.Add(new TextChunk
            {
                Index = index,
                Content = string.Join(" ", currentChunk)
            });
        }

        return chunks;
    }
}


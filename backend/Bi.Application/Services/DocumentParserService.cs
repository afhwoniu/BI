using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using NPOI.XWPF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.HSSF.UserModel;

namespace Bi.Application.Services;

/// <summary>
/// 文档解析服务实现
/// 支持txt、md、pdf、docx、xls、xlsx格式
/// </summary>
public class DocumentParserService : IDocumentParserService
{
    private readonly ILogger<DocumentParserService> _logger;

    // 支持的文件类型（不支持旧版doc，需转换为docx）
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "txt", "md", "pdf", "docx", "xls", "xlsx"
    };

    public DocumentParserService(ILogger<DocumentParserService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 检查是否支持该文件类型
    /// </summary>
    public bool IsSupported(string fileType)
    {
        return SupportedTypes.Contains(fileType.TrimStart('.'));
    }

    /// <summary>
    /// 解析文档内容
    /// </summary>
    public async Task<DocumentParseResult> ParseAsync(Stream stream, string fileType)
    {
        var type = fileType.TrimStart('.').ToLowerInvariant();
        
        try
        {
            return type switch
            {
                "txt" or "md" => await ParseTextAsync(stream),
                "pdf" => ParsePdf(stream),
                "docx" => ParseDocx(stream),
                "doc" => DocumentParseResult.Fail("暂不支持旧版.doc格式，请转换为.docx"),
                "xlsx" => ParseExcel(stream, true),
                "xls" => ParseExcel(stream, false),
                _ => DocumentParseResult.Fail($"不支持的文件类型: {fileType}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文档解析失败: {FileType}", fileType);
            return DocumentParseResult.Fail($"解析失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 解析纯文本文件（txt、md）
    /// </summary>
    private static async Task<DocumentParseResult> ParseTextAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = await reader.ReadToEndAsync();
        return DocumentParseResult.Ok(content);
    }

    /// <summary>
    /// 解析PDF文件
    /// </summary>
    private DocumentParseResult ParsePdf(Stream stream)
    {
        var pages = new List<PageContent>();
        var fullText = new StringBuilder();

        using var document = PdfDocument.Open(stream);

        foreach (var page in document.GetPages())
        {
            // 清理页面文本，移除无效字符
            var pageText = CleanText(page.Text);
            pages.Add(new PageContent
            {
                PageNumber = page.Number,
                Content = pageText
            });
            fullText.AppendLine(pageText);
        }

        var metadata = new Dictionary<string, string>();
        if (document.Information != null)
        {
            if (!string.IsNullOrEmpty(document.Information.Title))
                metadata["title"] = CleanText(document.Information.Title);
            if (!string.IsNullOrEmpty(document.Information.Author))
                metadata["author"] = CleanText(document.Information.Author);
            if (!string.IsNullOrEmpty(document.Information.Subject))
                metadata["subject"] = CleanText(document.Information.Subject);
        }

        return new DocumentParseResult
        {
            Success = true,
            Content = fullText.ToString(),
            Pages = pages,
            Metadata = metadata.Count > 0 ? metadata : null
        };
    }

    /// <summary>
    /// 解析DOCX文件
    /// </summary>
    private DocumentParseResult ParseDocx(Stream stream)
    {
        var fullText = new StringBuilder();

        using var document = new XWPFDocument(stream);

        // 提取段落文本
        foreach (var paragraph in document.Paragraphs)
        {
            var text = CleanText(paragraph.Text);
            if (!string.IsNullOrWhiteSpace(text))
            {
                fullText.AppendLine(text);
            }
        }

        // 提取表格文本
        foreach (var table in document.Tables)
        {
            foreach (var row in table.Rows)
            {
                var rowTexts = new List<string>();
                foreach (var cell in row.GetTableCells())
                {
                    rowTexts.Add(CleanText(cell.GetText()));
                }
                fullText.AppendLine(string.Join(" | ", rowTexts));
            }
        }

        var metadata = new Dictionary<string, string>();
        var props = document.GetProperties()?.CoreProperties;
        if (props != null)
        {
            if (!string.IsNullOrEmpty(props.Title))
                metadata["title"] = CleanText(props.Title);
            if (!string.IsNullOrEmpty(props.Creator))
                metadata["author"] = CleanText(props.Creator);
            if (!string.IsNullOrEmpty(props.Subject))
                metadata["subject"] = CleanText(props.Subject);
        }

        return new DocumentParseResult
        {
            Success = true,
            Content = fullText.ToString(),
            Metadata = metadata.Count > 0 ? metadata : null
        };
    }

    /// <summary>
    /// 解析Excel文件（xls、xlsx）
    /// </summary>
    /// <param name="stream">文件流</param>
    /// <param name="isXlsx">是否为xlsx格式</param>
    private DocumentParseResult ParseExcel(Stream stream, bool isXlsx)
    {
        var fullText = new StringBuilder();

        IWorkbook workbook = isXlsx
            ? new XSSFWorkbook(stream)
            : new HSSFWorkbook(stream);

        try
        {
            for (int sheetIndex = 0; sheetIndex < workbook.NumberOfSheets; sheetIndex++)
            {
                var sheet = workbook.GetSheetAt(sheetIndex);
                if (sheet == null) continue;

                // 添加工作表名称
                fullText.AppendLine($"=== {CleanText(sheet.SheetName)} ===");

                for (int rowIndex = 0; rowIndex <= sheet.LastRowNum; rowIndex++)
                {
                    var row = sheet.GetRow(rowIndex);
                    if (row == null) continue;

                    var cellTexts = new List<string>();
                    for (int cellIndex = 0; cellIndex < row.LastCellNum; cellIndex++)
                    {
                        var cell = row.GetCell(cellIndex);
                        var cellValue = CleanText(GetCellValue(cell));
                        cellTexts.Add(cellValue);
                    }

                    var rowText = string.Join(" | ", cellTexts);
                    if (!string.IsNullOrWhiteSpace(rowText))
                    {
                        fullText.AppendLine(rowText);
                    }
                }

                fullText.AppendLine();  // 工作表之间空一行
            }

            return new DocumentParseResult
            {
                Success = true,
                Content = fullText.ToString()
            };
        }
        finally
        {
            workbook.Close();
        }
    }

    /// <summary>
    /// 获取单元格值的字符串表示
    /// </summary>
    private static string GetCellValue(NPOI.SS.UserModel.ICell? cell)
    {
        if (cell == null) return string.Empty;

        return cell.CellType switch
        {
            CellType.String => cell.StringCellValue ?? string.Empty,
            CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                ? cell.DateCellValue?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty
                : cell.NumericCellValue.ToString(),
            CellType.Boolean => cell.BooleanCellValue.ToString(),
            CellType.Formula => GetFormulaCellValue(cell),
            CellType.Error => string.Empty,
            CellType.Blank => string.Empty,
            _ => cell.ToString() ?? string.Empty
        };
    }

    /// <summary>
    /// 获取公式单元格的计算值
    /// </summary>
    private static string GetFormulaCellValue(NPOI.SS.UserModel.ICell cell)
    {
        try
        {
            return cell.CachedFormulaResultType switch
            {
                CellType.String => cell.StringCellValue ?? string.Empty,
                CellType.Numeric => cell.NumericCellValue.ToString(),
                CellType.Boolean => cell.BooleanCellValue.ToString(),
                _ => cell.ToString() ?? string.Empty
            };
        }
        catch
        {
            return cell.CellFormula;
        }
    }

    /// <summary>
    /// 清理文本内容，移除无效的UTF-8字符和特殊字符
    /// 防止PostgreSQL存储时出现编码错误
    /// </summary>
    private static string CleanText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        // 移除NULL字符和其他控制字符（保留换行、制表符）
        var cleaned = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            // 保留可打印字符、换行符、制表符
            if (c == '\n' || c == '\r' || c == '\t' || !char.IsControl(c))
            {
                // 过滤替换字符(U+FFFD)和其他问题字符
                if (c != '\uFFFD' && c != '\0')
                {
                    cleaned.Append(c);
                }
            }
        }

        return cleaned.ToString();
    }
}


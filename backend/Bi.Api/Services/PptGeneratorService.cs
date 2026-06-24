#nullable disable
using Bi.Api.Models;
using ShapeCrawler;

namespace Bi.Api.Services;

/// <summary>
/// 使用ShapeCrawler生成PPT的服务
/// </summary>
public class PptGeneratorService
{
    private readonly ILogger<PptGeneratorService> _logger;

    // 模板颜色配置（与前端PptGenerator.vue保持一致）
    private static readonly Dictionary<string, TemplateColors> TemplateColorMap = new()
    {
        // 商务蓝：深蓝背景，白色文字
        ["business"] = new TemplateColors
        {
            BackgroundColor = "003366",  // 深蓝色
            TitleColor = "FFFFFF",       // 白色
            TextColor = "E8E8E8",        // 浅灰色
            AccentColor = "006699"       // 亮蓝色
        },
        // 医疗绿：深绿背景，白色文字
        ["medical"] = new TemplateColors
        {
            BackgroundColor = "006633",  // 深绿色
            TitleColor = "FFFFFF",       // 白色
            TextColor = "E8F5E8",        // 浅绿色
            AccentColor = "009966"       // 亮绿色
        },
        // 简约白：白色背景，深色文字
        ["simple"] = new TemplateColors
        {
            BackgroundColor = "F8F8F8",  // 浅灰白
            TitleColor = "333333",       // 深灰色
            TextColor = "666666",        // 中灰色
            AccentColor = "0066CC"       // 蓝色
        },
        // 科技紫：深紫背景，现代风格
        ["tech"] = new TemplateColors
        {
            BackgroundColor = "2D1B4E",  // 深紫色
            TitleColor = "FFFFFF",       // 白色
            TextColor = "E8E0F0",        // 浅紫色
            AccentColor = "9B59B6"       // 亮紫色
        },
        // 暖橙：温暖橙色系，活力风格
        ["warm"] = new TemplateColors
        {
            BackgroundColor = "8B4513",  // 深棕橙
            TitleColor = "FFFFFF",       // 白色
            TextColor = "FFF0E0",        // 浅橙色
            AccentColor = "FF8C00"       // 橙色
        },
        // 深灰：专业深灰风格
        ["dark"] = new TemplateColors
        {
            BackgroundColor = "2C3E50",  // 深灰蓝
            TitleColor = "FFFFFF",       // 白色
            TextColor = "BDC3C7",        // 浅灰色
            AccentColor = "3498DB"       // 蓝色
        }
    };

    public PptGeneratorService(ILogger<PptGeneratorService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 生成PPTX文件
    /// </summary>
    public byte[] GeneratePptx(PptOutlineResponse outline, string template, string? pptTitle = null)
    {
        // 获取模板颜色配置
        var colors = TemplateColorMap.GetValueOrDefault(template) ?? TemplateColorMap["business"];

        // 创建新的演示文稿（使用初始化器创建一张空幻灯片）
        var pres = new Presentation(p => p.Slide());

        // 设置幻灯片尺寸为16:9
        pres.SlideWidth = 914;
        pres.SlideHeight = 514;

        int slideIndex = 1;
        foreach (var slideData in outline.Slides.OrderBy(s => s.Order))
        {
            // 如果不是第一张幻灯片，添加新幻灯片
            if (slideIndex > 1)
            {
                // ShapeCrawler使用复制的方式添加新幻灯片
                pres.Slides.Add(pres.Slide(1));
            }

            // 根据类型创建不同内容
            switch (slideData.Type)
            {
                case "title":
                    CreateTitleSlide(pres, slideIndex, slideData, colors, pptTitle);
                    break;
                case "chart":
                    CreateChartSlide(pres, slideIndex, slideData, colors);
                    break;
                case "kpi":
                    CreateKpiSlide(pres, slideIndex, slideData, colors);
                    break;
                case "summary":
                    CreateSummarySlide(pres, slideIndex, slideData, colors);
                    break;
                default:
                    CreateContentSlide(pres, slideIndex, slideData, colors);
                    break;
            }
            slideIndex++;
        }

        // 导出为字节数组
        using var ms = new MemoryStream();
        pres.Save(ms);
        ms.Position = 0;
        return ms.ToArray();
    }

    /// <summary>
    /// 模板颜色配置类
    /// </summary>
    private class TemplateColors
    {
        public string BackgroundColor { get; set; }
        public string TitleColor { get; set; }
        public string TextColor { get; set; }
        public string AccentColor { get; set; }
    }

    /// <summary>
    /// 添加背景矩形（用于设置幻灯片背景色）
    /// </summary>
    private void AddBackgroundRectangle(Presentation pres, int slideIndex, TemplateColors colors)
    {
        var slide = pres.Slide(slideIndex);
        var shapes = slide.Shapes;

        // 添加一个覆盖整个幻灯片的矩形作为背景
        shapes.AddShape(x: 0, y: 0, width: 914, height: 514);
        var bgShape = shapes.Last();

        // 设置背景色
        try
        {
            bgShape.Fill.SetColor(colors.BackgroundColor);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "设置背景色失败，使用默认样式");
        }

        // 清除文本框
        if (bgShape.TextBox != null)
        {
            bgShape.TextBox.SetText("");
        }
    }

    /// <summary>
    /// 清除幻灯片上的所有形状（用于复制的幻灯片）
    /// </summary>
    private void ClearSlideShapes(Presentation pres, int slideIndex)
    {
        var slide = pres.Slide(slideIndex);
        // 获取所有形状并删除
        while (slide.Shapes.Count > 0)
        {
            slide.Shapes.First().Remove();
        }
    }

    /// <summary>
    /// 创建封面页 - 根据布局模板应用不同样式
    /// 支持布局：centered-title(居中封面), left-title(左对齐封面)
    /// </summary>
    private void CreateTitleSlide(Presentation pres, int slideIndex, PptSlide data, TemplateColors colors, string? pptTitle = null)
    {
        ClearSlideShapes(pres, slideIndex);
        AddBackgroundRectangle(pres, slideIndex, colors);

        var slide = pres.Slide(slideIndex);
        var shapes = slide.Shapes;
        var layout = data.Layout ?? "centered-title";

        // 主标题使用pptTitle（前端传入的PPT标题），副标题使用slide.Title
        var mainTitle = !string.IsNullOrEmpty(pptTitle) ? pptTitle : data.Title;
        var subTitle = !string.IsNullOrEmpty(pptTitle) ? data.Title : (data.Points.Count > 0 ? data.Points[0] : null);

        if (layout == "left-title")
        {
            // 左对齐封面布局
            shapes.AddShape(x: 50, y: 150, width: 700, height: 80);
            var titleShape = shapes.Last();
            if (titleShape.TextBox != null)
            {
                titleShape.TextBox.SetText(mainTitle);
                if (titleShape.TextBox.Paragraphs.Count > 0 && titleShape.TextBox.Paragraphs[0].Portions.Count > 0)
                {
                    var titlePara = titleShape.TextBox.Paragraphs[0];
                    titlePara.Portions[0].Font.Size = 40;
                    titlePara.Portions[0].Font.IsBold = true;
                    titlePara.HorizontalAlignment = TextHorizontalAlignment.Left;
                }
            }
            try { titleShape.Fill.SetNoFill(); } catch { }

            if (!string.IsNullOrEmpty(subTitle))
            {
                shapes.AddShape(x: 50, y: 250, width: 600, height: 50);
                var subtitleShape = shapes.Last();
                if (subtitleShape.TextBox != null)
                {
                    subtitleShape.TextBox.SetText(subTitle);
                    if (subtitleShape.TextBox.Paragraphs.Count > 0 && subtitleShape.TextBox.Paragraphs[0].Portions.Count > 0)
                    {
                        var subPara = subtitleShape.TextBox.Paragraphs[0];
                        subPara.Portions[0].Font.Size = 18;
                        subPara.HorizontalAlignment = TextHorizontalAlignment.Left;
                    }
                }
                try { subtitleShape.Fill.SetNoFill(); } catch { }
            }

            shapes.AddShape(x: 50, y: 320, width: 150, height: 4);
            var lineShape = shapes.Last();
            try { lineShape.Fill.SetColor(colors.AccentColor); } catch { }
            if (lineShape.TextBox != null) lineShape.TextBox.SetText("");
        }
        else
        {
            // 居中封面布局
            shapes.AddShape(x: 50, y: 180, width: 814, height: 80);
            var titleShape = shapes.Last();
            if (titleShape.TextBox != null)
            {
                titleShape.TextBox.SetText(mainTitle);
                if (titleShape.TextBox.Paragraphs.Count > 0 && titleShape.TextBox.Paragraphs[0].Portions.Count > 0)
                {
                    var titlePara = titleShape.TextBox.Paragraphs[0];
                    titlePara.Portions[0].Font.Size = 44;
                    titlePara.Portions[0].Font.IsBold = true;
                    titlePara.HorizontalAlignment = TextHorizontalAlignment.Center;
                }
            }
            try { titleShape.Fill.SetNoFill(); } catch { }

            if (!string.IsNullOrEmpty(subTitle))
            {
                shapes.AddShape(x: 100, y: 280, width: 714, height: 50);
                var subtitleShape = shapes.Last();
                if (subtitleShape.TextBox != null)
                {
                    subtitleShape.TextBox.SetText(subTitle);
                    if (subtitleShape.TextBox.Paragraphs.Count > 0 && subtitleShape.TextBox.Paragraphs[0].Portions.Count > 0)
                    {
                        var subPara = subtitleShape.TextBox.Paragraphs[0];
                        subPara.Portions[0].Font.Size = 20;
                        subPara.HorizontalAlignment = TextHorizontalAlignment.Center;
                    }
                }
                try { subtitleShape.Fill.SetNoFill(); } catch { }
            }
        }

        _logger.LogInformation("创建封面页: Layout={Layout}, MainTitle={Title}", layout, mainTitle);
    }

    /// <summary>
    /// 创建内容页 - 根据布局模板应用不同样式
    /// 支持布局：bullets-left(左侧要点), two-column(双栏布局), bullets-centered(居中要点)
    /// </summary>
    private void CreateContentSlide(Presentation pres, int slideIndex, PptSlide data, TemplateColors colors)
    {
        ClearSlideShapes(pres, slideIndex);
        AddBackgroundRectangle(pres, slideIndex, colors);

        var slide = pres.Slide(slideIndex);
        var shapes = slide.Shapes;
        var layout = data.Layout ?? "bullets-left";

        // 标题
        shapes.AddShape(x: 30, y: 20, width: 854, height: 60);
        var titleShape = shapes.Last();
        SetTextWithFormat(titleShape, data.Title, 32, true, TextHorizontalAlignment.Left);
        try { titleShape.Fill.SetNoFill(); } catch { }

        if (data.Points.Count > 0)
        {
            if (layout == "two-column")
            {
                var halfCount = (data.Points.Count + 1) / 2;
                var leftPoints = data.Points.Take(halfCount).ToList();
                var rightPoints = data.Points.Skip(halfCount).ToList();

                shapes.AddShape(x: 30, y: 100, width: 400, height: 380);
                var leftShape = shapes.Last();
                SetBulletPoints(leftShape, leftPoints, 18, TextHorizontalAlignment.Left);
                try { leftShape.Fill.SetNoFill(); } catch { }

                if (rightPoints.Count > 0)
                {
                    shapes.AddShape(x: 460, y: 100, width: 400, height: 380);
                    var rightShape = shapes.Last();
                    SetBulletPoints(rightShape, rightPoints, 18, TextHorizontalAlignment.Left);
                    try { rightShape.Fill.SetNoFill(); } catch { }
                }
            }
            else if (layout == "bullets-centered")
            {
                shapes.AddShape(x: 100, y: 120, width: 714, height: 360);
                var contentShape = shapes.Last();
                SetBulletPoints(contentShape, data.Points, 20, TextHorizontalAlignment.Center);
                try { contentShape.Fill.SetNoFill(); } catch { }
            }
            else
            {
                shapes.AddShape(x: 50, y: 100, width: 814, height: 380);
                var contentShape = shapes.Last();
                SetBulletPoints(contentShape, data.Points, 20, TextHorizontalAlignment.Left);
                try { contentShape.Fill.SetNoFill(); } catch { }
            }
        }

        _logger.LogInformation("创建内容页: Layout={Layout}, Title={Title}", layout, data.Title);
    }

    /// <summary>
    /// 创建图表页 - 根据布局模板应用不同样式
    /// 支持布局：full-image(全幅图表), image-left-text-right(左图右文),
    ///          image-right-text-left(左文右图), image-top-text-bottom(上图下文)
    /// </summary>
    private void CreateChartSlide(Presentation pres, int slideIndex, PptSlide data, TemplateColors colors)
    {
        ClearSlideShapes(pres, slideIndex);
        AddBackgroundRectangle(pres, slideIndex, colors);

        var slide = pres.Slide(slideIndex);
        var shapes = slide.Shapes;
        var layout = data.Layout ?? "full-image";

        // 标题
        shapes.AddShape(x: 30, y: 20, width: 854, height: 50);
        var titleShape = shapes.Last();
        SetTextWithFormat(titleShape, data.Title, 28, true, TextHorizontalAlignment.Left);
        try { titleShape.Fill.SetNoFill(); } catch { }

        int imgX, imgY, imgWidth, imgHeight;
        int textX = 0, textY = 0, textWidth = 0, textHeight = 0;
        bool hasTextArea = false;

        switch (layout)
        {
            case "image-left-text-right":
                imgX = 30; imgY = 80; imgWidth = 450; imgHeight = 400;
                textX = 500; textY = 100; textWidth = 380; textHeight = 380;
                hasTextArea = true;
                break;
            case "image-right-text-left":
                imgX = 450; imgY = 80; imgWidth = 450; imgHeight = 400;
                textX = 30; textY = 100; textWidth = 400; textHeight = 380;
                hasTextArea = true;
                break;
            case "image-top-text-bottom":
                imgX = 100; imgY = 75; imgWidth = 714; imgHeight = 280;
                textX = 50; textY = 370; textWidth = 814; textHeight = 120;
                hasTextArea = true;
                break;
            default: // full-image - 要点在底部，图表在上方
                imgX = 100; imgY = 80; imgWidth = 714; imgHeight = 320;
                break;
        }

        // 嵌入图表图片（拉伸填充容器区域）
        if (!string.IsNullOrEmpty(data.ChartImageBase64))
        {
            try
            {
                var imageBytes = Convert.FromBase64String(data.ChartImageBase64);
                using var imageStream = new MemoryStream(imageBytes);
                shapes.AddPicture(imageStream);
                var picture = shapes.Last();
                picture.X = imgX;
                picture.Y = imgY;
                picture.Width = imgWidth;
                picture.Height = imgHeight;
                _logger.LogInformation("嵌入图表图片: Layout={Layout}, Title={Title}, Size={W}x{H}", layout, data.Title, imgWidth, imgHeight);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "嵌入图表图片失败: {Title}", data.Title);
                AddChartPlaceholder(pres, slideIndex, colors, imgX, imgY, imgWidth, imgHeight);
            }
        }
        else
        {
            AddChartPlaceholder(pres, slideIndex, colors, imgX, imgY, imgWidth, imgHeight);
        }

        // 要点文字（在图片之后添加，位于图表下方或侧边）
        if (hasTextArea && data.Points.Count > 0)
        {
            shapes.AddShape(x: textX, y: textY, width: textWidth, height: textHeight);
            var pointsShape = shapes.Last();
            SetBulletPoints(pointsShape, data.Points, layout == "image-top-text-bottom" ? 14 : 16, TextHorizontalAlignment.Left);
            try { pointsShape.Fill.SetNoFill(); } catch { }
        }
        else if (!hasTextArea && data.Points.Count > 0)
        {
            // full-image布局：要点在图表下方
            shapes.AddShape(x: 30, y: 410, width: 854, height: 90);
            var pointsShape = shapes.Last();
            SetBulletPoints(pointsShape, data.Points, 14, TextHorizontalAlignment.Left);
            try { pointsShape.Fill.SetNoFill(); } catch { }
        }
    }

    /// <summary>
    /// 添加图表占位符（支持自定义位置）
    /// </summary>
    private void AddChartPlaceholder(Presentation pres, int slideIndex, TemplateColors colors,
        int x = 150, int y = 150, int width = 614, int height = 300)
    {
        var slide = pres.Slide(slideIndex);
        var shapes = slide.Shapes;
        shapes.AddShape(x: x, y: y, width: width, height: height);
        var placeholder = shapes.Last();
        if (placeholder.TextBox != null)
        {
            placeholder.TextBox.SetText("📊 图表区域\n\n请在PowerPoint中插入图表\n或联系管理员确认图表截图是否已保存");
            if (placeholder.TextBox.Paragraphs.Count > 0 && placeholder.TextBox.Paragraphs[0].Portions.Count > 0)
            {
                var para = placeholder.TextBox.Paragraphs[0];
                para.Portions[0].Font.Size = 16;
                para.HorizontalAlignment = TextHorizontalAlignment.Center;
            }
        }
        try { placeholder.Fill.SetNoFill(); } catch { }
    }

    /// <summary>
    /// 创建KPI指标页 - 根据布局模板应用不同样式
    /// 支持布局：three-kpi(三指标卡片), four-kpi(四指标2x2网格), kpi-with-chart(指标+图表)
    /// </summary>
    private void CreateKpiSlide(Presentation pres, int slideIndex, PptSlide data, TemplateColors colors)
    {
        // 清除复制来的形状
        ClearSlideShapes(pres, slideIndex);

        // 添加背景
        AddBackgroundRectangle(pres, slideIndex, colors);

        var slide = pres.Slide(slideIndex);
        var shapes = slide.Shapes;
        var layout = data.Layout ?? "three-kpi";

        // 标题
        shapes.AddShape(x: 30, y: 20, width: 854, height: 50);
        var titleShape = shapes.Last();
        if (titleShape.TextBox != null)
        {
            titleShape.TextBox.SetText(data.Title);
            if (titleShape.TextBox.Paragraphs.Count > 0 && titleShape.TextBox.Paragraphs[0].Portions.Count > 0)
            {
                var titlePara = titleShape.TextBox.Paragraphs[0];
                titlePara.Portions[0].Font.Size = 28;
                titlePara.Portions[0].Font.IsBold = true;
            }
        }
        try { titleShape.Fill.SetNoFill(); } catch { }

        // KPI卡片区域：优先显示图表图片（KPI值可能为N/A），其次显示卡片
        bool hasChartImage = !string.IsNullOrEmpty(data.ChartImageBase64);
        bool hasKpiData = data.KpiCards != null && data.KpiCards.Count > 0;
        // 判断KPI数据是否有效（不全为N/A）
        bool hasValidKpi = hasKpiData && data.KpiCards.Any(c => c.Value != "N/A" && c.Value != "查询失败");

        if (hasChartImage)
        {
            // 有图表图片时优先显示图片（拉伸填充）
            try
            {
                var imageBytes = Convert.FromBase64String(data.ChartImageBase64);
                using var imageStream = new MemoryStream(imageBytes);
                shapes.AddPicture(imageStream);
                var picture = shapes.Last();
                picture.X = 50;
                picture.Y = 80;
                picture.Width = 814;
                picture.Height = 370;
                _logger.LogInformation("KPI页嵌入图表图片: Title={Title}", data.Title);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "KPI页嵌入图片失败: {Title}", data.Title);
            }
        }
        else if (hasValidKpi)
        {
            if (layout == "four-kpi")
            {
                CreateKpiGridLayout(slide, data.KpiCards, colors, 2, 2, 80, 100, 350, 340);
            }
            else if (layout == "kpi-with-chart")
            {
                CreateKpiRowLayout(slide, data.KpiCards, colors, Math.Min(data.KpiCards.Count, 4), 30, 80, 180);
            }
            else
            {
                var cardCount = Math.Min(data.KpiCards.Count, 4);
                CreateKpiRowLayout(slide, data.KpiCards, colors, cardCount, 30, 100, 160);

                if (data.KpiCards.Count > 4)
                {
                    var secondRowCards = data.KpiCards.Skip(4).Take(4).ToList();
                    CreateKpiRowLayout(slide, secondRowCards, colors, secondRowCards.Count, 30, 290, 160);
                }
            }
        }
        else
        {
            shapes.AddShape(x: 150, y: 150, width: 614, height: 200);
            var placeholder = shapes.Last();
            if (placeholder.TextBox != null)
            {
                placeholder.TextBox.SetText("📊 KPI指标区域\n\n暂无指标数据");
                if (placeholder.TextBox.Paragraphs.Count > 0 && placeholder.TextBox.Paragraphs[0].Portions.Count > 0)
                {
                    var para = placeholder.TextBox.Paragraphs[0];
                    para.Portions[0].Font.Size = 16;
                    para.HorizontalAlignment = TextHorizontalAlignment.Center;
                }
            }
            try { placeholder.Fill.SetNoFill(); } catch { }
        }

        // 要点说明（放在底部）
        if (data.Points.Count > 0)
        {
            var pointsText = string.Join("  |  ", data.Points);
            shapes.AddShape(x: 30, y: 460, width: 854, height: 40);
            var pointsShape = shapes.Last();
            if (pointsShape.TextBox != null)
            {
                pointsShape.TextBox.SetText(pointsText);
                if (pointsShape.TextBox.Paragraphs.Count > 0 && pointsShape.TextBox.Paragraphs[0].Portions.Count > 0)
                {
                    var para = pointsShape.TextBox.Paragraphs[0];
                    para.Portions[0].Font.Size = 12;
                    para.HorizontalAlignment = TextHorizontalAlignment.Center;
                }
            }
            try { pointsShape.Fill.SetNoFill(); } catch { }
        }

        _logger.LogInformation("创建KPI页: Layout={Layout}, CardCount={CardCount}", layout, data.KpiCards?.Count ?? 0);
    }

    /// <summary>
    /// 创建KPI卡片横排布局
    /// </summary>
    private void CreateKpiRowLayout(IUserSlide slide, List<KpiCardData> cards, TemplateColors colors,
        int count, int startX, int startY, int cardHeight)
    {
        var cardWidth = (854 - 60 - (count - 1) * 20) / count;

        for (int i = 0; i < Math.Min(cards.Count, count); i++)
        {
            var kpi = cards[i];
            var cardX = startX + i * (cardWidth + 20);
            CreateKpiCard(slide, kpi, colors, cardX, startY, cardWidth, cardHeight);
        }
    }

    /// <summary>
    /// 创建KPI卡片网格布局
    /// </summary>
    private void CreateKpiGridLayout(IUserSlide slide, List<KpiCardData> cards, TemplateColors colors,
        int cols, int rows, int startX, int startY, int totalWidth, int totalHeight)
    {
        var gap = 20;
        var cardWidth = (totalWidth - (cols - 1) * gap) / cols;
        var cardHeight = (totalHeight - (rows - 1) * gap) / rows;

        for (int i = 0; i < Math.Min(cards.Count, cols * rows); i++)
        {
            var row = i / cols;
            var col = i % cols;
            var kpi = cards[i];
            var cardX = startX + col * (cardWidth + gap);
            var cardY = startY + row * (cardHeight + gap);
            CreateKpiCard(slide, kpi, colors, cardX, cardY, cardWidth, cardHeight);
        }
    }

    /// <summary>
    /// 创建单个KPI卡片
    /// </summary>
    private void CreateKpiCard(IUserSlide slide, KpiCardData kpi, TemplateColors colors,
        int x, int y, int width, int height)
    {
        var shapes = slide.Shapes;
        // 卡片文本：标题 -> 数值 -> 变化信息（多段落）
        var valueText = $"{kpi.Value}{(string.IsNullOrEmpty(kpi.Unit) ? "" : " " + kpi.Unit)}";
        var changeInfo = "";
        if (!string.IsNullOrEmpty(kpi.YoyChange))
            changeInfo += $"同比: {kpi.YoyChange}";
        if (!string.IsNullOrEmpty(kpi.MomChange))
            changeInfo += (string.IsNullOrEmpty(changeInfo) ? "" : "  ") + $"环比: {kpi.MomChange}";

        shapes.AddShape(x: x, y: y, width: width, height: height);
        var cardShape = shapes.Last();
        try { cardShape.Fill.SetColor(colors.AccentColor); } catch { }

        if (cardShape.TextBox != null)
        {
            // 第一段：标题
            cardShape.TextBox.SetText(kpi.Title);
            if (cardShape.TextBox.Paragraphs.Count > 0 && cardShape.TextBox.Paragraphs[0].Portions.Count > 0)
            {
                cardShape.TextBox.Paragraphs[0].Portions[0].Font.Size = 12;
                cardShape.TextBox.Paragraphs[0].HorizontalAlignment = TextHorizontalAlignment.Center;
            }

            // 第二段：数值（大字体）
            try
            {
                cardShape.TextBox.Paragraphs.Add();
                var valuePara = cardShape.TextBox.Paragraphs[1];
                valuePara.Text = valueText;
                if (valuePara.Portions.Count > 0)
                {
                    valuePara.Portions[0].Font.Size = 24;
                    valuePara.Portions[0].Font.IsBold = true;
                }
                valuePara.HorizontalAlignment = TextHorizontalAlignment.Center;
            }
            catch { }

            // 第三段：同比/环比（如果有）
            if (!string.IsNullOrEmpty(changeInfo))
            {
                try
                {
                    cardShape.TextBox.Paragraphs.Add();
                    var changePara = cardShape.TextBox.Paragraphs[2];
                    changePara.Text = changeInfo;
                    if (changePara.Portions.Count > 0)
                    {
                        changePara.Portions[0].Font.Size = 10;
                    }
                    changePara.HorizontalAlignment = TextHorizontalAlignment.Center;
                }
                catch { }
            }
        }
    }

    /// <summary>
    /// 创建总结页 - 根据布局模板应用不同样式
    /// 支持布局：summary-points(总结要点), summary-centered(居中总结)
    /// </summary>
    private void CreateSummarySlide(Presentation pres, int slideIndex, PptSlide data, TemplateColors colors)
    {
        // 清除复制来的形状
        ClearSlideShapes(pres, slideIndex);
        AddBackgroundRectangle(pres, slideIndex, colors);

        var slide = pres.Slide(slideIndex);
        var shapes = slide.Shapes;
        var layout = data.Layout ?? "summary-points";

        // 标题
        shapes.AddShape(x: 30, y: 30, width: 854, height: 70);
        var titleShape = shapes.Last();
        SetTextWithFormat(titleShape, data.Title, 36, true, TextHorizontalAlignment.Center);
        try { titleShape.Fill.SetNoFill(); } catch { }

        if (layout == "summary-centered")
        {
            // 居中总结 - 每个要点独立段落
            shapes.AddShape(x: 100, y: 150, width: 714, height: 300);
            var contentShape = shapes.Last();
            SetBulletPoints(contentShape, data.Points, 24, TextHorizontalAlignment.Center);
            try { contentShape.Fill.SetNoFill(); } catch { }
        }
        else
        {
            // 总结要点布局（带✓符号）
            shapes.AddShape(x: 80, y: 130, width: 754, height: 350);
            var contentShape = shapes.Last();
            if (contentShape.TextBox != null && data.Points.Count > 0)
            {
                contentShape.TextBox.SetText($"✓  {data.Points[0]}");
                if (contentShape.TextBox.Paragraphs.Count > 0 && contentShape.TextBox.Paragraphs[0].Portions.Count > 0)
                {
                    contentShape.TextBox.Paragraphs[0].Portions[0].Font.Size = 22;
                    contentShape.TextBox.Paragraphs[0].HorizontalAlignment = TextHorizontalAlignment.Left;
                }
                for (int i = 1; i < data.Points.Count; i++)
                {
                    try
                    {
                        contentShape.TextBox.Paragraphs.Add();
                        var para = contentShape.TextBox.Paragraphs[i];
                        para.Text = $"✓  {data.Points[i]}";
                        if (para.Portions.Count > 0) para.Portions[0].Font.Size = 22;
                        para.HorizontalAlignment = TextHorizontalAlignment.Left;
                    }
                    catch { break; }
                }
            }
            try { contentShape.Fill.SetNoFill(); } catch { }
        }

        _logger.LogInformation("创建总结页: Layout={Layout}, Title={Title}", layout, data.Title);
    }

    /// <summary>
    /// 设置形状文本和格式
    /// </summary>
    private void SetTextWithFormat(IShape shape, string text, int fontSize, bool bold, TextHorizontalAlignment alignment)
    {
        if (shape.TextBox == null) return;
        shape.TextBox.SetText(text);
        if (shape.TextBox.Paragraphs.Count > 0 && shape.TextBox.Paragraphs[0].Portions.Count > 0)
        {
            var para = shape.TextBox.Paragraphs[0];
            para.Portions[0].Font.Size = fontSize;
            para.Portions[0].Font.IsBold = bold;
            para.HorizontalAlignment = alignment;
        }
    }

    /// <summary>
    /// 设置要点列表（多段落渲染，每个要点独立一段）
    /// </summary>
    private void SetBulletPoints(IShape shape, List<string> points, int fontSize, TextHorizontalAlignment alignment)
    {
        if (shape.TextBox == null || points.Count == 0) return;

        // 第一段用SetText设置
        shape.TextBox.SetText($"●  {points[0]}");
        if (shape.TextBox.Paragraphs.Count > 0 && shape.TextBox.Paragraphs[0].Portions.Count > 0)
        {
            var para = shape.TextBox.Paragraphs[0];
            para.Portions[0].Font.Size = fontSize;
            para.HorizontalAlignment = alignment;
        }

        // 后续段落通过Paragraphs.Add添加
        for (int i = 1; i < points.Count; i++)
        {
            try
            {
                shape.TextBox.Paragraphs.Add();
                var para = shape.TextBox.Paragraphs[i];
                para.Text = $"●  {points[i]}";
                if (para.Portions.Count > 0)
                {
                    para.Portions[0].Font.Size = fontSize;
                }
                para.HorizontalAlignment = alignment;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "添加段落失败，回退到单文本模式");
                // 回退：拼接已有文本
                try
                {
                    var existingText = shape.TextBox.Paragraphs[0].Text;
                    for (int j = i; j < points.Count; j++)
                    {
                        existingText += $"\n●  {points[j]}";
                    }
                    shape.TextBox.SetText(existingText);
                    if (shape.TextBox.Paragraphs.Count > 0 && shape.TextBox.Paragraphs[0].Portions.Count > 0)
                    {
                        shape.TextBox.Paragraphs[0].Portions[0].Font.Size = fontSize;
                        shape.TextBox.Paragraphs[0].HorizontalAlignment = alignment;
                    }
                }
                catch { }
                break;
            }
        }
    }

}


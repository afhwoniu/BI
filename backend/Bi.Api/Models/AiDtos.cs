namespace Bi.Api.Models;

/// <summary>
/// AI对话请求
/// </summary>
public class AiChatRequest
{
    /// <summary>
    /// 用户输入的问题
    /// </summary>
    public string Question { get; set; } = string.Empty;
    
    /// <summary>
    /// 数据源ID（必填）
    /// </summary>
    public long DatasourceId { get; set; }
    
    /// <summary>
    /// 指定表名列表（可选，为空则使用所有表）
    /// </summary>
    public List<string>? TableNames { get; set; }
    
    /// <summary>
    /// 会话ID（用于多轮对话）
    /// </summary>
    public string? SessionId { get; set; }
    
    /// <summary>
    /// 是否流式返回
    /// </summary>
    public bool Stream { get; set; } = false;
}

/// <summary>
/// AI对话响应
/// </summary>
public class AiChatResponse
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 消息ID（用于下钻时引用）
    /// </summary>
    public long MessageId { get; set; }

    /// <summary>
    /// 对话模式：bi-指标统计, hz360-患者360, internetsearch-通用问答, report-智能报表
    /// </summary>
    public string Mode { get; set; } = "bi";

    /// <summary>
    /// 模式分类理由（调试用）
    /// </summary>
    public string? ModeReason { get; set; }

    /// <summary>
    /// AI生成的SQL（单查询模式，兼容旧版）
    /// </summary>
    public string? Sql { get; set; }

    /// <summary>
    /// AI的文字回复
    /// </summary>
    public string? Answer { get; set; }

    /// <summary>
    /// 患者360查询结果（仅hz360模式）
    /// </summary>
    public List<Patient360Info>? Patients { get; set; }

    /// <summary>
    /// 明细SQL（核心！用于下钻聚合）
    /// </summary>
    public string? DetailSql { get; set; }

    /// <summary>
    /// 医院字段名（用于医共体筛选）
    /// </summary>
    public string? HospitalField { get; set; }

    /// <summary>
    /// 日期字段名（用于同比环比计算和时间参数替换）
    /// </summary>
    public string? DateField { get; set; }

    /// <summary>
    /// 可用维度字段列表
    /// </summary>
    public List<string>? Dimensions { get; set; }

    /// <summary>
    /// 可用度量字段列表
    /// </summary>
    public List<MeasureField>? Measures { get; set; }

    /// <summary>
    /// 医院列表（从明细数据中获取）
    /// </summary>
    public List<string>? Hospitals { get; set; }

    /// <summary>
    /// 推荐的图表类型（单查询模式）
    /// </summary>
    public string? ChartType { get; set; }

    /// <summary>
    /// 图表配置建议（单查询模式）
    /// </summary>
    public ChartConfigSuggestion? ChartConfig { get; set; }

    /// <summary>
    /// SQL执行结果（单查询模式）
    /// </summary>
    public List<Dictionary<string, object?>>? Data { get; set; }

    /// <summary>
    /// 多查询列表（仪表板模式：KPI + 图表）
    /// </summary>
    public List<QueryItem>? Queries { get; set; }

    /// <summary>
    /// 原始图表配置（用于刷新时重新生成SQL，不返回给前端）
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<DefaultChartConfig>? DefaultChartsConfig { get; set; }

    /// <summary>
    /// 原始KPI配置（用于刷新时重新生成SQL，不返回给前端）
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<KpiConfig>? KpiConfigs { get; set; }

    /// <summary>
    /// 报表页签列表（仅report模式）
    /// </summary>
    public List<ReportSheetData>? ReportSheets { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Token使用统计
    /// </summary>
    public int? TokensUsed { get; set; }

    /// <summary>
    /// 调试信息：发送给AI的提示词列表
    /// </summary>
    public List<PromptInfo>? Prompts { get; set; }

    /// <summary>
    /// 完整提示词（用于调试和复盘）
    /// </summary>
    public string? PromptText { get; set; }
}

/// <summary>
/// 报表页签数据（仅report模式使用）
/// </summary>
public class ReportSheetData
{
    /// <summary>
    /// 页签标题（如"门诊日报"、"费用汇总"）
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 列定义列表
    /// </summary>
    public List<ReportColumnDef> Columns { get; set; } = new();

    /// <summary>
    /// 数据行列表
    /// </summary>
    public List<Dictionary<string, object?>> Rows { get; set; } = new();

    /// <summary>
    /// 合计行（键值对，键为字段名）
    /// </summary>
    public Dictionary<string, object?>? SummaryRow { get; set; }

    /// <summary>
    /// 该页签执行的SQL（调试用）
    /// </summary>
    public string? Sql { get; set; }
}

/// <summary>
/// 报表列定义
/// </summary>
public class ReportColumnDef
{
    /// <summary>
    /// 字段名（对应数据行的键）
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// 显示标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 数据类型：text/number/date
    /// </summary>
    public string DataType { get; set; } = "text";

    /// <summary>
    /// 列宽（像素）
    /// </summary>
    public int Width { get; set; } = 120;

    /// <summary>
    /// 对齐方式：left/center/right
    /// </summary>
    public string Align { get; set; } = "left";
}

/// <summary>
/// 度量字段定义
/// </summary>
public class MeasureField
{
    /// <summary>
    /// 字段名
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// 显示别名
    /// </summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>
    /// 聚合函数（SUM/COUNT/AVG/MAX/MIN）
    /// </summary>
    public string Agg { get; set; } = "SUM";
}

/// <summary>
/// 默认图表配置（用于保存和刷新）
/// </summary>
public class DefaultChartConfig
{
    /// <summary>
    /// 图表类型（bar/line/pie）
    /// </summary>
    public string Type { get; set; } = "bar";

    /// <summary>
    /// 图表标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 分组字段（维度）
    /// </summary>
    public string GroupBy { get; set; } = string.Empty;

    /// <summary>
    /// 度量配置
    /// </summary>
    public MeasureField? Measure { get; set; }
}

/// <summary>
/// KPI配置（用于保存和刷新）
/// </summary>
public class KpiConfig
{
    /// <summary>
    /// KPI标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// SQL模板（使用(...)作为明细SQL占位符）
    /// </summary>
    public string SqlTemplate { get; set; } = string.Empty;
}

/// <summary>
/// 单个查询项（用于多查询模式）
/// </summary>
public class QueryItem
{
    /// <summary>
    /// 查询类型：kpi（指标卡片）、bar（柱状图）、line（折线图）、pie（饼图）、table（表格）
    /// </summary>
    public string Type { get; set; } = "bar";

    /// <summary>
    /// 标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// SQL查询语句
    /// </summary>
    public string Sql { get; set; } = string.Empty;

    /// <summary>
    /// KPI类型时的取值字段名
    /// </summary>
    public string? Field { get; set; }

    /// <summary>
    /// 查询执行结果
    /// </summary>
    public List<Dictionary<string, object?>>? Data { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; set; }

    #region 同比环比字段（仅KPI类型使用）

    /// <summary>
    /// 同比值（去年同期的值）
    /// </summary>
    public decimal? Yoy { get; set; }

    /// <summary>
    /// 同比增长率（正数表示增长，负数表示下降，单位：百分比）
    /// </summary>
    public decimal? YoyRate { get; set; }

    /// <summary>
    /// 环比值（上一周期的值）
    /// </summary>
    public decimal? Mom { get; set; }

    /// <summary>
    /// 环比增长率（正数表示增长，负数表示下降，单位：百分比）
    /// </summary>
    public decimal? MomRate { get; set; }

    #endregion
}

/// <summary>
/// 提示词信息（用于调试）
/// </summary>
public class PromptInfo
{
    /// <summary>
    /// 阶段名称（如"表选择"、"SQL生成"）
    /// </summary>
    public string Phase { get; set; } = string.Empty;

    /// <summary>
    /// 提示词内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// AI响应内容
    /// </summary>
    public string? Response { get; set; }
}

/// <summary>
/// 图表配置建议
/// </summary>
public class ChartConfigSuggestion
{
    /// <summary>
    /// 维度字段列表
    /// </summary>
    public List<string> Dimensions { get; set; } = new();
    
    /// <summary>
    /// 度量字段列表
    /// </summary>
    public List<MeasureSuggestion> Measures { get; set; } = new();
    
    /// <summary>
    /// 图表标题建议
    /// </summary>
    public string? Title { get; set; }
}

/// <summary>
/// 度量建议
/// </summary>
public class MeasureSuggestion
{
    /// <summary>
    /// 字段名
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// 聚合类型（sum/count/avg/max/min）
    /// </summary>
    public string AggType { get; set; } = "sum";

    /// <summary>
    /// 别名/显示名称
    /// </summary>
    public string? Alias { get; set; }
}

/// <summary>
/// 获取表列表请求
/// </summary>
public class GetTablesRequest
{
    /// <summary>
    /// 数据源ID
    /// </summary>
    public long DatasourceId { get; set; }
}

/// <summary>
/// 保存为图表请求
/// </summary>
public class SaveAsChartRequest
{
    /// <summary>
    /// 图表标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 原始问题
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// SQL语句
    /// </summary>
    public string Sql { get; set; } = string.Empty;

    /// <summary>
    /// 数据源ID
    /// </summary>
    public long DatasourceId { get; set; }

    /// <summary>
    /// 图表类型
    /// </summary>
    public string? ChartType { get; set; }

    /// <summary>
    /// 图表配置JSON
    /// </summary>
    public string? ChartConfig { get; set; }
}

/// <summary>
/// 下钻请求（基于明细SQL聚合）
/// </summary>
public class DrillRequest
{
    /// <summary>
    /// 消息ID（基于哪条消息的明细SQL）
    /// </summary>
    public long MessageId { get; set; }

    /// <summary>
    /// 数据源ID
    /// </summary>
    public long DatasourceId { get; set; }

    /// <summary>
    /// 分组维度字段
    /// </summary>
    public string GroupBy { get; set; } = string.Empty;

    /// <summary>
    /// 过滤条件
    /// </summary>
    public List<DrillFilter>? Filters { get; set; }

    /// <summary>
    /// 聚合度量
    /// </summary>
    public List<MeasureField>? Measures { get; set; }

    /// <summary>
    /// 排序方式（asc/desc）
    /// </summary>
    public string? OrderBy { get; set; }

    /// <summary>
    /// 限制条数
    /// </summary>
    public int Limit { get; set; } = 50;
}

/// <summary>
/// 下钻过滤条件
/// </summary>
public class DrillFilter
{
    /// <summary>
    /// 字段名
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// 操作符（=, !=, >, <, >=, <=, like, in）
    /// </summary>
    public string Op { get; set; } = "=";

    /// <summary>
    /// 值
    /// </summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// 下钻响应
/// </summary>
public class DrillResponse
{
    /// <summary>
    /// 查询结果数据
    /// </summary>
    public List<Dictionary<string, object?>>? Data { get; set; }

    /// <summary>
    /// 实际执行的SQL（调试用）
    /// </summary>
    public string? ExecutedSql { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// 明细查询请求（点击图表下钻到明细数据）
/// </summary>
public class DetailRequest
{
    /// <summary>
    /// 消息ID（基于哪条消息的明细SQL）
    /// </summary>
    public long MessageId { get; set; }

    /// <summary>
    /// 数据源ID
    /// </summary>
    public long DatasourceId { get; set; }

    /// <summary>
    /// 筛选条件列表（如点击某日期/科室等）
    /// </summary>
    public List<DrillFilter>? Filters { get; set; }

    /// <summary>
    /// 页码（从1开始）
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// 每页条数
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// 排序字段
    /// </summary>
    public string? OrderBy { get; set; }

    /// <summary>
    /// 时间范围开始日期（用于替换明细SQL中的时间参数）
    /// </summary>
    public string? StartDate { get; set; }

    /// <summary>
    /// 时间范围结束日期（用于替换明细SQL中的时间参数）
    /// </summary>
    public string? EndDate { get; set; }

    /// <summary>
    /// 排序方向（asc/desc）
    /// </summary>
    public string? OrderDir { get; set; } = "desc";
}

/// <summary>
/// 明细查询响应（分页）
/// </summary>
public class DetailResponse
{
    /// <summary>
    /// 明细数据
    /// </summary>
    public List<Dictionary<string, object?>>? Data { get; set; }

    /// <summary>
    /// 总记录数
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// 当前页码
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// 每页条数
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// 列信息（字段名列表）
    /// </summary>
    public List<string>? Columns { get; set; }

    /// <summary>
    /// 实际执行的SQL（调试用）
    /// </summary>
    public string? ExecutedSql { get; set; }

    /// <summary>
    /// 筛选条件描述（用于显示）
    /// </summary>
    public string? FilterDescription { get; set; }
}

/// <summary>
/// 重放历史会话请求（支持新时间范围）
/// </summary>
public class ReplayRequest
{
    /// <summary>
    /// 消息ID
    /// </summary>
    public long MessageId { get; set; }

    /// <summary>
    /// 数据源ID
    /// </summary>
    public long DatasourceId { get; set; }

    /// <summary>
    /// 新的开始日期（可选，用于替换原SQL中的时间参数）
    /// </summary>
    public string? StartDate { get; set; }

    /// <summary>
    /// 新的结束日期（可选，用于替换原SQL中的时间参数）
    /// </summary>
    public string? EndDate { get; set; }
}

/// <summary>
/// 刷新查询请求（带筛选条件重新执行KPI和图表查询）
/// </summary>
public class RefreshRequest
{
    /// <summary>
    /// 消息ID（基于哪条消息的配置）
    /// </summary>
    public long MessageId { get; set; }

    /// <summary>
    /// 数据源ID
    /// </summary>
    public long DatasourceId { get; set; }

    /// <summary>
    /// 筛选条件（如医院筛选）
    /// </summary>
    public List<DrillFilter>? Filters { get; set; }

    /// <summary>
    /// 时间范围开始日期（用于计算同比环比）
    /// </summary>
    public string? StartDate { get; set; }

    /// <summary>
    /// 时间范围结束日期（用于计算同比环比）
    /// </summary>
    public string? EndDate { get; set; }
}

/// <summary>
/// 刷新查询响应
/// </summary>
public class RefreshResponse
{
    /// <summary>
    /// KPI查询结果列表（包含数据）
    /// </summary>
    public List<QueryItem>? Queries { get; set; }

    /// <summary>
    /// 筛选条件描述
    /// </summary>
    public string? FilterDescription { get; set; }
}

/// <summary>
/// 会话列表项
/// </summary>
public class SessionListItem
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 会话Key
    /// </summary>
    public string SessionKey { get; set; } = string.Empty;

    /// <summary>
    /// 会话标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 对话模式：bi-指标统计, hz360-患者360, internetsearch-通用问答, report-智能报表
    /// </summary>
    public string Mode { get; set; } = "bi";

    /// <summary>
    /// 数据源ID
    /// </summary>
    public long? DatasourceId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 最后活动时间
    /// </summary>
    public DateTime LastActiveAt { get; set; }

    /// <summary>
    /// 最后一条Assistant消息的ID（用于重放）
    /// </summary>
    public long? LastMessageId { get; set; }

    /// <summary>
    /// 已保存图片数量（用于显示）
    /// </summary>
    public int ImageCount { get; set; }
}

/// <summary>
/// 患者360信息（用于hz360模式返回）
/// </summary>
public class Patient360Info
{
    /// <summary>
    /// 患者ID
    /// </summary>
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// 患者姓名
    /// </summary>
    public string PatientName { get; set; } = string.Empty;

    /// <summary>
    /// 性别
    /// </summary>
    public string? Gender { get; set; }

    /// <summary>
    /// 年龄
    /// </summary>
    public int? Age { get; set; }

    /// <summary>
    /// 出生日期
    /// </summary>
    public DateTime? BirthDate { get; set; }

    /// <summary>
    /// 身份证号（脱敏）
    /// </summary>
    public string? IdCard { get; set; }

    /// <summary>
    /// 联系电话（脱敏）
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// 最近就诊日期
    /// </summary>
    public DateTime? LastVisitDate { get; set; }

    /// <summary>
    /// 最近就诊科室
    /// </summary>
    public string? LastDepartment { get; set; }

    /// <summary>
    /// 最近诊断
    /// </summary>
    public string? LastDiagnosis { get; set; }

    /// <summary>
    /// 360详情链接（外部URL，可直接跳转浏览器）
    /// </summary>
    public string? DetailUrl { get; set; }
}

#region PPT生成相关DTO

/// <summary>
/// PPT大纲生成请求
/// </summary>
public class PptOutlineRequest
{
    /// <summary>
    /// 选中的会话ID列表
    /// </summary>
    public List<long> SessionIds { get; set; } = new();

    /// <summary>
    /// PPT主题/标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 用户输入的思路/要求（可选）
    /// </summary>
    public string? Idea { get; set; }

    /// <summary>
    /// 数据源ID
    /// </summary>
    public long DatasourceId { get; set; }
}

/// <summary>
/// PPT大纲优化请求
/// </summary>
public class PptOptimizeRequest
{
    /// <summary>
    /// 当前大纲
    /// </summary>
    public PptOutlineResponse Outline { get; set; } = new();

    /// <summary>
    /// 优化提示词
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// 目标幻灯片索引（可选，-1或null表示全局优化，>=0表示只优化指定幻灯片）
    /// </summary>
    public int? SlideIndex { get; set; }

    /// <summary>
    /// 优化模式：global-全局优化, single-单页优化
    /// </summary>
    public string Mode { get; set; } = "global";
}

/// <summary>
/// 常用优化指令类型
/// </summary>
public static class OptimizeCommands
{
    /// <summary>精简内容 - 减少要点数量，使内容更简洁</summary>
    public const string Simplify = "simplify";
    /// <summary>扩展内容 - 增加更多细节和要点</summary>
    public const string Expand = "expand";
    /// <summary>调整顺序 - 重新排列要点顺序使逻辑更清晰</summary>
    public const string Reorder = "reorder";
    /// <summary>更换布局 - 推荐更合适的布局</summary>
    public const string ChangeLayout = "change_layout";
    /// <summary>优化标题 - 使标题更吸引人</summary>
    public const string OptimizeTitle = "optimize_title";
    /// <summary>添加备注 - 生成演讲备注</summary>
    public const string AddNotes = "add_notes";
}

/// <summary>
/// PPT大纲响应
/// </summary>
public class PptOutlineResponse
{
    /// <summary>
    /// PPT标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 幻灯片列表
    /// </summary>
    public List<PptSlide> Slides { get; set; } = new();

    /// <summary>
    /// 发送给AI的系统提示词（用于前端展示）
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// 发送给AI的用户提示词（用于前端展示）
    /// </summary>
    public string? UserPrompt { get; set; }
}

/// <summary>
/// PPT幻灯片
/// </summary>
public class PptSlide
{
    /// <summary>
    /// 幻灯片序号
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 幻灯片标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 幻灯片类型：title-封面, content-内容页, chart-图表页, kpi-指标页, summary-总结页
    /// </summary>
    public string Type { get; set; } = "content";

    /// <summary>
    /// 要点列表（文字内容）
    /// </summary>
    public List<string> Points { get; set; } = new();

    /// <summary>
    /// 关联的消息ID（用于获取图表数据）
    /// </summary>
    public long? MessageId { get; set; }

    /// <summary>
    /// 图表配置JSON（如果是图表页）
    /// </summary>
    public string? ChartConfig { get; set; }

    /// <summary>
    /// 图表数据（如果是图表页）
    /// </summary>
    public List<Dictionary<string, object>>? ChartData { get; set; }

    /// <summary>
    /// 图表图片Base64（前端截图传入，无需data:image/png;base64,前缀）
    /// </summary>
    public string? ChartImageBase64 { get; set; }

    /// <summary>
    /// 图表截图URL列表（用于前端预览显示）
    /// </summary>
    public List<string>? ChartImageUrls { get; set; }

    /// <summary>
    /// KPI指标卡片数据列表（用于kpi类型幻灯片）
    /// </summary>
    public List<KpiCardData>? KpiCards { get; set; }

    /// <summary>
    /// 备注/讲稿
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// 布局模板ID（AI推荐或用户选择）
    /// 可选值：centered-title, left-title, bullets-left, two-column,
    /// full-image, image-left-text-right, three-kpi, four-kpi, summary-points
    /// </summary>
    public string? Layout { get; set; }
}

#region 布局模板相关

/// <summary>
/// 幻灯片布局模板定义
/// </summary>
public class SlideLayoutTemplate
{
    /// <summary>
    /// 布局ID（如 "centered-title", "two-column"）
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 布局名称（显示用）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 适用的幻灯片类型（逗号分隔，如 "title,content"）
    /// </summary>
    public string ApplicableTypes { get; set; } = string.Empty;

    /// <summary>
    /// 布局描述
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// 预设布局模板库
/// </summary>
public static class SlideLayouts
{
    /// <summary>
    /// 所有可用的布局模板
    /// </summary>
    public static readonly List<SlideLayoutTemplate> All = new()
    {
        // 封面页布局
        new SlideLayoutTemplate { Id = "centered-title", Name = "居中封面", ApplicableTypes = "title", Description = "标题居中，副标题在下方" },
        new SlideLayoutTemplate { Id = "left-title", Name = "左对齐封面", ApplicableTypes = "title", Description = "标题左对齐，现代风格" },

        // 内容页布局
        new SlideLayoutTemplate { Id = "bullets-left", Name = "左侧要点", ApplicableTypes = "content", Description = "要点列表左对齐" },
        new SlideLayoutTemplate { Id = "two-column", Name = "双栏布局", ApplicableTypes = "content", Description = "左右分栏显示要点" },
        new SlideLayoutTemplate { Id = "bullets-centered", Name = "居中要点", ApplicableTypes = "content", Description = "要点居中显示" },

        // 图表页布局
        new SlideLayoutTemplate { Id = "full-image", Name = "全幅图表", ApplicableTypes = "chart", Description = "图表占满整页" },
        new SlideLayoutTemplate { Id = "image-left-text-right", Name = "左图右文", ApplicableTypes = "chart", Description = "左侧图表，右侧说明文字" },
        new SlideLayoutTemplate { Id = "image-right-text-left", Name = "左文右图", ApplicableTypes = "chart", Description = "左侧说明文字，右侧图表" },
        new SlideLayoutTemplate { Id = "image-top-text-bottom", Name = "上图下文", ApplicableTypes = "chart", Description = "上方图表，下方说明" },

        // KPI指标页布局
        new SlideLayoutTemplate { Id = "three-kpi", Name = "三指标卡片", ApplicableTypes = "kpi", Description = "3个KPI卡片横排" },
        new SlideLayoutTemplate { Id = "four-kpi", Name = "四指标卡片", ApplicableTypes = "kpi", Description = "2x2网格布局" },
        new SlideLayoutTemplate { Id = "kpi-with-chart", Name = "指标+图表", ApplicableTypes = "kpi", Description = "上方KPI卡片，下方图表" },

        // 总结页布局
        new SlideLayoutTemplate { Id = "summary-points", Name = "总结要点", ApplicableTypes = "summary", Description = "带图标的总结列表" },
        new SlideLayoutTemplate { Id = "summary-centered", Name = "居中总结", ApplicableTypes = "summary", Description = "总结内容居中显示" }
    };

    /// <summary>
    /// 根据幻灯片类型获取可用布局
    /// </summary>
    public static List<SlideLayoutTemplate> GetByType(string slideType)
    {
        return All.Where(l => l.ApplicableTypes.Split(',').Contains(slideType)).ToList();
    }

    /// <summary>
    /// 获取类型的默认布局
    /// </summary>
    public static string GetDefaultLayout(string slideType)
    {
        return slideType switch
        {
            "title" => "centered-title",
            "content" => "bullets-left",
            "chart" => "full-image",
            "kpi" => "three-kpi",
            "summary" => "summary-points",
            _ => "bullets-left"
        };
    }
}

#endregion

/// <summary>
/// KPI指标卡片数据
/// </summary>
public class KpiCardData
{
    /// <summary>
    /// 指标标题（如"异常肾功能检验人次"）
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 指标值（如"1,234"）
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 指标单位（如"人次"、"万元"）
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// 同比变化（如"+12.5%"）
    /// </summary>
    public string? YoyChange { get; set; }

    /// <summary>
    /// 环比变化（如"-3.2%"）
    /// </summary>
    public string? MomChange { get; set; }
}

/// <summary>
/// PPT生成请求
/// </summary>
public class PptGenerateRequest
{
    /// <summary>
    /// PPT大纲
    /// </summary>
    public PptOutlineResponse Outline { get; set; } = new();

    /// <summary>
    /// 数据源ID
    /// </summary>
    public long DatasourceId { get; set; }

    /// <summary>
    /// 模板样式：business-商务蓝, medical-医疗绿, simple-简约白, tech-科技紫, warm-暖橙, dark-深灰
    /// </summary>
    public string Template { get; set; } = "business";

    /// <summary>
    /// PPT主标题（封面页使用，前端传入）
    /// </summary>
    public string? PptTitle { get; set; }

    /// <summary>
    /// 受众信息
    /// </summary>
    public string? Audience { get; set; }
}
#endregion

#region Word报告生成相关DTO

/// <summary>
/// Word报告大纲生成请求
/// </summary>
public class WordOutlineRequest
{
    /// <summary>
    /// 选中的会话ID列表
    /// </summary>
    public List<long> SessionIds { get; set; } = new();

    /// <summary>
    /// 报告标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 用户输入的要求/思路（可选）
    /// </summary>
    public string? Idea { get; set; }

    /// <summary>
    /// 数据源ID（用于获取图表数据）
    /// </summary>
    public long DatasourceId { get; set; }
}

/// <summary>
/// Word报告大纲响应
/// </summary>
public class WordOutlineResponse
{
    /// <summary>
    /// 报告标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 报告副标题（可选）
    /// </summary>
    public string? Subtitle { get; set; }

    /// <summary>
    /// 摘要
    /// </summary>
    public string? Abstract { get; set; }

    /// <summary>
    /// 章节列表
    /// </summary>
    public List<WordChapter> Chapters { get; set; } = new();

    /// <summary>
    /// 发送给AI的系统提示词（用于前端展示）
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// 发送给AI的用户提示词（用于前端展示）
    /// </summary>
    public string? UserPrompt { get; set; }
}

/// <summary>
/// Word报告章节
/// </summary>
public class WordChapter
{
    /// <summary>
    /// 章节序号
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 章节标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 章节类型：text-纯文本, table-表格, chart-图表说明, conclusion-总结
    /// </summary>
    public string Type { get; set; } = "text";

    /// <summary>
    /// 正文内容（支持Markdown格式）
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 关联的消息ID（用于获取图表/表格数据）
    /// </summary>
    public long? MessageId { get; set; }

    /// <summary>
    /// 表格数据（如果是表格类型）
    /// </summary>
    public List<Dictionary<string, object>>? TableData { get; set; }

    /// <summary>
    /// 图表图片Base64（前端截图传入，无需data:image/png;base64,前缀）
    /// </summary>
    public string? ChartImageBase64 { get; set; }

    /// <summary>
    /// 图表截图URL列表（用于前端预览显示）
    /// </summary>
    public List<string>? ChartImageUrls { get; set; }

    /// <summary>
    /// 子章节（支持嵌套）
    /// </summary>
    public List<WordChapter>? SubChapters { get; set; }
}

/// <summary>
/// Word报告生成请求
/// </summary>
public class WordGenerateRequest
{
    /// <summary>
    /// Word报告大纲
    /// </summary>
    public WordOutlineResponse Outline { get; set; } = new();

    /// <summary>
    /// 数据源ID
    /// </summary>
    public long DatasourceId { get; set; }

    /// <summary>
    /// 模板样式：formal-正式报告, simple-简约版, academic-学术版
    /// </summary>
    public string Template { get; set; } = "formal";
}

#endregion

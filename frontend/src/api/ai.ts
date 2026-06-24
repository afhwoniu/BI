import request from './request'

/** AI对话请求参数 */
export interface AiChatRequest {
  question: string
  datasourceId: number
  tableNames?: string[]
  sessionId?: string
  stream?: boolean
}

/** 度量建议 */
export interface MeasureSuggestion {
  field: string
  aggType: string
  alias?: string
}

/** 图表配置建议 */
export interface ChartConfigSuggestion {
  dimensions: string[]
  measures: MeasureSuggestion[]
  title?: string
}

/** 提示词信息（用于调试） */
export interface PromptInfo {
  phase: string  // 阶段名称
  content: string  // 提示词内容
  response?: string  // AI响应
}

/** 单个查询项（用于多查询模式） */
export interface QueryItem {
  type: 'kpi' | 'bar' | 'line' | 'pie' | 'table'  // 查询类型
  title: string  // 标题
  sql: string  // SQL语句
  field?: string  // KPI类型的取值字段
  data?: Record<string, any>[]  // 执行结果
  error?: string  // 错误信息
  // 同比环比字段（仅KPI类型使用）
  yoy?: number  // 同比值（去年同期的值）
  yoyRate?: number  // 同比增长率（百分比，正数增长，负数下降）
  mom?: number  // 环比值（上期的值）
  momRate?: number  // 环比增长率（百分比，正数增长，负数下降）
}

/** 度量字段定义 */
export interface MeasureField {
  field: string  // 字段名
  alias: string  // 显示别名
  agg: string    // 聚合函数（SUM/COUNT/AVG等）
}

/** 下钻过滤条件 */
export interface DrillFilter {
  field: string   // 字段名
  op: string      // 操作符（=, !=, like, in等）
  value: string   // 值
}

/** 下钻请求参数 */
export interface DrillRequest {
  messageId: number      // 消息ID（基于哪条消息的明细SQL）
  datasourceId: number   // 数据源ID
  groupBy: string        // 分组维度字段
  filters?: DrillFilter[]  // 过滤条件
  measures?: MeasureField[] // 聚合度量
  orderBy?: string       // 排序方式（asc/desc）
  limit?: number         // 限制条数
}

/** 下钻响应 */
export interface DrillResponse {
  data?: Record<string, any>[]  // 查询结果数据
  executedSql?: string          // 实际执行的SQL（调试用）
  error?: string                // 错误信息
}

/** 明细查询请求（点击图表下钻到明细数据） */
export interface DetailRequest {
  messageId: number      // 消息ID（基于哪条消息的明细SQL）
  datasourceId: number   // 数据源ID
  filters?: DrillFilter[]  // 过滤条件（点击的维度值）
  page?: number          // 页码（从1开始）
  pageSize?: number      // 每页条数
  orderBy?: string       // 排序字段
  orderDir?: string      // 排序方向（asc/desc）
  startDate?: string     // 时间范围开始日期（用于替换明细SQL中的时间参数）
  endDate?: string       // 时间范围结束日期（用于替换明细SQL中的时间参数）
}

/** 明细查询响应（分页） */
export interface DetailResponse {
  data?: Record<string, any>[]  // 明细数据
  total?: number         // 总记录数
  page?: number          // 当前页码
  pageSize?: number      // 每页条数
  columns?: string[]     // 列信息
  executedSql?: string   // 实际执行的SQL（调试用）
  filterDescription?: string  // 筛选条件描述
}

/** 患者360信息 */
export interface Patient360Info {
  patientId: string      // 患者ID
  patientName: string    // 患者姓名
  gender?: string        // 性别
  age?: number           // 年龄
  birthDate?: string     // 出生日期
  idCard?: string        // 身份证号（脱敏后）
  phone?: string         // 联系电话（脱敏后）
  lastVisitDate?: string // 最近就诊日期
  lastDepartment?: string // 最近就诊科室
  lastDiagnosis?: string  // 最近诊断
  detailUrl?: string      // 详情页链接
}

/** 报表列定义 */
export interface ReportColumnDef {
  field: string        // 字段名（对应数据行的键）
  title: string        // 显示标题
  dataType: string     // 数据类型：text/number/date
  width: number        // 列宽（像素）
  align: string        // 对齐方式：left/center/right
}

/** 报表页签数据 */
export interface ReportSheetData {
  title: string                           // 页签标题
  columns: ReportColumnDef[]              // 列定义
  rows: Record<string, any>[]             // 数据行
  summaryRow?: Record<string, any> | null // 合计行
  sql?: string                            // 执行的SQL（调试用）
}

/** AI对话响应 */
export interface AiChatResponse {
  sessionId: string
  messageId: number  // 消息ID（用于下钻时引用）
  mode?: string      // 模式：bi-指标统计, hz360-患者360, internetsearch-通用问答, report-智能报表
  modeReason?: string  // 模式分类理由
  sql?: string  // 单查询模式（兼容旧版）
  answer?: string
  detailSql?: string   // 明细SQL（核心！用于下钻聚合）
  hospitalField?: string  // 医院字段名（用于医共体筛选）
  dateField?: string      // 日期字段名（用于同比环比计算和时间参数替换）
  dimensions?: string[]   // 可用维度字段列表
  measures?: MeasureField[]  // 可用度量字段列表
  hospitals?: string[]   // 医院列表（从明细数据中获取）
  chartType?: string
  chartConfig?: ChartConfigSuggestion
  data?: Record<string, any>[]  // 单查询模式数据
  queries?: QueryItem[]  // 多查询模式（仪表板）
  patients?: Patient360Info[]  // 患者360查询结果（hz360模式）
  reportSheets?: ReportSheetData[]  // 报表页签列表（report模式）
  error?: string
  tokensUsed?: number
  prompts?: PromptInfo[]  // 提示词列表（调试用）
  promptText?: string     // 完整提示词（用于调试复盘）
}

/** 表信息 */
export interface TableInfo {
  tableName: string
  tableComment?: string
  columnCount: number
}

/** 列信息 */
export interface ColumnInfo {
  columnName: string
  columnComment?: string
  dataType: string
  isNullable: boolean
  isPrimaryKey: boolean
}

/** AI对话（超时时间设置为3分钟，LLM调用较慢） */
export function aiChat(data: AiChatRequest) {
  return request.post<AiChatResponse>('/ai/chat', data, { timeout: 180000 })
}

/** 保存为图表请求 */
export interface SaveAsChartRequest {
  title: string
  question: string
  sql: string
  datasourceId: number
  chartType?: string
  chartConfig?: string
}

/** 保存AI分析结果为图表（自动创建数据集+图表） */
export function saveAsChart(data: SaveAsChartRequest) {
  return request.post<number>('/ai/save-as-chart', data)
}

/** 获取数据源的表列表 */
export function getAiTables(datasourceId: number) {
  return request.get<TableInfo[]>(`/ai/tables/${datasourceId}`)
}

/** 获取表的字段列表 */
export function getAiColumns(datasourceId: number, tableName: string) {
  return request.get<ColumnInfo[]>(`/ai/columns/${datasourceId}/${tableName}`)
}

/** 下钻查询 - 基于已保存的明细SQL进行聚合分析 */
export function aiDrill(data: DrillRequest) {
  return request.post<DrillResponse>('/ai/drill', data, { timeout: 60000 })
}

/** 获取医院列表 - 基于明细SQL获取可筛选的医院 */
export function getAiHospitals(messageId: number, datasourceId: number) {
  return request.get<string[]>(`/ai/hospitals/${messageId}/${datasourceId}`)
}

/** 明细查询 - 点击图表下钻到明细数据（分页） */
export function aiDetail(data: DetailRequest) {
  return request.post<DetailResponse>('/ai/detail', data, { timeout: 60000 })
}

/** 刷新请求 - 带筛选条件重新执行KPI和图表查询 */
export interface RefreshRequest {
  messageId: number
  datasourceId: number
  filters?: DrillFilter[]
  startDate?: string  // 时间范围开始日期（用于计算同比环比）
  endDate?: string    // 时间范围结束日期（用于计算同比环比）
}

/** 刷新响应 */
export interface RefreshResponse {
  queries?: QueryItem[]
  filterDescription?: string
}

/** 刷新查询 - 带筛选条件重新执行KPI和图表查询 */
export function aiRefresh(data: RefreshRequest) {
  return request.post<RefreshResponse>('/ai/refresh', data, { timeout: 60000 })
}

/** 会话列表项 */
export interface SessionListItem {
  id: number
  sessionKey: string
  title: string
  datasourceId?: number
  createdAt: string
  lastActiveAt: string
  lastMessageId?: number
  mode?: string  // 对话模式：bi-指标统计, hz360-患者360, internetsearch-AI检索, report-智能报表
  imageCount?: number  // 已保存图片数量
}

/** 获取会话历史列表 */
export function getAiSessions(datasourceId?: number) {
  const params = datasourceId ? { datasourceId } : {}
  return request.get<SessionListItem[]>('/ai/sessions', { params })
}

/** 删除会话 */
export function deleteAiSession(id: number) {
  return request.delete<boolean>(`/ai/sessions/${id}`)
}

/** 更新会话标题 */
export function updateAiSessionTitle(id: number, title: string) {
  return request.put<boolean>(`/ai/sessions/${id}/title`, { title })
}

/** 获取会话的已保存图片列表 */
export function getSessionImages(id: number) {
  return request.get<string[]>(`/ai/sessions/${id}/images`)
}

/** 删除会话中的指定图片 */
export function deleteSessionImage(sessionId: number, imagePath: string) {
  return request.delete<boolean>(`/ai/sessions/${sessionId}/images`, {
    params: { imagePath }
  })
}

/** 重放历史会话请求参数 */
export interface ReplayRequest {
  messageId: number
  datasourceId: number
  startDate?: string  // 新的开始日期（可选，用于替换原SQL中的时间参数）
  endDate?: string    // 新的结束日期（可选，用于替换原SQL中的时间参数）
}

/** 重放历史会话 - 基于保存的配置重新执行SQL获取最新数据 */
export function aiReplay(params: ReplayRequest) {
  const { messageId, datasourceId, startDate, endDate } = params
  let url = `/ai/replay/${messageId}?datasourceId=${datasourceId}`
  if (startDate) url += `&startDate=${startDate}`
  if (endDate) url += `&endDate=${endDate}`
  return request.post<AiChatResponse>(url, {}, { timeout: 60000 })
}

/** 图表截图项（包含图片和标题） */
export interface ChartImageItem {
  image: string     // Base64图片
  title: string     // 图表标题
}

/** 上传图表截图请求 */
export interface UploadChartImagesRequest {
  messageId: number
  images: string[]  // Base64图片列表（兼容旧版）
  chartImages?: ChartImageItem[]  // 新版：带标题的图表截图列表
}

/** 上传图表截图 - 前端渲染图表后上传截图到服务器 */
export function uploadChartImages(data: UploadChartImagesRequest) {
  return request.post<string[]>('/ai/chart-images', data, { timeout: 60000 })
}

// ============ PPT生成相关接口 ============

/** PPT幻灯片类型 */
export type SlideType = 'title' | 'content' | 'chart' | 'kpi' | 'summary'

/** KPI指标卡片数据 */
export interface KpiCardData {
  title: string        // 指标标题（如"异常肾功能检验人次"）
  value: string        // 指标值（如"1,234"）
  unit?: string        // 指标单位（如"人次"、"万元"）
  yoyChange?: string   // 同比变化（如"+12.5%"）
  momChange?: string   // 环比变化（如"-3.2%"）
}

/** PPT幻灯片 */
export interface PptSlide {
  order: number        // 排序顺序
  type: SlideType      // 幻灯片类型
  title: string        // 标题
  points: string[]     // 要点列表
  notes?: string       // 演讲备注
  layout?: string      // 布局模板ID（AI推荐或用户选择）
  messageId?: number   // 关联的消息ID（图表页/指标页使用）
  chartConfig?: string // 图表配置JSON
  chartImageBase64?: string // 图表图片Base64（前端截图传入）
  chartImageUrls?: string[] // 图表截图URL列表（后端返回，用于预览）
  kpiCards?: KpiCardData[]  // KPI指标卡片数据列表（指标页使用）
}

/** PPT大纲响应 */
export interface PptOutlineResponse {
  slides: PptSlide[]
  systemPrompt?: string  // 发送给AI的系统提示词
  userPrompt?: string    // 发送给AI的用户提示词
}

/** PPT大纲生成请求 */
export interface PptOutlineRequest {
  sessionIds: number[] // 选中的会话ID列表
  title: string        // PPT标题
  audience?: string    // 目标受众
  style?: string       // 风格要求
}

/** PPT文件生成请求 */
export interface PptGenerateRequest {
  outline: PptOutlineResponse  // PPT大纲
  template: string             // 模板名称（business/medical/simple/tech/warm/dark）
  pptTitle?: string            // PPT主标题（封面页使用）
  audience?: string            // 受众信息
}

/** 生成PPT大纲 */
export function generatePptOutline(data: PptOutlineRequest) {
  return request.post<PptOutlineResponse>('/ai/ppt/outline', data, { timeout: 120000 })
}

/** PPT大纲优化请求 */
export interface PptOptimizeRequest {
  outline: PptOutlineResponse  // 当前大纲
  prompt: string               // 优化提示词
  slideIndex?: number          // 目标幻灯片索引（可选，-1或undefined表示全局优化）
  mode?: 'global' | 'single'   // 优化模式：global-全局优化, single-单页优化
}

/** 常用优化指令 */
export const OptimizeCommands = {
  simplify: '精简内容，减少要点数量，使内容更简洁',
  expand: '扩展内容，增加更多细节和要点',
  reorder: '调整顺序，重新排列要点使逻辑更清晰',
  changeLayout: '推荐更合适的布局样式',
  optimizeTitle: '优化标题，使其更吸引人',
  addNotes: '生成演讲备注'
} as const

/** 优化PPT大纲 */
export function optimizePptOutline(data: PptOptimizeRequest) {
  return request.post<PptOutlineResponse>('/ai/ppt/optimize', data, { timeout: 120000 })
}

/** 生成PPT文件（返回base64） */
export function generatePptFile(data: PptGenerateRequest) {
  return request.post<string>('/ai/ppt/generate', data, { timeout: 60000 })
}

// ===================== Word报告生成相关 =====================

/** Word章节 */
export interface WordChapter {
  order: number           // 章节序号
  title: string           // 章节标题
  type: 'text' | 'table' | 'chart' | 'conclusion'  // 章节类型
  content: string         // 正文内容
  messageId?: number      // 关联的消息ID
  tableData?: Record<string, any>[]  // 表格数据
  chartImageBase64?: string  // 图表图片Base64（前端截图传入）
  chartImageUrls?: string[]  // 图表截图URL列表（用于预览）
  subChapters?: WordChapter[]  // 子章节
}

/** Word报告大纲响应 */
export interface WordOutlineResponse {
  title: string
  subtitle?: string
  abstract?: string
  chapters: WordChapter[]
  systemPrompt?: string  // 发送给AI的系统提示词
  userPrompt?: string    // 发送给AI的用户提示词
}

/** Word大纲生成请求 */
export interface WordOutlineRequest {
  sessionIds: number[]    // 选中的会话ID列表
  title: string           // 报告标题
  idea?: string           // 用户要求/思路
  datasourceId?: number   // 数据源ID
}

/** Word文件生成请求 */
export interface WordGenerateRequest {
  outline: WordOutlineResponse  // Word大纲
  template: string              // 模板名称（formal/simple/academic）
  datasourceId?: number         // 数据源ID
}

/** 生成Word报告大纲 */
export function generateWordOutline(data: WordOutlineRequest) {
  return request.post<WordOutlineResponse>('/ai/word/outline', data, { timeout: 120000 })
}

/** 生成Word文件（返回base64） */
export function generateWordFile(data: WordGenerateRequest) {
  return request.post<string>('/ai/word/generate', data, { timeout: 60000 })
}

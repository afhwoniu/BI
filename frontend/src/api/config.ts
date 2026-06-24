import request from './request'

// 配置分组
export interface ConfigGroup {
  key: string
  name: string
  icon: string
}

// 配置项
export interface ConfigItem {
  id: number
  configKey: string
  configValue: string | null
  configGroup: string
  configType: string
  isEncrypted: boolean
  displayName: string | null
  remark: string | null
  sortOrder: number
}

// 获取配置分组列表
export function getConfigGroups() {
  return request.get<ConfigGroup[]>('/config/groups')
}

// 获取指定分组的配置
export function getConfigByGroup(group: string) {
  return request.get<ConfigItem[]>(`/config/group/${group}`)
}

// 获取单个配置
export function getConfig(key: string) {
  return request.get<string>(`/config/${key}`)
}

// 批量保存配置
export function saveConfigBatch(configs: ConfigItem[]) {
  return request.put<boolean>('/config/batch', configs)
}

// 刷新配置缓存
export function refreshConfigCache() {
  return request.post<boolean>('/config/refresh')
}

// 测试AI连接（旧接口，保留兼容）
export function testAiConnection() {
  return request.post<{ provider: string; status: string }>('/config/test-ai')
}

// LLM测试请求参数
export interface LlmTestRequest {
  provider: string
  model: string
  baseUrl: string
  apiKey: string
}

// LLM测试响应
export interface LlmTestResponse {
  success: boolean
  message: string
  response?: string
}

// Embedding测试响应
export interface EmbeddingTestResponse {
  success: boolean
  message: string
  dimensions?: number
}

// 测试LLM连接
export function testLlmConnection(params: LlmTestRequest) {
  return request.post<LlmTestResponse>('/config/test-llm', params)
}

// 测试Embedding连接
export function testEmbeddingConnection(params: LlmTestRequest) {
  return request.post<EmbeddingTestResponse>('/config/test-embedding', params)
}

// ASR测试请求参数
export interface AsrTestRequest {
  provider: string
  model: string
  apiKey: string
}

// ASR测试响应
export interface AsrTestResponse {
  success: boolean
  message: string
}

// 测试ASR连接
export function testAsrConnection(params: AsrTestRequest) {
  return request.post<AsrTestResponse>('/config/test-asr', params)
}

// 获取开发说明
export function getDevNotes() {
  return request.get<string>('/config/dev-notes')
}

// 保存开发说明
export function saveDevNotes(content: string) {
  return request.put<boolean>('/config/dev-notes', { content })
}


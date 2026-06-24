import request from '@/api/request'

// 测试用例
export interface TestCase {
  id: number
  name: string
  query: string
  expectedDocumentIds?: string
  expectedChunkIds?: string
  expectedKeywords?: string
  categoryId?: number
  remark?: string
  isEnabled: boolean
  createdAt: string
  updatedAt: string
}

// 测试运行
export interface TestRun {
  id: number
  name?: string
  status: string
  totalCases: number
  completedCases: number
  topK: number
  minScore: number
  hitRate: number
  mrr: number
  avgPrecision: number
  avgRecall: number
  avgLatencyMs: number
  startedAt?: string
  completedAt?: string
  createdAt: string
}

// 检索到的分块
export interface RetrievedChunk {
  chunkId: number
  documentId: number
  documentTitle: string
  contentPreview: string
  score: number
  rank: number
  isExpected: boolean
}

// 单个用例测试结果
export interface TestCaseResult {
  caseId: number
  caseName: string
  query: string
  isHit: boolean
  reciprocalRank: number
  precision: number
  recall: number
  latencyMs: number
  retrievedChunks: RetrievedChunk[]
  expectedDocIds: number[]
  expectedChunkIds: number[]
}

// 测试报告
export interface TestReport {
  runId: number
  name?: string
  status: string
  totalCases: number
  completedCases: number
  topK: number
  minScore: number
  hitRate: number
  mrr: number
  avgPrecision: number
  avgRecall: number
  avgLatencyMs: number
  details: TestCaseResult[]
  startedAt?: string
  completedAt?: string
}

// 获取所有测试用例
export function getTestCases() {
  return request.get<TestCase[]>('/knowledge-test/cases')
}

// 获取单个测试用例
export function getTestCase(id: number) {
  return request.get<TestCase>(`/knowledge-test/cases/${id}`)
}

// 创建测试用例
export function createTestCase(data: Record<string, unknown>) {
  return request.post<TestCase>('/knowledge-test/cases', data)
}

// 更新测试用例
export function updateTestCase(id: number, data: Record<string, unknown>) {
  return request.put<TestCase>(`/knowledge-test/cases/${id}`, data)
}

// 删除测试用例
export function deleteTestCase(id: number) {
  return request.delete(`/knowledge-test/cases/${id}`)
}

// 获取测试运行列表
export function getTestRuns(page = 1, pageSize = 20) {
  return request.get<TestRun[]>('/knowledge-test/runs', {
    params: { page, pageSize }
  })
}

// 获取测试报告
export function getTestReport(id: number) {
  return request.get<TestReport>(`/knowledge-test/runs/${id}`)
}

// 启动测试运行
export function startTestRun(name?: string, topK = 5, minScore = 0.5) {
  return request.post<TestRun>('/knowledge-test/runs', {
    name,
    topK,
    minScore
  })
}

// 删除测试运行
export function deleteTestRun(id: number) {
  return request.delete(`/knowledge-test/runs/${id}`)
}


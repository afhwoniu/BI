import request from './request'

// ============ 类型定义 ============

/** 知识库分类 */
export interface KnowledgeCategory {
  id: number
  name: string
  parentId?: number
  sortOrder: number
  description?: string
  createdAt: string
  children?: KnowledgeCategory[]
}

/** 知识库文档 */
export interface KnowledgeDocument {
  id: number
  categoryId?: number
  title: string
  fileName?: string
  fileType?: string
  fileSize?: number
  filePath?: string
  status: string  // pending, processing, completed, failed
  errorMessage?: string
  chunkCount: number
  processedChunkCount: number  // 已处理分块数
  processProgress: number      // 处理进度（0-100）
  datasourceId?: number
  createdBy?: number
  createdAt: string
  updatedAt: string
  category?: KnowledgeCategory
}

/** 文档列表响应 */
export interface DocumentListResponse {
  items: KnowledgeDocument[]
  total: number
  page: number
  pageSize: number
}

/** 检索结果 */
export interface KnowledgeSearchResult {
  chunkId: number
  documentId: number
  documentTitle: string
  fileName?: string      // 文件名
  content: string
  score: number
  pageNumber?: number
  sectionTitle?: string
}

/** 检索请求 */
export interface SearchRequest {
  query: string
  topK?: number
  categoryId?: number
  datasourceId?: number
  minScore?: number
}

// ============ 分类管理 ============

/** 获取分类列表（树形结构） */
export function getCategories() {
  return request.get<KnowledgeCategory[]>('/knowledge/categories')
}

/** 创建分类 */
export function createCategory(data: { name: string; parentId?: number; description?: string }) {
  return request.post<KnowledgeCategory>('/knowledge/categories', data)
}

/** 更新分类 */
export function updateCategory(id: number, data: { name: string; description?: string }) {
  return request.put<KnowledgeCategory>(`/knowledge/categories/${id}`, data)
}

/** 删除分类 */
export function deleteCategory(id: number) {
  return request.delete<{ message: string }>(`/knowledge/categories/${id}`)
}

// ============ 文档管理 ============

/** 获取文档列表 */
export function getDocuments(params: {
  categoryId?: number
  status?: string
  keyword?: string
  page?: number
  pageSize?: number
}) {
  return request.get<DocumentListResponse>('/knowledge/documents', { params })
}

/** 获取文档详情 */
export function getDocument(id: number) {
  return request.get<KnowledgeDocument>(`/knowledge/documents/${id}`)
}

/** 上传文档（文档处理可能需要较长时间，设置5分钟超时） */
export function uploadDocument(formData: FormData) {
  return request.post<KnowledgeDocument>('/knowledge/documents/upload', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
    timeout: 300000  // 5分钟超时，因为需要解析+分块+向量化
  })
}

/** 删除文档 */
export function deleteDocument(id: number) {
  return request.delete<{ message: string }>(`/knowledge/documents/${id}`)
}

/** 重新处理文档 */
export function reprocessDocument(id: number) {
  return request.post<{ message: string }>(`/knowledge/documents/${id}/reprocess`)
}

/** 文档处理状态 */
export interface DocumentStatus {
  id: number
  status: string  // pending, processing, completed, failed
  progress: number  // 0-100
  chunkCount: number
  processedChunkCount: number
  errorMessage?: string
}

/** 获取文档处理状态（用于轮询） */
export function getDocumentStatus(id: number) {
  return request.get<DocumentStatus>(`/knowledge/documents/${id}/status`)
}

/** 知识库分块 */
export interface KnowledgeChunk {
  id: number
  documentId: number
  chunkIndex: number
  content: string
  contentLength: number
  pageNumber?: number
  sectionTitle?: string
  createdAt: string
}

/** 获取文档分块列表 */
export function getDocumentChunks(id: number) {
  return request.get<KnowledgeChunk[]>(`/knowledge/documents/${id}/chunks`)
}

// ============ 语义检索 ============

/** 语义检索 */
export function searchKnowledge(data: SearchRequest) {
  return request.post<KnowledgeSearchResult[]>('/knowledge/search', data)
}


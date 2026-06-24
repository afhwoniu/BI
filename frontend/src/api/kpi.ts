import request from './request'

// 指标分类相关
export interface KpiCategory {
  id: number
  name: string
  parentId: number | null
  description: string | null
  sortOrder: number
  children?: KpiCategory[]
}

export interface KpiCategoryForm {
  name: string
  parentId: number | null
  description: string | null
  sortOrder: number
}

// 指标定义相关
export interface KpiDefinition {
  id: number
  code: string
  name: string
  categoryId: number
  categoryName?: string
  definition: string | null
  formula: string | null
  sqlTemplate: string | null
  datasourceId: number | null
  unit: string | null
  dataType: string
  hasEmbedding: boolean
  embeddingUpdatedAt: string | null
  isEnabled: boolean
  createdAt: string
  updatedAt: string
}

export interface KpiDefinitionForm {
  code: string
  name: string
  categoryId: number
  definition: string | null
  formula: string | null
  sqlTemplate: string | null
  datasourceId: number | null
  unit: string | null
  dataType: string
  isEnabled: boolean
}

export interface KpiSearchResult {
  id: number
  code: string
  name: string
  definition: string | null
  formula: string | null
  sqlTemplate: string | null
  unit: string | null
  score: number
}

// 分类API
export const getCategories = () => request.get<KpiCategory[]>('/kpi/categories')
export const createCategory = (data: KpiCategoryForm) => request.post<KpiCategory>('/kpi/categories', data)
export const updateCategory = (id: number, data: KpiCategoryForm) => request.put<KpiCategory>(`/kpi/categories/${id}`, data)
export const deleteCategory = (id: number) => request.delete<boolean>(`/kpi/categories/${id}`)

// 指标API
export const getDefinitions = (params: { categoryId?: number; keyword?: string; page?: number; pageSize?: number }) =>
  request.get<{ items: KpiDefinition[]; total: number; page: number; pageSize: number }>('/kpi/definitions', { params })
export const getDefinition = (id: number) => request.get<KpiDefinition>(`/kpi/definitions/${id}`)
export const createDefinition = (data: KpiDefinitionForm) => request.post<KpiDefinition>('/kpi/definitions', data)
export const updateDefinition = (id: number, data: KpiDefinitionForm) => request.put<KpiDefinition>(`/kpi/definitions/${id}`, data)
export const deleteDefinition = (id: number) => request.delete<boolean>(`/kpi/definitions/${id}`)

// 向量相关
export const searchKpi = (query: string, topK = 5, minScore = 0.5) =>
  request.post<KpiSearchResult[]>('/kpi/search', { query, topK, minScore })
export const generateEmbedding = (id: number) => request.post<boolean>(`/kpi/definitions/${id}/embedding`)
export const generateEmbeddings = (ids?: number[]) => request.post<boolean>('/kpi/definitions/embeddings', ids)


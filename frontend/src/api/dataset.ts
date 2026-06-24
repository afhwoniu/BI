import request from './request'

export interface DatasetField {
  id?: number
  fieldName: string
  fieldAlias?: string
  dataType: string
  role: 'dim' | 'measure'
  aggType: string
  sortOrder: number
}

export interface Dataset {
  id: number
  name: string
  datasourceId: number
  datasourceName?: string
  remark?: string
  createdAt: string
  updatedAt: string
}

export interface DatasetDetail {
  id: number
  name: string
  datasourceId: number
  sqlText: string
  paramSchema?: string
  remark?: string
  fields: DatasetField[]
}

export interface DatasetCreate {
  name: string
  datasourceId: number
  sqlText: string
  paramSchema?: string
  remark?: string
  fields?: DatasetField[]
}

export interface DatasetUpdate extends DatasetCreate {}

export interface DatasetPreview {
  datasourceId: number
  sqlText: string
  maxRows?: number
}

export interface ColumnInfo {
  name: string
  dataType: string
}

export interface PreviewResult {
  columns: ColumnInfo[]
  rows: Record<string, any>[]
  totalRows: number
}

// 获取数据集列表
export function getDatasetList() {
  return request.get<Dataset[]>('/dataset')
}

// 获取数据集详情
export function getDatasetDetail(id: number) {
  return request.get<DatasetDetail>(`/dataset/${id}`)
}

// 创建数据集
export function createDataset(data: DatasetCreate) {
  return request.post<number>('/dataset', data)
}

// 更新数据集
export function updateDataset(id: number, data: DatasetUpdate) {
  return request.put<boolean>(`/dataset/${id}`, data)
}

// 删除数据集
export function deleteDataset(id: number) {
  return request.delete<boolean>(`/dataset/${id}`)
}

// 预览数据集
export function previewDataset(data: DatasetPreview) {
  return request.post<PreviewResult>('/dataset/preview', data)
}


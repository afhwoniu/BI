import request from './request'

// 报表列表项
export interface ReportListItem {
  id: number
  name: string
  reportType: string
  coverImage?: string
  isPublished: boolean
  pageCount: number
  createdAt: string
  updatedAt?: string
}

// 报表详情
export interface ReportDetail {
  id: number
  name: string
  reportType: string
  coverImage?: string
  configJson: string
  remark?: string
  isPublished: boolean
  publishedAt?: string
  createdAt: string
  updatedAt?: string
  pages: ReportPage[]
}

// 报表页面
export interface ReportPage {
  id: number
  title: string
  sortOrder: number
  configJson: string
  items: ReportItem[]
}

// 报表元素
export interface ReportItem {
  id: number
  itemType: 'chart' | 'panel' | 'text' | 'image' | 'shape'
  chartId?: number
  chartName?: string
  panelId?: number
  panelName?: string
  textContent?: string
  imageUrl?: string
  layoutJson: string
  styleJson: string
  sortOrder: number
}

// 创建报表请求
export interface ReportCreate {
  name: string
  reportType?: string
  coverImage?: string
  configJson?: string
  remark?: string
}

// 更新报表请求
export interface ReportUpdate extends ReportCreate {}

// 保存页面请求
export interface ReportPageSave {
  id?: number
  title: string
  sortOrder: number
  configJson?: string
}

// 保存元素请求
export interface ReportItemSave {
  id?: number
  itemType: string
  chartId?: number
  panelId?: number
  textContent?: string
  imageUrl?: string
  layoutJson: string
  styleJson?: string
  sortOrder: number
}

// 获取报表列表
export function getReportList() {
  return request.get<ReportListItem[]>('/reports')
}

// 获取报表详情
export function getReportDetail(id: number) {
  return request.get<ReportDetail>(`/reports/${id}`)
}

// 创建报表
export function createReport(data: ReportCreate) {
  return request.post<{ id: number }>('/reports', data)
}

// 更新报表
export function updateReport(id: number, data: ReportUpdate) {
  return request.put<void>(`/reports/${id}`, data)
}

// 删除报表
export function deleteReport(id: number) {
  return request.delete<void>(`/reports/${id}`)
}

// 保存页面
export function saveReportPage(reportId: number, data: ReportPageSave) {
  return request.post<{ id: number }>(`/reports/${reportId}/pages`, data)
}

// 删除页面
export function deleteReportPage(pageId: number) {
  return request.delete<void>(`/reports/pages/${pageId}`)
}

// 保存元素
export function saveReportItem(pageId: number, data: ReportItemSave) {
  return request.post<{ id: number }>(`/reports/pages/${pageId}/items`, data)
}

// 删除元素
export function deleteReportItem(itemId: number) {
  return request.delete<void>(`/reports/items/${itemId}`)
}

// 获取报表渲染数据
export function getReportRenderData(id: number) {
  return request.get<any>(`/reports/${id}/render`)
}


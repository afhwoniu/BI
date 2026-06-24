import request from './request'
import type { DatasetField } from './dataset'

export interface Chart {
  id: number
  name: string
  datasetId: number
  datasetName?: string
  chartType: string
  remark?: string
  createdAt: string
  updatedAt: string
}

export interface ChartDetail {
  id: number
  name: string
  datasetId: number
  datasetName?: string
  chartType: string
  configJson: string
  remark?: string
  createdAt: string
  fields: DatasetField[]
}

export interface ChartConfig {
  dimensions?: string[]
  measures?: string[]
  title?: string
  showLegend?: boolean
  echartsOption?: any
}

export interface ChartCreate {
  name: string
  datasetId: number
  chartType: string
  configJson: string
  remark?: string
}

export interface ChartUpdate extends ChartCreate {}

export interface FilterCondition {
  field: string
  operator: string
  value: any
}

export interface ChartQueryDto {
  filters?: FilterCondition[]
  skipCache?: boolean
}

export interface SeriesData {
  name: string
  data: any[]
}

export interface ChartQueryResult {
  categories: string[]
  series: SeriesData[]
  rawData: Record<string, any>[]
  yoySeries?: SeriesData[]  // 同比数据
  momSeries?: SeriesData[]  // 环比数据
}

// 获取图表列表
export function getChartList() {
  return request.get<Chart[]>('/chart')
}

// 获取图表详情
export function getChartDetail(id: number) {
  return request.get<ChartDetail>(`/chart/${id}`)
}

// 创建图表
export function createChart(data: ChartCreate) {
  return request.post<number>('/chart', data)
}

// 更新图表
export function updateChart(id: number, data: ChartUpdate) {
  return request.put<boolean>(`/chart/${id}`, data)
}

// 删除图表
export function deleteChart(id: number) {
  return request.delete<boolean>(`/chart/${id}`)
}

// 图表查询
export function queryChart(id: number, dto?: ChartQueryDto) {
  return request.post<ChartQueryResult>(`/chart/${id}/query`, dto || {})
}

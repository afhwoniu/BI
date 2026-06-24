import request from './request'

export interface Panel {
  id: number
  name: string
  panelType: string
  remark?: string
  itemCount: number
  createdAt: string
  updatedAt: string
}

export interface PanelItem {
  id: number
  chartId?: number
  chartName?: string
  chartType?: string
  layoutJson: string
  sortOrder: number
}

export interface PanelDetail {
  id: number
  name: string
  panelType: string
  configJson: string
  remark?: string
  items: PanelItem[]
}

export interface PanelCreate {
  name: string
  panelType?: string
  configJson?: string
  remark?: string
}

export interface PanelItemUpdate {
  chartId?: number
  layoutJson: string
  sortOrder: number
}

export interface PanelUpdate {
  name: string
  panelType?: string
  configJson?: string
  remark?: string
  items?: PanelItemUpdate[]
}

// 获取面板列表
export function getPanelList(panelType?: string) {
  const params = panelType ? { panelType } : {}
  return request.get<Panel[]>('/panel', { params })
}

// 获取面板详情
export function getPanelDetail(id: number) {
  return request.get<PanelDetail>(`/panel/${id}`)
}

// 创建面板
export function createPanel(data: PanelCreate) {
  return request.post<number>('/panel', data)
}

// 更新面板
export function updatePanel(id: number, data: PanelUpdate) {
  return request.put<boolean>(`/panel/${id}`, data)
}

// 删除面板
export function deletePanel(id: number) {
  return request.delete<boolean>(`/panel/${id}`)
}


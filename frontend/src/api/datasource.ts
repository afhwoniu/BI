import request from './request'

export interface Datasource {
  id: number
  name: string
  type: string
  isEnabled: boolean
  remark?: string
  createdAt: string
  updatedAt: string
}

export interface DatasourceDetail extends Datasource {
  connString: string
}

export interface DatasourceCreate {
  name: string
  type: string
  connString: string
  remark?: string
}

export interface DatasourceUpdate {
  name: string
  type: string
  connString?: string
  isEnabled: boolean
  remark?: string
}

export interface DatasourceTest {
  type: string
  connString: string
}

// 获取数据源列表
export function getDatasourceList() {
  return request.get<Datasource[]>('/datasource')
}

// 获取数据源详情
export function getDatasourceDetail(id: number) {
  return request.get<DatasourceDetail>(`/datasource/${id}`)
}

// 创建数据源
export function createDatasource(data: DatasourceCreate) {
  return request.post<number>('/datasource', data)
}

// 更新数据源
export function updateDatasource(id: number, data: DatasourceUpdate) {
  return request.put<boolean>(`/datasource/${id}`, data)
}

// 删除数据源
export function deleteDatasource(id: number) {
  return request.delete<boolean>(`/datasource/${id}`)
}

// 测试连接
export function testDatasourceConnection(data: DatasourceTest) {
  return request.post<string>('/datasource/test', data)
}


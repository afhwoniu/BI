import request from './request'

// 菜单项
export interface MenuItem {
  id: number
  name: string
  parentId: number
  menuType: string
  icon?: string
  linkUrl?: string
  publishId?: number
  sortOrder: number
  isVisible: boolean
  remark?: string
  publishTitle?: string
  children?: MenuItem[]
}

// 发布项
export interface PublishItem {
  id: number
  title: string
  objectType: string
  objectId: number
  accessScope: string
  accessToken?: string
  isEnabled: boolean
  viewCount: number
  lastViewedAt?: string
  expireAt?: string
  createdAt: string
  remark?: string
}

// 创建菜单请求
export interface MenuCreate {
  name: string
  parentId: number
  menuType?: string
  icon?: string
  linkUrl?: string
  publishId?: number
  sortOrder: number
  isVisible?: boolean
  remark?: string
}

// 创建发布请求
export interface PublishCreate {
  title: string
  objectType: string
  objectId: number
  accessScope?: string
  accessPassword?: string
  expireAt?: string
  allowedRoles?: string
  remark?: string
}

// 获取菜单树
export function getMenuTree() {
  return request.get<MenuItem[]>('/menus/tree')
}

// 获取菜单列表
export function getMenuList() {
  return request.get<MenuItem[]>('/menus')
}

// 创建菜单
export function createMenu(data: MenuCreate) {
  return request.post<{ id: number }>('/menus', data)
}

// 更新菜单
export function updateMenu(id: number, data: MenuCreate) {
  return request.put<void>(`/menus/${id}`, data)
}

// 删除菜单
export function deleteMenu(id: number) {
  return request.delete<void>(`/menus/${id}`)
}

// 获取发布列表
export function getPublishList(objectType?: string) {
  const params = objectType ? `?objectType=${objectType}` : ''
  return request.get<PublishItem[]>(`/publishes${params}`)
}

// 创建发布
export function createPublish(data: PublishCreate) {
  return request.post<{ id: number; token: string }>('/publishes', data)
}

// 更新发布
export function updatePublish(id: number, data: PublishCreate) {
  return request.put<void>(`/publishes/${id}`, data)
}

// 删除发布
export function deletePublish(id: number) {
  return request.delete<void>(`/publishes/${id}`)
}

// 切换发布状态
export function togglePublish(id: number) {
  return request.put<void>(`/publishes/${id}/toggle`)
}

// 重新生成Token
export function regenerateToken(id: number) {
  return request.post<{ token: string }>(`/publishes/${id}/regenerate-token`)
}

// 获取公开菜单（门户）
export function getPortalMenus() {
  return request.get<MenuItem[]>('/portal/menus')
}

// 通过Token访问发布内容
export function viewByToken(token: string, password?: string) {
  const params = password ? `?password=${password}` : ''
  return request.get<any>(`/portal/view/${token}${params}`)
}


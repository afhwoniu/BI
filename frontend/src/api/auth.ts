import request from './request'

// API响应类型
export interface ApiResponse<T> {
  code: number
  message: string
  data: T | null
}

export interface LoginResponse {
  token: string
  userId: number
  username: string
  realName?: string
  avatar?: string
}

export interface UserInfo {
  userId: number
  username: string
  realName?: string
  email?: string
  phone?: string
  avatar?: string
}

// 登录
export function login(username: string, password: string): Promise<ApiResponse<LoginResponse>> {
  return request.post('/auth/login', { username, password })
}

// 获取用户信息
export function getUserInfo(): Promise<ApiResponse<UserInfo>> {
  return request.get('/auth/userinfo')
}


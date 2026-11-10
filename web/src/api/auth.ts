import http from './http'                                   // 复用带拦截器的实例
import type { LoginResult } from './types'                  // 仅类型，编译后消失

export async function login(username: string, password: string): Promise<LoginResult> {
  const { data } = await http.post<LoginResult>('/auth/login', { username, password })
  // ↑ 等价于 POST /api/auth/login，请求体 { username, password }

  sessionStorage.setItem('token', data.token)               // 存 token 给拦截器用
  sessionStorage.setItem('role', data.role)                 // 存角色
  return data                                               // 返回给 LoginCard
}

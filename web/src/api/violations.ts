import http from './http'                                     // 复用带 token 的 axios 实例
import type { PagedResult, Violation } from './types'          // 类型

// 查违规列表：状态过滤 + 分页
export async function fetchViolations(status: string, page: number, pageSize: number) {
  const { data } = await http.get<PagedResult<Violation>>('/violations', {
    params: { status: status || undefined, page, pageSize }   // status 为空就不传，后端会返回全部
  })
  return data                                                  // { items, total, page, pageSize }
}

// 处理一条违规：只传备注，处理人由后端从 token 里取
export async function handleViolation(id: number, remark: string) {
  const { data } = await http.patch(`/violations/${id}/handle`, { remark })
  return data
}

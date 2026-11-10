import http from './http'                                     // 复用实例
import type { ComplianceStats } from './types'                // 类型

// 某一天的合规统计，不传日期就是今天
export async function fetchCompliance(date?: string) {
  const { data } = await http.get<ComplianceStats>('/stats/compliance', {
    params: { date }                                           // date 为 undefined 时不会出现在 URL 里
  })
  return data
}

// 最近 N 天的趋势
export async function fetchDaily(days = 7) {
  const { data } = await http.get('/stats/daily', { params: { days } })
  return data as Array<{
    date: string
    personDetections: number
    violations: number
    complianceRate: number
  }>
}

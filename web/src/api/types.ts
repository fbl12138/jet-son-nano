// 登录接口的返回结构（对应后端 AuthController 的 Ok(new { token, expiresAt, username, role })）
export interface LoginResult {
  token: string          // JWT 字符串，后续请求放进 Authorization 头
  expiresAt: string      // 过期时间（ISO 字符串），现在先不用，将来可做自动续期
  username: string       // 用户名
  role: string           // 角色：Admin / User
}

// 分页返回的通用结构（对应后端 PagedResult<T>）
export interface PagedResult<T> {
  items: T[]             // 当前页数据
  total: number          // 总条数
  page: number           // 当前页码（从 1 开始）
  pageSize: number       // 每页条数
}

// 违规记录（对应后端 ViolationDto）
export interface Violation {
  id: number                     // 违规 Id
  deviceName: string             // 设备名（后端 JOIN 出来的）
  trackId: string | null         // 跟踪号，可能为空
  violationTypes: string         // 违规类型，如 "no_mask"
  confidence: number | null      // 置信度，可能为空
  detectedAt: string             // 检测时间（UTC ISO 字符串，前端要转本地显示）
  status: string                 // 状态：Open / Handled
  imagePath: string | null       // 抓拍图路径，现在都是 null
}

// 某一天的合规统计（对应后端 StatsDtos）
export interface ComplianceStats {
  date: string                   // 统计日期 yyyy-MM-dd
  devices: number                // 当天有数据的设备数
  personDetections: number       // 检测人次
  violations: number             // 违规条数
  compliant: number              // 合规人次
  complianceRate: number         // 合规率 0~1（显示时要乘 100）
}
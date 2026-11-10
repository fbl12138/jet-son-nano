import axios from 'axios'                                   // 引入 axios

const http = axios.create({                                 // 创建带默认配置的实例
  baseURL: '/api',                                          // 所有请求加 /api 前缀（开发时被 Vite 代理到 5027）
  timeout: 5000                                             // 5 秒超时
})

http.interceptors.request.use(config => {                   // 请求拦截器：发请求前执行
  const token = sessionStorage.getItem('token')             // 取登录时存的 token
  if (token) {                                              // 有 token 才加头
    config.headers.Authorization = `Bearer ${token}`        // 后端 JWT 要求的格式
  }
  return config                                             // 必须返回 config
})

http.interceptors.response.use(                             // 响应拦截器
  response => response,                                     // 2xx 原样返回
  error => {                                                // 非 2xx
    if (error.response?.status === 401) {                   // token 无效或过期
      sessionStorage.clear()                                // 清本地登录状态
      location.reload()                                     // 刷新，App.vue 退回登录页
    }
    return Promise.reject(error)                            // 错误继续抛给调用方
  }
)

export default http                                         // 其他文件 import 使用

import { createApp } from 'vue'                             // Vue 的启动函数
import './style.css'                                        // 全局样式（Vite 模板自带）
import App from './App.vue'                                 // 根组件
import ElementPlus from 'element-plus'                      // UI 组件库
import 'element-plus/dist/index.css'                        // 组件库样式，必须引

createApp(App)                                              // 创建应用
  .use(ElementPlus)                                         // 注册 Element Plus
  .mount('#app')                                            // 挂到 index.html 的 #app 上
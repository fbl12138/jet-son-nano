# 管理端前端（Vue 3 + TypeScript + Vite）

后端接口的图形界面：登录、合规概览、最近 7 天趋势、违规分页与处置。

```bash
npm install
npm run dev      # 开发模式，Vite 把 /api 代理到 http://localhost:5027
npm run build    # 类型检查 + 构建，产物在 dist/
```

`npm run build` 的产物需要复制到 `ObjectDetection/wwwroot/` 才会被后端静态托管
（当前镜像直接使用仓库里已构建好的 `wwwroot`，改动前端后记得重新构建并提交）。

目录：

```
src/
├─ api/          axios 实例与接口封装（http.ts 统一挂 token、处理 401）
├─ components/   LoginCard / StatsCards / ViolationTable / Dashboard
└─ App.vue       未登录显示登录页，登录后进入看板
```

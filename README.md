# JetSon Nano · 边缘 AI 口罩检测与合规管理系统

基于 NVIDIA Jetson Nano 的工厂入口口罩检测系统：边缘端跑 YOLOv8 推理与多帧判定，
后端用 .NET 10 完成设备管理、违规裁决、处理闭环与合规统计。

```
Jetson Nano                    .NET 10 后端                    管理端
┌─────────────────┐        ┌──────────────────────┐      ┌──────────────┐
│ YOLOv8n 口罩检测 │        │ ASP.NET Core Web API │      │ Vue 3 + Vite │
│ TensorRT FP16   │  HTTP  │ EF Core 10           │◄────►│ Element Plus │
│ ByteTrack 跟踪   │───────►│ SQL Server           │      │              │
│ 多帧确认/ROI过滤 │ X-Api-Key│ 后台任务 × 2        │      │ JWT          │
└─────────────────┘        └──────────────────────┘      └──────────────┘
        上报检测结果              裁决·落库·聚合                 统计·处置
```

## 技术栈

| 端 | 技术 |
|---|---|
| 边缘推理 | YOLOv8n（口罩三分类）、TensorRT FP16、ByteTrack |
| 后端 | .NET 10 / ASP.NET Core / EF Core 10 / SQL Server |
| 鉴权 | 设备端 `X-Api-Key`，管理端 JWT Bearer |
| 后台任务 | `BackgroundService` × 2（心跳监控、统计回填与清理） |
| 前端 | Vue 3 + TypeScript + Vite + Element Plus + axios |
| 文档 | Swagger / OpenAPI |

## 核心设计

**1. 双鉴权，因为两类调用方完全不同**

- 边缘设备：无交互登录、无刷新令牌概念，用 `X-Api-Key` 头（按设备签发）
- 管理端：标准 JWT，`sub`/`name`/`role` 三个 claim，`role` 决定能否处置违规

**2. 违规裁决在服务端，设备只上报事实**

设备上报每个目标的类别与置信度，服务端按该设备的 `ComplianceRule` 裁决：
`RequireMask` / `RequireHelmet` / `MinConfidence` 三者决定是否生成违规记录。
好处是规则可在管理端热改，不用重新部署到设备。

**3. 心跳与离线判定**

设备每 20 秒上报心跳；`DeviceHeartbeatMonitor` 每 15 秒扫描一次，
超过 60 秒未上报置为 `Offline`。列表接口不直接信任库里的 `Status`，
而是用 `LastHeartbeatAt` 实时重算，避免后台任务延迟造成的状态滞后。

**4. 统计走预聚合，不实时扫描明细**

`DailyStatsWorker` 每小时回填最近 7 天的 `DailyStats`（按设备 × 天），
同时按保留策略分批清理：检测明细 30 天、违规记录 180 天（每批 5000 行，避免长事务）。

**5. 时间一律 UTC，展示层转 Asia/Shanghai**

`AppDbContext` 里用 `ValueConverter` 统一所有 `DateTime` 的 `Kind`，
跨时区部署（本地开发 vs Linux 容器）不会出现日期错位。

## 功能

| 模块 | 接口 |
|---|---|
| 登录 | `POST /api/auth/login` |
| 设备心跳 | `POST /api/devices/heartbeat`（ApiKey） |
| 设备列表 / 合规规则 | `GET /api/devices`、`GET/PUT /api/devices/{id}/rule` |
| 检测上报 | `POST /api/detections`（ApiKey） |
| 违规分页 / 详情 / 处置 | `GET /api/violations`、`GET /api/violations/{id}`、`PATCH /api/violations/{id}/handle` |
| 合规统计 / 日汇总 | `GET /api/stats/compliance`、`GET /api/stats/daily` |

违规处置是一个状态机：`Open → Handled`，重复处置返回 `409 Conflict`，`handledBy` 取自 token 而非请求体。

## 快速开始

### 环境

- .NET 10 SDK
- SQL Server（本机实例即可）
- Node.js 20+（仅前端构建）
- Jetson 端可选，无设备可用 `scripts/mock_jetson.py` 模拟

### 配置

`ObjectDetection/appsettings.json` 需要数据库连接串和 JWT 密钥。
**不要把真实密钥提交到仓库**，用 User Secrets 或环境变量：

```bash
cd ObjectDetection
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "<至少 32 位随机字符串>"
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost;Database=ObjectDetectionDb;Trusted_Connection=True;TrustServerCertificate=True;"
```

### 运行

```bash
# 建库
dotnet ef database update --project ObjectDetection

# 启动后端（局域网调试需绑 0.0.0.0，否则设备访问不到）
dotnet run --project ObjectDetection --urls http://0.0.0.0:5027

# 前端（开发模式，Vite 代理 /api → :5027）
cd web && npm install && npm run dev
```

Swagger：`http://localhost:5027/swagger`

开发环境首次启动会自动种两个账号（`Users` 表非空则跳过）：
`admin / Admin@123`（Admin）、`operator / User@123`（User）。

### 模拟设备

```bash
py scripts/mock_jetson.py     # 每 5 秒上报检测、每 20 秒上报心跳
```

## 目录结构

```
ObjectDetection/            后端（.NET 10）
├── Controllers/            6 个控制器
├── Models/                 Device / Detection / Violation / ComplianceRule / DailyStat / User
├── Data/                   AppDbContext（索引、精度、UTC 转换）
├── Services/               DetectionService（上报与裁决）
├── BackgroundServices/     DeviceHeartbeatMonitor / DailyStatsWorker
├── Dtos/                   请求与响应的传输对象
└── Migrations/             EF Core 迁移
web/                        管理前端（Vue 3 + Vite + TS）
scripts/                    Jetson 模拟、模型校验、视频合成、判定模块
models/best.pt              口罩 YOLOv8n 权重（MIT，来源见 docs/model-eval.md）
configs/                    ByteTrack 低阈值跟踪配置
docs/model-eval.md          模型评估卡（320 vs 640 实测对比）
REFACTOR-PLAN.md            改造计划（工程化补齐路线）
```

## 模型

| 项 | 值 |
|---|---|
| 权重 | YOLOv8n 三分类（`with_mask` / `without_mask` / `mask_weared_incorrect`） |
| 来源 | [jianxi-Erin/FaceMaskDetection](https://github.com/jianxi-Erin/FaceMaskDetection)（MIT） |
| 公开指标 | mAP50-95 = 0.708 |
| 边缘部署 | TensorRT FP16，15.5 FPS |

本机 CPU 实测（`docs/model-eval.md`）：320 输入 75.5 ms/张，640 输入 165.7 ms/张；
低置信度漏检是主要误差来源，因此 ByteTrack 用 `new_track_thresh=0.2` 的低阈值配置。

## 待改进

- 单元测试与集成测试尚未覆盖（见 `REFACTOR-PLAN.md` 阶段 1）
- 设备端多帧确认逻辑在 `scripts/judge.py`，与后端 `ComplianceRule.ConfirmFrames` 的职责边界需要收敛
- 上报接口暂无速率限制
- 未容器化（Dockerfile / compose 见改造计划阶段 4）

## 声明

口罩检测权重来自上游 MIT 许可项目，见 `docs/model-eval.md`；其余代码为本人实现。

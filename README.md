# JetSon Nano · 边缘 AI 口罩检测与合规管理系统

[![CI](https://github.com/fbl12138/jet-son-nano/actions/workflows/ci.yml/badge.svg)](https://github.com/fbl12138/jet-son-nano/actions/workflows/ci.yml)

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

---

## 快速开始（Docker，一条命令）

前提：装好 Docker 与 Docker Compose。

```bash
git clone https://github.com/fbl12138/jet-son-nano.git
cd jet-son-nano

# 1. 配置密钥（必须改，SQL Server 对密码复杂度有要求）
cp .env.example .env
nano .env          # 设置 DB_PASSWORD 与 JWT_KEY

# 2. 启动（首次会拉镜像并构建，约 5～10 分钟）
docker compose up -d --build

# 3. 验证
curl http://localhost:5027/health        # 期望：Healthy
```

打开 `http://localhost:5027`，用 `admin / Admin@123` 登录。

> **为什么能一条命令起：**
> `docker-compose.yml` 里包含 `app` 与 `db`（SQL Server 2022）两个服务，
> 应用通过服务名 `db` 访问数据库，启动时由 `Database__AutoMigrate=true` 自动建表，
> 密码与 JWT 密钥从 `.env` 注入，不进版本库。
> `db` 带就绪探测，应用等数据库 healthy 后再启动；迁移连不上时还会重试 10 次（每次间隔 3 秒），
> 不会因为 SQL Server 起得慢而进重启循环。

常用命令：

```bash
docker compose ps                # 状态
docker compose logs -f app       # 应用日志
docker compose down              # 停止（数据保留）
docker compose down -v           # 连数据库一起删
```

> 部署配置里用的是 `ASPNETCORE_ENVIRONMENT=Development`，以便自动种两个演示账号并开启 Swagger。
> **生产环境应改为 `Production`**，账号通过运维脚本创建。

---

## 本地开发

### 环境

- .NET 10 SDK
- SQL Server（本机实例即可）
- Node.js 20+（前端构建）

### 配置

`ObjectDetection/appsettings.json` 里的连接串与 JWT 密钥**不要提交真实值**，用 User Secrets：

```bash
cd ObjectDetection
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "<至少 32 位随机字符串>"
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost;Database=ObjectDetectionDb;Trusted_Connection=True;TrustServerCertificate=True;"
```

### 运行

```bash
# 跑测试（37 个用例）
dotnet test test/ObjectDetection.Tests

# 建库
dotnet ef database update --project ObjectDetection

# 启动后端（局域网联调需绑 0.0.0.0，否则设备访问不到）
dotnet run --project ObjectDetection --urls http://0.0.0.0:5027

# 前端（开发模式，Vite 代理 /api → :5027）
cd web && npm install && npm run dev
```

Swagger：`http://localhost:5027/swagger`（仅 Development）

开发环境首次启动会自动种两个账号（`Users` 表非空则跳过）：
`admin / Admin@123`（Admin）、`operator / User@123`（User）。

### 模拟设备

没有真实 Jetson 时：

```bash
py scripts/mock_jetson.py     # 每 5 秒上报检测、每 20 秒上报心跳
```

---

## 技术栈

| 端 | 技术 |
|---|---|
| 边缘推理 | YOLOv8n（口罩三分类）、TensorRT FP16、ByteTrack |
| 后端 | .NET 10 / ASP.NET Core / EF Core 10 / SQL Server |
| 鉴权 | 设备端 `X-Api-Key`（存哈希），管理端 JWT Bearer |
| 后台任务 | `BackgroundService` × 2（心跳监控、统计回填与清理） |
| 前端 | Vue 3 + TypeScript + Vite + Element Plus + axios |
| 测试 | xUnit（37 个用例） |
| 部署 | Docker 多阶段构建 + docker-compose |
| 文档 | Swagger / OpenAPI |

---

## 接口

| 方法 | 路径 | 鉴权 | 说明 |
|---|---|---|---|
| POST | `/api/auth/login` | 匿名 | 登录，返回 JWT（`sub`/`name`/`role`）；限流 10 次/分钟 |
| POST | `/api/devices/heartbeat` | `X-Api-Key` | 设备心跳（限流 60 次/分钟） |
| GET | `/api/devices` | JWT | 设备列表，在线状态按最近心跳实时计算 |
| GET | `/api/devices/{id}/rule` | JWT | 读取该设备的合规规则 |
| PUT | `/api/devices/{id}/rule` | JWT · Admin | 新增或更新合规规则（upsert） |
| POST | `/api/detections` | `X-Api-Key` | 上报检测结果（限流 60 次/分钟） |
| GET | `/api/violations` | JWT | 违规分页查询，支持 `status`/`deviceId`/`from`/`to`/`page`/`pageSize` |
| GET | `/api/violations/{id}` | JWT | 违规详情 |
| PATCH | `/api/violations/{id}/handle` | JWT · Admin | 处置违规：`Open → Handled`，重复处置返回 `409` |
| GET | `/api/stats/compliance` | JWT | 单日合规概览（可选 `date`） |
| GET | `/api/stats/daily` | JWT | 最近 N 天逐日统计（`days` 钳制到 1~31） |
| GET | `/health` | 匿名 | 健康检查，含数据库连通性探测 |

---

## 核心设计

**1. 双鉴权，因为两类调用方完全不同**

- 边缘设备：无交互登录、无刷新令牌概念，用 `X-Api-Key` 头；**数据库只存 SHA-256 哈希**，比较用 `CryptographicOperations.FixedTimeEquals`（恒定时间，避免时序侧信道）。
- 管理端：标准 JWT，`role` 决定能否处置违规；`handledBy` 取自 token 而非请求体。

**2. 违规裁决在服务端，设备只上报事实**

设备上报每个目标的类别与置信度，服务端按该设备的 `ComplianceRule` 裁决：
`RequireMask` / `RequireHelmet` / `MinConfidence` 决定是否生成违规记录。
规则可在管理端热改，无需重新部署到设备。
判定逻辑抽成纯函数 `Domain/ComplianceEvaluator`，不依赖 EF 与时间，可独立单测。

**3. 心跳与离线判定**

设备每 20 秒上报心跳；`DeviceHeartbeatMonitor` 每 15 秒扫描，超过 60 秒未上报置为 `Offline`。
列表接口不直接信任库里的 `Status`，而是用 `LastHeartbeatAt` 实时重算，避免后台任务延迟造成的状态滞后。

**4. 统计走预聚合，接口查询次数与天数无关**

`DailyStatsWorker` 每小时回填最近 31 天的 `DailyStats`（按设备 × 天）：

- 两张明细表各用 **1 条 SQL** 按「业务日」聚合（时间平移后取日期分组），不再是每天各查一次；
- 一次性读入区间内已有聚合行，避免「每设备查一次」的 N+1；
- 整个区间一次 `SaveChanges`。

`GET /api/stats/daily` 因此固定只发 **3 条 SQL**（1 次读聚合表 + 2 次算今天），与 `days`、设备数都无关。

> 今天的数据不读聚合表——回填按天进行，今天还在增长，所以今天实时算。
> 代价是接口里有两套口径，换来的是今天的数字不会错。

**5. 时间：存 UTC，业务日按上海时区切分**

- 数据库只存 UTC；`AppDbContext` 用 `ValueConverter` 统一 `DateTime.Kind`，保证序列化带 `Z`。
- 「哪一天」是业务概念：`Domain/DailyWindow` 把「上海的某一天」换算成 UTC 半开区间 `[start, end)`。
- 「今天」由 `StatsClock`（基于 .NET `TimeProvider`）提供，**不读系统时区**，因此容器部署到 UTC 也不会日期错位；测试用 `FixedTimeProvider` 注入固定时刻。

**6. 设备上报接口限流**

`POST /api/detections` 与 `/api/devices/heartbeat` 使用固定窗口限流（60 次/分钟），
防止设备网络抖动时的重试风暴；管理端接口不受影响。

**7. 数据保留**

检测明细保留 30 天、违规记录保留 180 天，按每批 5000 行分批删除，避免长事务锁表。

**8. 违规处置是条件更新，不是「先查后改」**

`PATCH /api/violations/{id}/handle` 落库时用一条 `UPDATE ... WHERE Id = @id AND Status = 'Open'`
（EF Core `ExecuteUpdateAsync`）：受影响行数为 1 才算处置成功，为 0 且记录存在就返回 `409`。

这样两个管理员同时点「处理」时，只有第一个能改到状态，第二个拿到 409，
**不会出现「两个人都显示处理成功、后写入的把前一个人的处理人和备注覆盖掉」**。
测试用 SQLite 覆盖了这个场景：一个上下文先读到 `Open`，另一个处置成功后再处置，必须返回 `AlreadyHandled`。

---

## 工程实践

| 项 | 实现 |
|---|---|
| 单元测试 | **37 个 xUnit 用例**：合规判定、日期窗口、合规口径、业务时钟、统计服务（InMemory）、违规处置并发（SQLite）、ApiKey 哈希 |
| 健康检查 | `GET /health` + `AddDbContextCheck`，容器编排可直接用作探针 |
| 限流 | 设备接口 60 次/分钟；登录接口 10 次/分钟，超限返回 `429` |
| 并发安全 | 违规处置用条件更新（`WHERE Status = 'Open'`），并发重复处置只会成功一次 |
| 全局异常 | `IExceptionHandler` 统一输出 `ProblemDetails`，5xx 不泄露内部细节 |
| 密钥管理 | 设备密钥存 SHA-256 哈希；JWT 密钥与数据库密码走环境变量 / User Secrets |
| 容器化 | 多阶段构建（SDK 编译 → ASP.NET 运行时），最终镜像不含源码与编译器 |
| CI/CD | GitHub Actions：push/PR 自动编译、跑测试、构建前端、验证镜像；推 main 后发布镜像到 GHCR |
| 决策记录 | `docs/decisions/`（ADR-001：移除服务端 `ConfirmFrames`） |

---

## CI/CD

流水线定义在 `.github/workflows/ci.yml`，push / PR 自动运行。

```
push / PR
   │
   ├─ backend   编译 + 33 个单元测试（上传测试报告）
   ├─ frontend  npm ci + vue-tsc 类型检查 + 构建
   │        ↓ 两者都通过
   └─ docker    构建镜像验证（push: false）
            ↓
        publish-image   ← 仅 push 到 main 时
            构建并推送 ghcr.io/<repo>:latest 与 :<commit-sha>
```

- **CI 的价值**：每次提交都在**全新环境**验证，能发现"本地能跑但漏提交文件"的问题
- **CD 的边界**：只负责**产出镜像**，不做部署。部署端在内网，无法被 GitHub 主动访问，因此采用**拉取式**：
  ```bash
  docker compose pull app && docker compose up -d app
  ```
- **两个 tag 的作用**：`latest` 用于日常部署，`:<sha>` 用于精确定位与回滚

---

## 目录结构

```
ObjectDetection/            后端（.NET 10）
├── Controllers/            5 个控制器
├── Models/                 Device / Detection / Violation / ComplianceRule / DailyStat / User
├── Data/                   AppDbContext（索引、精度、UTC 转换）
├── Domain/                 纯领域逻辑：ComplianceEvaluator / DailyWindow / ComplianceMath / StatsClock / ApiKeyHasher
├── Services/               DetectionService / DeviceService / ViolationService / StatsService
├── BackgroundServices/     DeviceHeartbeatMonitor / DailyStatsWorker
├── Infrastructure/         GlobalExceptionHandler
├── Dtos/                   请求与响应的传输对象
└── Migrations/             EF Core 迁移

test/ObjectDetection.Tests/ 单元测试（xUnit + EF InMemory）
web/                        管理前端（Vue 3 + Vite + TS）
scripts/                    Jetson 模拟、模型校验、视频合成、判定模块
models/best.pt              口罩 YOLOv8n 权重（MIT，来源见 docs/model-eval.md）
configs/                    ByteTrack 低阈值跟踪配置
docs/                       模型评估卡、架构决策记录
.github/workflows/ci.yml    CI/CD 流水线
Dockerfile / docker-compose.yml / .env.example   容器化部署
```

---

## 模型

| 项 | 值 |
|---|---|
| 权重 | YOLOv8n 三分类（`with_mask` / `without_mask` / `mask_weared_incorrect`） |
| 来源 | [jianxi-Erin/FaceMaskDetection](https://github.com/jianxi-Erin/FaceMaskDetection)（MIT） |
| 公开指标 | mAP50-95 = 0.708 |
| 边缘部署 | TensorRT FP16，15.5 FPS |

本机 CPU 实测（`docs/model-eval.md`）：320 输入 75.5 ms/张，640 输入 165.7 ms/张；
低置信度漏检是主要误差来源，因此 ByteTrack 用 `new_track_thresh=0.2` 的低阈值配置。

---

## 已知限制

诚实地列出来，避免"看起来完整、实际有坑"：

- **前端无自动化测试**：只有后端有单元测试，前端靠手工回归。
- **CI 仅跑在 GitHub**：Gitee 仓库不执行 Actions，Gitee 侧未接入 Gitee Go。
- **部署为拉取式且手动触发**：CI/CD 止于镜像发布；服务器在内网，无公网入口，推送式部署不可行。
- **前端构建未纳入镜像**：当前镜像使用仓库中已构建的 `wwwroot`；改前端需本地 `npm run build` 后提交产物。
- **单实例假设**：限流计数器、后台任务都按单实例设计，多副本部署需要引入分布式限流与任务互斥；登录限流同样是进程内固定窗口，多实例时每个实例各算一份额度。
- **日志仅控制台**：没有集中式日志收集与链路追踪。
- **演示环境用 Development**：compose 里为方便演示开启自动建号与 Swagger，生产需改为运维建号。

---

## 声明

口罩检测权重来自上游 MIT 许可项目，见 `docs/model-eval.md`；其余代码为本人实现。

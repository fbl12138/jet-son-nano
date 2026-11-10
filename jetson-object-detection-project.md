# 项目方案：AI 监控系统 —— 工厂入口口罩检测

> 定位：边缘 AI（Jetson Nano + YOLO 推理）+ C# 业务后端（ASP.NET Core + SQL Server）
> 场景：工厂车间入口，自动检测进入车间的工人是否佩戴口罩，未佩戴实时告警并留痕
> 目标：能写进简历的完整项目，同时覆盖你学习计划中 Web API / EF Core / 调试的全部要点
> 规模：MVP 约 2–3 周（每天 2 小时）

---

## 一、业务定义（定稿）

**项目名称**：AI 监控系统 —— 工厂入口口罩检测

**业务场景**
工厂车间入口（门岗/闸机通道）部署一台摄像头 + 边缘计算板。工人进入车间时，系统自动识别其**是否佩戴口罩**，未佩戴立即告警并记录，形成"检测 → 告警 → 整改 → 统计"的闭环。

**解决什么现实问题**

| 痛点 | 现状 | 系统怎么解决 |
|---|---|---|
| 人工守门看不全 | 门岗靠人盯，高峰期漏检、人情放行 | AI 每帧检测，不疲劳、不放水 |
| 违规无留痕 | 事后追责没有证据 | 每次违规抓拍 + 入库，可查可导出 |
| 合规率说不清 | 检查要数据，靠手工统计 | 自动统计每日/每班次合规率 |
| 管理成本高 | 多个车间入口要配多个人 | 一台设备管一个入口，后端统一管多设备 |

**检测目标（类别设计）**

| 类别 | 含义 | 训练难度 | 建议 |
|---|---|---|---|
| `person` | 人 | 极低（YOLO 官方预训练直接可用） | 必做 |
| `mask` / `no_mask` | 戴口罩 / 未戴 | **低：有现成公开模型/权重，无需自己训练** | 核心目标 |
| `helmet` / `no_helmet` | 戴安全帽 / 未戴 | 低 | 以后扩展，不进 MVP |

**判定规则**

```
进入 ROI（门口区域）的人：
  规则：必须检测到 mask（口罩）→ 否则记"未戴口罩"

判定条件：
  - 目标在 ROI 内（避免拍到路过的人）
  - 置信度 ≥ 阈值（如 0.5）
  - 连续 N 帧确认（如 3 帧，防误报）
  - 同一人 5 秒内只记一次违规（防重复告警）
```

**完整业务流程**

```
工人走向入口
  → 摄像头采帧 → YOLO 检测（person / mask / no_mask）
  → 人体跟踪（同一人多帧关联，避免一人多报）
  → ROI 判定：人在门口区域内？
  → 口罩判定：该人的口鼻区域有 mask？
  ├─ 有 → 记一条"合规通过"（可选，或只统计数据）
  └─ 没有 → 抓拍图片 + 生成"违规记录" → 实时语音提示"请佩戴口罩"
  → 违规处理闭环：确认整改 → 标记已处理 → 计入统计
```

**实时告警的三种落地方式（任选/组合）**

| 方式 | 实现 | 适合 |
|---|---|---|
| 现场语音/屏幕 | Jetson 本地接小喇叭/屏幕，检测到违规直接播报 | 最直接，延迟最低 |
| 后端推送 | 违规入库 → SignalR 推送到管理端页面/大屏 | 有看板需求 |
| 消息通知 | 违规 → 推送企业微信/钉钉/短信 | 要闭环处理 |

---

## 二、实现什么业务（功能清单）

### MVP（必须做）

| 编号 | 业务功能 | 说明 |
|---|---|---|
| F1 | 设备上报检测结果 | Jetson 上报：入口 ROI 内每个人 + 口罩检测结果 + 合规判定 |
| F2 | 违规记录查询 | 按设备、时间范围、班次分页查询（违规类型目前只有"未戴口罩"） |
| F3 | 设备管理 | 设备注册（每个入口一台）、心跳、在线/离线状态 |
| F4 | 合规规则配置 | 配置该入口是否要求戴口罩（可开关，预留扩展位） |
| F5 | 违规处理 | 违规列表、抓拍图查看、标记已处理、记录处理人 |
| F6 | 用户登录鉴权 | JWT 登录，区分管理员/班组长/普通用户 |

### 进阶（做完 MVP 再加）

| 编号 | 功能 | 说明 |
|---|---|---|
| F7 | 合规率统计 | 每日/每班次/每入口的合规率、违规趋势 |
| F8 | 实时看板 | SignalR 推送新违规到管理端大屏 |
| F9 | 抓拍图片 | 违规时保存该帧图片，前端可查看 |
| F10 | 数据清理 | 后台定时归档/删除过期检测记录 |
| F11 | 安全帽扩展 | 增加 helmet 类别，规则表加 RequireHelmet |

### 检测端（Jetson）侧的任务边界

| Jetson/Python 负责 | C# 后端负责 |
|---|---|
| 采帧、YOLO 推理、跟踪 | 接收结果、存储 |
| ROI 判定、口罩判定 | 规则配置管理 |
| 本地语音提示 | 违规记录、处理闭环 |
| 上报 JSON | 统计报表、权限、多设备管理 |

> 判定放在边缘（Jetson 直接给"是否合规"），后端只负责存和处理——延迟低、断网可用。

---

## 三、系统架构与数据流

```
┌───────────────── Jetson Nano（边缘）─────────────────┐
│  USB摄像头 → GStreamer/OpenCV → YOLO(TensorRT) 推理    │
│  → 后处理(NMS/坐标) → ROI/口罩判定 → JSON              │
│  → Python 上报脚本（HTTP POST /api/detections）         │
│  → 定时心跳（HTTP POST /api/devices/heartbeat）         │
└──────────────────────────┬───────────────────────────┘
                           │ HTTP JSON（X-Api-Key）
┌──────────────────────────▼───────────────────────────┐
│            C# ASP.NET Core Web API（x64 服务器/PC）    │
│  ┌────────────────────────────────────────────────┐  │
│  │ Controller 层：接收上报 / 提供查询 / 登录        │  │
│  ├────────────────────────────────────────────────┤  │
│  │ Service 层：检测入库、违规生成、统计计算          │  │
│  ├────────────────────────────────────────────────┤  │
│  │ EF Core 数据层 → SQL Server                      │  │
│  └────────────────────────────────────────────────┘  │
│  后台任务：心跳超时检测、日统计、数据清理              │
└──────────────────────────┬───────────────────────────┘
                           │ REST API（JWT）
┌──────────────────────────▼───────────────────────────┐
│   前端/用户端（可用简单 HTML+JS 或 Swagger 先顶着）    │
└──────────────────────────────────────────────────────┘
```

**职责边界**
- Jetson/Python：只负责"识别 + 上报"，不做业务
- C#：负责"存储 + 业务规则 + 对用户提供 API"
- 前端：负责"展示 + 操作"

---

## 三·五、中间件：Redis 与消息队列（MQ）

> 你的两件套：**Redis 解决"查得快"**，**MQ 解决"顶得住"**。MVP 阶段可以先不加，后期优化时再加。

### 加入中间件后的完整数据流

```
Jetson ──POST /api/detections──► 上报接口（快速返回）
                                    │
                    ① Redis：设备鉴权缓存 / 限流 / 幂等去重
                                    │
                         ② MQ 入队 → 立即返回 202 Accepted
                                    │（异步，不阻塞设备）
                     ┌──────────────▼──────────────┐
                     │ BackgroundService 消费者      │
                     │ ③ 批量入库 → SQL Server      │
                     │ ④ 规则判断（规则来自 Redis）   │
                     │ ⑤ 生成违规 / 更新统计         │
                     └─────────────────────────────┘

查询接口：⑥ 热数据读 Redis 缓存 → 冷数据查 SQL Server
```

### Redis 用在哪（5 个真实场景）

| # | 用途 | 解决的问题 | Key 设计 |
|---|---|---|---|
| 1 | **设备鉴权缓存** | 每次上报都要查 Devices 表 → 缓存后免查库 | `device:{code}` → {apiKey, status}，TTL 5min |
| 2 | **规则缓存** | 每条检测要判规则 → 规则常读少变 | `rules:enabled` → JSON，规则变更时删除 |
| 3 | **上报限流** | 防单设备刷爆后端 | `rate:{deviceId}:{分钟}` 计数器 + EXPIRE |
| 4 | **幂等去重** | 设备重试导致重复入库 | `dedup:{deviceCode}:{timestamp}:{hash}` SETNX |
| 5 | **热数据查询缓存** | 最近检测/设备在线/今日统计反复查库 | `latest:detections`、`stats:today`，TTL 30s–5min |

### MQ 用在哪（3 个核心价值）

| # | 价值 | 说明 |
|---|---|---|
| 1 | **削峰** | 上报接口只入队就返回（202），消费速度自己控制 |
| 2 | **解耦** | 入库、违规生成、统计异步拆分 |
| 3 | **批量** | 消费者攒批一次 INSERT，吞吐提升 10 倍以上 |

### 选型（.NET 生态）

| 方案 | 特点 | 适合 |
|---|---|---|
| **Redis Streams** | 已装 Redis 就够，轻量 | ⭐ MVP 首选 |
| **RabbitMQ** | 经典可靠，MassTransit/EasyNetQ | 正式项目 |
| **CAP**（dotnetcore/CAP） | 集成 EF + MQ，自动重试+幂等 | ⭐ 面试加分 |

### 重要：别一上来就加

| 阶段 | 建议 |
|---|---|
| 阶段 1–3（打通上报/查询/设备） | **先不加**，单库单服务最稳 |
| 阶段 4（上报变多） | 加 Redis：设备/规则缓存、限流 |
| 阶段 6（性能优化） | 加 MQ：削峰 + 批量入库 |

---

## 四、数据库设计（SQL Server）

```sql
-- 用户
CREATE TABLE Users (
    Id            INT IDENTITY PRIMARY KEY,
    Username      NVARCHAR(50)  NOT NULL UNIQUE,
    PasswordHash  NVARCHAR(200) NOT NULL,
    Role          NVARCHAR(20)  NOT NULL DEFAULT 'User',  -- Admin/Leader/User
    CreatedAt     DATETIME2     NOT NULL DEFAULT SYSDATETIME()
);

-- 设备
CREATE TABLE Devices (
    Id              INT IDENTITY PRIMARY KEY,
    DeviceCode      NVARCHAR(50)  NOT NULL UNIQUE,   -- jetson-01
    Name            NVARCHAR(100) NOT NULL,
    Location        NVARCHAR(200) NULL,
    ApiKey          NVARCHAR(100) NOT NULL,
    Status          NVARCHAR(20)  NOT NULL DEFAULT 'Offline',
    LastHeartbeatAt DATETIME2     NULL,
    CreatedAt       DATETIME2     NOT NULL DEFAULT SYSDATETIME()
);

-- 检测框记录（原始 YOLO 结果）
CREATE TABLE Detections (
    Id          BIGINT IDENTITY PRIMARY KEY,
    DeviceId    INT           NOT NULL REFERENCES Devices(Id),
    TrackId     NVARCHAR(50)  NULL,
    LabelCode   NVARCHAR(50)  NOT NULL,      -- person / mask / no_mask
    Confidence  DECIMAL(5,4)  NOT NULL,
    BBoxX       INT NOT NULL, BBoxY INT NOT NULL,
    BBoxW       INT NOT NULL, BBoxH INT NOT NULL,
    ImagePath   NVARCHAR(300) NULL,
    DetectedAt  DATETIME2     NOT NULL,
    CreatedAt   DATETIME2     NOT NULL DEFAULT SYSDATETIME()
);
CREATE INDEX IX_Detections_Device_Time ON Detections(DeviceId, DetectedAt DESC);
CREATE INDEX IX_Detections_Label_Time  ON Detections(LabelCode, DetectedAt DESC);

-- 入口合规规则（每个入口一份）
CREATE TABLE ComplianceRules (
    Id              INT IDENTITY PRIMARY KEY,
    DeviceId        INT           NOT NULL REFERENCES Devices(Id),
    RequireMask     BIT           NOT NULL DEFAULT 1,   -- 必须戴口罩（当前唯一启用的规则）
    RequireHelmet   BIT           NOT NULL DEFAULT 0,   -- 预留：以后加安全帽
    MinConfidence   DECIMAL(5,4)  NOT NULL DEFAULT 0.5,
    ConfirmFrames   INT           NOT NULL DEFAULT 3,   -- 连续 N 帧确认
    Enabled         BIT           NOT NULL DEFAULT 1,
    UpdatedAt       DATETIME2     NOT NULL DEFAULT SYSDATETIME()
);

-- 违规记录（一个人一条）
CREATE TABLE Violations (
    Id              BIGINT IDENTITY PRIMARY KEY,
    DeviceId        INT           NOT NULL REFERENCES Devices(Id),
    TrackId         NVARCHAR(50)  NULL,
    ViolationTypes  NVARCHAR(100) NOT NULL,     -- 当前只有 no_mask
    PersonCount     INT           NOT NULL DEFAULT 1,
    ImagePath       NVARCHAR(300) NULL,
    Confidence      DECIMAL(5,4)  NULL,
    DetectedAt      DATETIME2     NOT NULL,
    Status          NVARCHAR(20)  NOT NULL DEFAULT 'Open',  -- Open / Handled / Ignored
    HandledBy       INT           NULL REFERENCES Users(Id),
    HandledAt       DATETIME2     NULL,
    Remark          NVARCHAR(300) NULL,
    CreatedAt       DATETIME2     NOT NULL DEFAULT SYSDATETIME()
);
CREATE INDEX IX_Violations_Status_Time ON Violations(Status, DetectedAt DESC);
CREATE INDEX IX_Violations_Device_Time ON Violations(DeviceId, DetectedAt DESC);
```

### 设计要点（面试可讲）
- **Detections 用 BIGINT 主键**：检测框记录量最大（每帧多人多属性）
- **复合索引 (DeviceId, DetectedAt)、(LabelCode, DetectedAt)**：覆盖时间范围查询（最左前缀）
- **违规与检测框分离**：检测框是原始数据，违规是业务事件，避免业务表被帧数据淹没
- **规则按设备配置**：不同入口可按需开关（预留了 helmet 字段扩展）
- **ApiKey 给设备用，JWT 给用户用**：两类身份分开

---

## 五、API 设计（端点清单）

### 认证
| 方法 | 路径 | 说明 | 鉴权 |
|---|---|---|---|
| POST | `/api/auth/login` | 登录，返回 JWT | 无 |
| GET | `/api/auth/me` | 当前用户信息 | JWT |

### 设备上报（Jetson 调用，用 ApiKey）
| 方法 | 路径 | 说明 |
|---|---|---|
| POST | `/api/detections` | 上报一批人员检测+口罩判定 |
| POST | `/api/devices/heartbeat` | 心跳 |

上报请求示例：
```json
{
  "deviceCode": "jetson-01",
  "timestamp": "2026-10-24T16:50:00+08:00",
  "persons": [
    {
      "trackId": "p-1001",
      "bbox": [102, 45, 210, 390],
      "attributes": { "mask": 0.92 },
      "compliant": true,
      "violations": [],
      "confidence": 0.92,
      "imageUrl": null
    },
    {
      "trackId": "p-1002",
      "bbox": [400, 60, 180, 380],
      "attributes": { "mask": null },
      "compliant": false,
      "violations": ["no_mask"],
      "confidence": 0.88,
      "imageUrl": "/snapshots/2026-10-24/p1002.jpg"
    }
  ]
}
```
响应：`201 Created`，`{ "saved": 2, "violationsCreated": 1 }`

### 查询（用户端，用 JWT）
| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/violations` | 违规列表：`?deviceId=&status=Open&from=&to=&page=1&pageSize=20` |
| GET | `/api/violations/{id}` | 违规详情 |
| PATCH | `/api/violations/{id}/handle` | 标记处理：`{ "status": "Handled", "remark": "已提醒" }` |
| GET | `/api/detections` | 原始检测框查询（分析用） |
| GET | `/api/devices` | 设备列表 + 在线状态 |
| GET | `/api/stats/compliance` | 合规率：`?date=2026-10-24` |
| GET | `/api/stats/daily` | 近 N 天趋势：`?days=7` |

---

## 六、项目结构与代码骨架

```
ObjectDetection.Api/
├── Controllers/  Auth / Detections / Violations / Devices / Stats
├── Models/       User, Device, Detection, Violation, ComplianceRule
├── Dtos/         DetectionReportDto, ViolationDto, PagedResult, LoginRequest
├── Data/         AppDbContext
├── Services/     DetectionService, ViolationService, DeviceService, StatsService
├── Middleware/   ExceptionMiddleware
├── Auth/         JwtTokenGenerator
├── BackgroundServices/ DeviceHeartbeatMonitor
└── Program.cs
```

**AppDbContext**
```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Detection> Detections => Set<Detection>();
    public DbSet<Violation> Violations => Set<Violation>();
    public DbSet<ComplianceRule> ComplianceRules => Set<ComplianceRule>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Detection>().HasIndex(d => new { d.DeviceId, d.DetectedAt });
        b.Entity<Detection>().HasIndex(d => new { d.LabelCode, d.DetectedAt });
        b.Entity<Violation>().HasIndex(v => new { v.Status, v.DetectedAt });
        b.Entity<Violation>().HasIndex(v => new { v.DeviceId, v.DetectedAt });
    }
}
```

**上报接口**
```csharp
[ApiController]
[Route("api/detections")]
public class DetectionsController : ControllerBase
{
    private readonly DetectionService _service;
    public DetectionsController(DetectionService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Report([FromBody] DetectionReportDto dto)
    {
        var apiKey = Request.Headers["X-Api-Key"].ToString();
        var result = await _service.SaveAsync(dto, apiKey);
        return Created("/api/detections", result);
    }
}
```

**DetectionService 核心逻辑**
```csharp
public async Task<ReportResult> SaveAsync(DetectionReportDto dto, string apiKey)
{
    // ① 设备鉴权（生产环境先查 Redis 缓存）
    var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceCode == dto.DeviceCode);
    if (device is null || device.ApiKey != apiKey)
        throw new UnauthorizedAccessException("设备未授权");

    var rule = await _db.ComplianceRules
        .FirstOrDefaultAsync(r => r.DeviceId == device.Id && r.Enabled);

    int violationCount = 0;

    foreach (var person in dto.Persons)
    {
        // ② 存原始检测框：人 + 口罩属性
        _db.Detections.Add(BuildDetection(device.Id, person, "person", 1.0, dto.Timestamp));
        if (person.Attributes.GetValueOrDefault("mask") is double maskConf)
            _db.Detections.Add(BuildDetection(device.Id, person, "mask", maskConf, dto.Timestamp));

        // ③ 生成违规：优先用边缘判定，否则按规则兜底
        var types = person.Violations.Count > 0
            ? person.Violations
            : EvaluateByRule(person.Attributes, rule);

        if (types.Count > 0)
        {
            _db.Violations.Add(new Violation
            {
                DeviceId = device.Id,
                TrackId = person.TrackId,
                ViolationTypes = string.Join(",", types),   // 如 "no_mask"
                PersonCount = 1,
                ImagePath = person.ImageUrl,
                Confidence = person.Confidence is double pc ? (decimal)pc : null,
                DetectedAt = dto.Timestamp,
                Status = "Open"
            });
            violationCount++;
        }
    }

    await _db.SaveChangesAsync();   // 一次 SaveChanges = 一个事务
    return new ReportResult { Saved = dto.Persons.Count, ViolationsCreated = violationCount };
}

private static Detection BuildDetection(int deviceId, PersonResultDto p,
    string label, double conf, DateTime at) => new()
{
    DeviceId = deviceId, TrackId = p.TrackId, LabelCode = label,
    Confidence = (decimal)conf,
    BBoxX = p.BBox[0], BBoxY = p.BBox[1], BBoxW = p.BBox[2], BBoxH = p.BBox[3],
    ImagePath = p.ImageUrl, DetectedAt = at
};

// 后端兜底判定（边缘没给 violations 时才用）
private static List<string> EvaluateByRule(Dictionary<string, double?> attrs, ComplianceRule? rule)
{
    var types = new List<string>();
    if (rule is null) return types;
    if (rule.RequireMask && attrs.GetValueOrDefault("mask") is null) types.Add("no_mask");
    return types;
}
```

---

## 七、开发路线（6 个阶段）

### 阶段 1：打通上报链路（2–3 天）
- Python 模拟上报脚本（不需要 Jetson 也能开发）：
```python
import requests, datetime, time, random

while True:
    has_mask = random.random() > 0.4
    payload = {
        "deviceCode": "jetson-01",
        "timestamp": datetime.datetime.now().isoformat(),
        "persons": [{
            "trackId": "p-1001",
            "bbox": [100, 50, 200, 300],
            "attributes": {"mask": 0.92 if has_mask else None},
            "compliant": has_mask,
            "violations": [] if has_mask else ["no_mask"],
            "confidence": 0.9 if has_mask else 0.85,
            "imageUrl": None
        }]
    }
    r = requests.post("http://localhost:5000/api/detections",
                      json=payload, headers={"X-Api-Key": "dev-key-001"})
    print(r.status_code, r.text)
    time.sleep(5)
```
- C#：建项目 → 建库（5 张表）→ 上报接口 → 脚本灌数据
- **验收**：脚本跑 1 分钟，Detections 和 Violations 都有数据

### 阶段 2：违规查询 + 分页（2–3 天）
- `GET /api/violations`（分页 + 按状态/时间过滤），DTO + 投影
- **验收**：Swagger 里能过滤、分页正确

### 阶段 3：设备管理 + 规则配置（2 天）
- 设备 CRUD、心跳、离线检测（超 60 秒 → Offline）
- 规则配置：是否要求口罩（可开关）
- **验收**：停心跳设备变 Offline；关掉 RequireMask 后不再生成违规

### 阶段 4：违规处理闭环（2–3 天）
- 列表 / 详情 / 标记处理（Open → Handled，记录处理人、备注、时间）
- **验收**：上报 no_mask → 列表出现 → 标记处理后状态正确

### 阶段 5：JWT 认证 + 权限（2 天）
- 登录、Token、[Authorize]、角色（管理员配规则，班组长处理违规）
- **验收**：不带 Token 返回 401

### 阶段 6：统计 + 优化 + 收尾（3–4 天）
- 合规率统计（按日/入口）、趋势
- 性能：执行计划验证索引、10 万条数据测分页
- README、架构图、接口文档
- **验收**：合规率正确；10 万条分页 < 200ms

---

## 八、Jetson 端与模型（已大幅简化：不用自己训练）

### 8.1 模型从哪来（1 天搞定）

口罩检测是成熟场景，**不需要自己采集数据、标注、训练**。三个现成来源，按优先级：

| 方案 | 做法 | 时间 |
|---|---|---|
| **A. 直接用现成模型/权重** ⭐ | GitHub / Ultralytics Hub / Roboflow Universe 上搜"face mask detection YOLO"，下载 .pt 或 .onnx 权重，验证可用 | 半天 |
| B. 公开数据集微调 | Roboflow 口罩数据集导出 YOLOv8 格式，Colab 上训练 50 epoch | 1 天 |
| C. 两阶段（极端情况） | person 检测 + 口鼻区域裁图 → 小分类模型判断有无口罩 | 2 天 |

**推荐 A**：先跑通；如果现成权重效果不好，再用 B 微调一次。

### 8.2 部署流程（Jetson）

```
① 拿权重：mask_yolov8n.pt / .onnx
② 验证：在 PC 上用 ultralytics 跑几张图，确认能检出 mask / no_mask
③ 导出：model.export(format="engine") 在 Jetson 上转 TensorRT（FP16）
④ 部署：Jetson 加载 engine，读摄像头推理
⑤ 后处理：person 框 → 判断口鼻区域附近是否有 mask 框 → ROI 过滤 → 多帧确认 → 上报
```

```python
# 推理示例（Jetson）
from ultralytics import YOLO
model = YOLO("mask_yolov8n.engine")     # TensorRT engine，FP16
results = model(frame, imgsz=640, conf=0.5)
# results[0].boxes → 按 cls 取 person / mask / no_mask
```

### 8.3 上报逻辑

```python
import requests, datetime

def report(persons, device_code="jetson-01", api_key="dev-key-001",
           url="http://192.168.1.100:5000/api/detections"):
    payload = {
        "deviceCode": device_code,
        "timestamp": datetime.datetime.now().isoformat(),
        "persons": persons
    }
    try:
        requests.post(url, json=payload,
                      headers={"X-Api-Key": api_key}, timeout=3)
    except requests.RequestException as e:
        print("上报失败，本地缓存稍后重试", e)
```

**注意**：批量合并上报（每 1–5 秒一次，别每帧 POST）。

### 8.4 防误报（必做）

- 连续 3–5 帧判定一致才算违规（`ConfirmFrames`）
- 同一 `trackId` 5 秒内只记一次（内存字典或 Redis SETNX）
- 置信度阈值 0.5 起，现场可调
- ROI 限定门口区域
- 侧脸/低头可能误判 → 摄像头正对通道，安装高度对准人脸

---

## 九、验收标准（做完自查）

- [ ] Jetson（或模拟脚本）能持续上报，Detections / Violations 落库
- [ ] 违规查询支持状态/时间过滤 + 分页，10 万条下 < 200ms
- [ ] 设备心跳正常，离线自动识别
- [ ] RequireMask 可开关
- [ ] 违规处理闭环（Open → Handled，记录处理人/备注）
- [ ] JWT 登录 + 接口鉴权 + 角色区分
- [ ] 合规率统计接口可用
- [ ] 全局异常处理、参数校验、日志
- [ ] README + 架构图 + Swagger 文档

## 十、简历一句话描述

> **AI 工厂入口口罩检测系统**：基于 Jetson Nano + YOLOv8（TensorRT 加速）的边缘检测端，实时识别进入车间的工人是否佩戴口罩；C# ASP.NET Core 后端提供设备管理、合规规则配置、违规记录处理闭环与合规率统计，SQL Server 存储，EF Core 数据访问，JWT 鉴权，索引优化支撑 10 万级记录分页查询。

面试可展开：
1. **为什么边缘计算**：视频不传云端，只传 KB 级 JSON
2. **怎么防误报**：多帧确认 + 跟踪去重 + ROI + 阈值
3. **后端业务价值**：多设备管理、规则可配、违规工单闭环、合规率统计

---

## 十一、常见坑

1. **SQL Server 不能装 Jetson**（ARM64 无官方支持）→ 后端跑 x64 PC/服务器；边缘部署只能 SQLite
2. **每帧 POST 会打爆后端** → 批量合并上报（1–5 秒一次）
3. **误报是最大坑** → 多帧确认 + 跟踪去重 + ROI
4. **侧脸/低头识别差** → 摄像头正对通道、高度对准人脸
5. **时间戳统一 UTC 存、展示转本地时区**
6. **Detections 别建太多索引** → 写入性能会掉
7. **图片别存数据库** → 存文件系统，表里存路径
8. **ApiKey 别写死代码** → 存数据库/配置，上报接口限流
9. **隐私合规**：抓拍涉及人脸，要有存储期限/访问权限/告知牌意识
10. **现成模型不是万能** → 先用 PC 验证效果再上 Jetson，别在板子上反复调试浪费时间

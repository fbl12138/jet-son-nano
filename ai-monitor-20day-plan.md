# AI 监控系统 —— 20 天实现计划（口罩检测版，每天 2 小时）

> 项目：工厂入口「口罩检测」+ C# Web API 后端
> 每天 2 小时：45min 学习/查资料 + 60min 动手 + 15min 复盘
> 20 天约 40 小时
> 目标：第 20 天能完整演示「Jetson 检测 → 上报 → 违规列表 → 处理 → 合规率统计」

---

## 进度同步（2026-10-31）

> 交接文档：`HANDOFF.md`（代码地图、环境事实、遗留问题、Day 8 起点）
> 依据：`ObjectDetection/` 源码 + 本机数据库实测；`dotnet build` 0 错误 0 警告。
> 最后更新：2026-10-31。
> 当前位置：**Day 14 完成**——10 万条压测通过（分页 21.6–24.1 ms），`DailyStats` 汇总与 30/180 天清理任务实测可用；下一步进入 Day 15 极简前端。

| 计划 | 状态 | 实际证据 |
|---|---|---|
| Day 1 环境与建库 | ✅ 完成 | .NET SDK 10.0.401；`ObjectDetectionDb` 5 张表 + 7 个索引；`InitialCreate` 已应用；`/swagger` 可访问 |
| Day 2 上报接口 + 违规生成 | ✅ 完成 | `POST /api/detections` 实测：正例 201 且 Detections 落 2 行（person + mask，mask 置信度 0.92）、反例 `violationsCreated = 1`、错误 ApiKey 返回 401；`bbox` 宽高顺序已修正（库中 180 / 380 核对无误） |
| Day 3 模拟上报 + 违规分页查询 | ✅ 完成 | 模拟脚本灌入 27 条违规；`GET /api/violations?pageSize=20&page=2` 返回 7 条且 `total = 27`、`totalPages = 2`，分页与边界正确；`ViolationDto` 投影已带出 `deviceName`（JOIN 生效，未泄露 `ApiKey`） |
| Day 4 设备管理 + 心跳 | ✅ 完成 | `POST /api/devices/heartbeat` 返回 200 并刷新 `LastHeartbeatAt`；停心跳后接口第 56 秒、数据库第 66 秒转 `Offline`（15 秒轮询 + 60 秒阈值，落在 60–75 秒预期内）；重启后 25 秒内恢复 `Online` |
| Day 5 合规规则配置 | ✅ 完成 | `GET/PUT /api/devices/{id}/rule` 实测：关 `RequireMask` + 带 `violations` 上报 → `violationsCreated = 0`，开回来 → `1`；阈值 0.95 时 `mask=0.6` → `1`，0.5 时 → `0`；`Enabled=false` → `0`；`minConfidence=1.5` → 400；设备 999 → 404；违规被抑制时 Detections 仍落库。核心修复：删掉 `person.Violations` 短路，改为 `EvaluateByRule` 单一判据 |
| Day 6 违规处理闭环 | ✅ 完成 | `GET /api/violations/43` 返回详情（`deviceName` / `personCount` / 处理三字段，处理前均为 null）；`PATCH .../43/handle` → 200 且 `status=Handled`、`handledAt` UTC；重复 PATCH → 409；待处理 `total` 43 → 42；`/999999` → 404；`handledBy:0` → 400；`remark` 301 字 → 400；库里四字段与响应一致（`LEN(Remark)=14`）。测试用数据已还原 |
| Day 7 JWT 认证 + 周复盘 | ✅ 完成 | `Users` 种子 admin(Admin)/operator(User)；登录签发 token（`sub`/`name`/`role`）；不带 token → 401，错密码 → 401，篡改 token → 401；operator 处理违规 / 改规则 → 403，读列表 / 读规则 → 200；admin 处理写入 `handledBy=1`（body 塞 999 无效）；设备上报 201、心跳 200 未受 JWT 影响；登录前后密码哈希不变 |
| Day 8 现成模型 PC 验证 | ✅ 完成 | `models/best.pt`（6.26 MB，MIT，YOLOv8n 口罩三分类）；`classes={0:with_mask,1:without_mask,2:mask_weared_incorrect}`；15 张实测：320 → 41/1/1（平均 75.5 ms）、640 → 45/8/1（平均 165.7 ms）；320 在远景人群漏检 6 张脸，**结论：推理尺寸用 640**；评估卡见 `docs/model-eval.md` |
| Day 9 判定逻辑开发 | ✅ 完成（逻辑层） | `scripts/judge.py`：类别映射 → ROI/最小尺寸过滤 → 多帧确认（3 帧）→ 5 秒去重 → persons；图片 15 张输出 28 条 / 唯一 ID 28 个 / 2 条 `no_mask`；合成视频 10 秒输出 2 条（戴）、1 条（不戴）。关键修复：ByteTrack 默认阈值 0.6 会丢掉低分违规框 → `configs/bytetrack_mask.yaml` 降到 0.2；无跟踪 ID 时不再逐帧输出。**真实录像待补** |
| Day 10 Jetson 部署 | ✅ 完成 | `best.pt`(6 MB) → ONNX(12 MB) → 容器内导出 `best-fp16.engine`(8.8 MB)；类别 `{with_mask, without_mask, mask_weared_incorrect}`；单帧基准 preprocess 7.2 / inference 11.5 / postprocess 4.9 ms；整机 **15.5 FPS**（采集 33 + 推理 23 + 编码 8 = 64 ms，三段串行）；对比旧 yolo11n FP32 的 12 FPS 提升 29% |
| Day 11 真机联调 | ✅ 完成 | 新增 `yolo_models/reporter.py`（ROI/尺寸过滤 → 跨帧去重 → 队列 → 每 2 秒批量 POST），`real_time_yolo.sh` 加两处调用；后端绑 `0.0.0.0:5027` 后 Jetson 返回 201；实测落库：Detections +29、Violations +24，`mask=0.503` 未违规、`mask=0.365` 违规，阈值生效 |
| Day 12 阈值调优 + 周复盘 | ✅ 完成 | 去重改为“每 track 一次”+ `TrackId` 加会话前缀；真机调参：`conf=0.25` 不戴 10 次漏 1 次、`conf=0.15` 全中；落库数据显示 0.15 轮最低违规置信度 **0.202** → 生产定 **0.20**；第 2 周复盘已写入计划 |
| Day 13 统计接口 | ✅ 完成 | 时间戳统一：存量 `DetectedAt` 减 8h 迁到 UTC（DiffMin 0）、`AppDbContext` 加 UTC 转换器（JSON 带 `Z`）、`SaveAsync` 显式 `ToUniversalTime`；新增 `GET /api/stats/compliance` + `/daily`；实测 `date=2026-11-02` → 93 人次 / 74 违规 / 合规率 0.2043，与 SQL 手查一致；修复 `[FromBody]` → `[FromQuery]` 导致参数被忽略的问题 |
| Day 14 性能优化 + 数据过期 | ✅ 完成 | 10 万条压测：分页 21.6–24.1 ms（<200ms）；深分页 SQL 74–87 ms，确认瓶颈是 OFFSET 而非索引；新增 `DailyStats`（`DateOnly` 日期）+ `DailyStatsWorker`（每小时回填 7 天 + 30/180 天分批清理）；实测清理任务删除 60 天前数据 |
| Day 15 极简前端 | 🟡 下一个 | 待做：单页 HTML（fetch 调 API）——违规列表 + 处理按钮 + 合规率卡片；演示时能“点一下处理，状态就变” |
| Day 16 – Day 20 | ⬜ 未开始 | 无抓拍图、文档、彩排、总结 |

### Day 2 收尾清单（已完成）

1. ✅ 新建 `Controllers/DetectionsController.cs`，暴露 `POST /api/detections`
2. ✅ `Program.cs` 注册 `IDetectionService`（并加了 Swashbuckle 10.3.1）
3. ✅ `UnauthorizedAccessException` → 401
4. ✅ 种子数据：`Jetson-01` / `dev-key-001` + 默认规则
5. ✅ `BuildDetection` 宽高顺序修正
6. ✅ Swagger 页面可访问

### Day 2 遗留（不阻塞，后面顺手处理）

- `timestamp` 目前客户端传 `+08:00` 本地时间，和 `CreatedAt = DateTime.UtcNow` 不一致；Day 13 做按天统计前要统一成 UTC
- ✅ `EvaluateByRule` 已接 `MinConfidence`（Day 5 完成）；`ConfirmFrames` 不在后端判，交 Day 9 设备端

### Day 5 收尾清单（已完成）

1. ✅ `Dtos/ComplianceRuleDto.cs`、`Dtos/UpdateRuleRequestDto.cs`（阈值用 `[Range(typeof(decimal),"0","1")]`）
2. ✅ `GET /api/devices/{id}/rule`：区分"设备不存在"与"未配规则"两种 404
3. ✅ `PUT /api/devices/{id}/rule`：upsert + `UpdatedAt = DateTime.UtcNow`
4. ✅ `DetectionService.Meets` + `EvaluateByRule` 接阈值；删掉 `person.Violations.Count > 0 ? ...` 短路
5. ✅ 12 项实测（开关 / 阈值 / Enabled / 400 / 404 / 落库不受影响），数据已清理还原

### Day 6 收尾清单（已完成）

1. ✅ `Dtos/ViolationDetailDto.cs`、`Dtos/HandleViolationRequestDto.cs`（`[Range(1,…)]` 挡漏传、`[MaxLength(300)]` 对齐库字段）
2. ✅ `GET /api/violations/{id}`：投影出 `deviceName`，处理三字段为 `null` 时照常返回
3. ✅ `PATCH /api/violations/{id}/handle`：`Open → Handled` 状态机，非 `Open` 返回 409
4. ✅ `HandledAt = DateTime.UtcNow`；`HandledBy` 已改为 Day 7 从 token 的 `sub` 取
5. ✅ 9 项实测（详情 / 处理 / 409 / 列表 total / 404 / 两个 400 / 落库），测试数据已还原

### Day 7 收尾清单（已完成）

1. ✅ `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.14；`appsettings.json` 配 `Jwt:Key/Issuer/Audience/ExpireMinutes`
2. ✅ `Program.cs`：`AddAuthentication().AddJwtBearer()` + `AddAuthorization()`，管道 `UseAuthentication()` 在 `UseAuthorization()` 之前；仅 Development 种账号
3. ✅ `POST /api/auth/login`：`PasswordHasher` 校验，用户不存在与密码错统一返回 401（防用户名枚举）
4. ✅ `[Authorize]`：违规查询/详情、设备列表、规则读取；`[Authorize(Roles="Admin")]`：处理违规、改规则
5. ✅ `handledBy` 从 token 的 `sub` 取，请求体不再传
6. ✅ 12 项实测（401 / 403 / 200 / 篡改 / 设备链路 / 哈希不变），测试数据已还原

### 第 1 周复盘（2026-10-31）

| 问题 | 结论 |
|---|---|
| 最有价值的设计决定 | **判定权收回服务端**（Day 5）：改规则即时生效，调阈值不用重刷 Jetson；设备只报“属性 + 置信度” |
| 哪些是权衡、不是绝对正确 | `ConfirmFrames` 只存不用（帧在设备端）；重复处理返回 409 而非幂等；`violations` 字段保留但不参与判定 |
| 重复踩的坑 | ① `DateTime` 读回 `Kind=Unspecified`（Day 5 埋、Day 6 现）② PowerShell 给 `curl.exe` 传内联 JSON 被剥引号 ③ 配置键名拼错（`Issur`）只表现为 401、不报原因 ④ `[Authorize(Roles=…)]` 放到 GET 而不是 PUT |
| 未偿还的技术债 | 无 git 仓库；时间戳未统一；处理接口读-改-写竞态；JWT Key 明文在 `appsettings.json`；无刷新 token |
| 下周最大风险 | Day 8 现成模型效果不达标；Day 10 TensorRT 导出；Day 11 断网重连；Day 12 误报调优 |

**答辩一句话**：系统分两层认证——设备用 `ApiKey` 走上报通道，人用 JWT 走管理通道；判定规则集中在服务端，设备只上报检测结果，所以政策调整不需要重新部署边缘端。

### 第 2 周复盘（2026-11-02）

| 问题 | 结论 |
|---|---|
| 用三方模型值不值 | **值**。省下数据集/标注/训练约 3–5 天，代价是不能讲模型训练、效果上限固定。项目价值靠系统层（规则、认证、联调）撑 |
| 部署难不难 | 步骤本身不难（有 AI 辅助写代码），**难在排错**：Docker 桥接不可用、旧容器占摄像头、engine 命名规则、后端只绑 `127.0.0.1` |
| 最隐蔽的 bug | ① ByteTrack 默认阈值 0.6 丢弃低分“没戴”框（漏报）② 去重写成 5 秒窗口导致重复上报 ③ track 碎裂产生多条违规 |
| 重复 trackId | 已修：`p-<会话时间戳>-<序号>`，重启不再重号 |
| 剩余风险 | track 碎裂阈值（`p["w"]*0.08`）待调；真实调参数据（四轮 conf）尚未采集 |

**答辩提醒**：不要说“因为有 AI 所以不难”。真正的贡献是**判断哪里不对**——上述三个 bug 不跑真机发现不了。

### 两个设计决定（已写进代码注释，别当 bug 修）

- **判定权在服务端**：设备上报的 `violations` 保留在契约里但不参与判定。改规则立即生效，不用重刷 Jetson；代价是断网时设备拿不到规则（Day 11 做"启动拉一次 + 本地缓存"）
- **`ConfirmFrames` 只存不用**：服务器没有帧缓冲，它的语义是"连续 N 帧"，只能在 Day 9 的 Jetson 端实现

### 两处与计划的偏差（不影响进度）

- 计划写 `.NET 8`，实际用的是 `.NET 10`（SDK 10.0.401），代码写法不变，照计划继续即可
- 目录里还没有 git 仓库，建议尽快 `git init` 并把 `bin/obj` 写进 `.gitignore`

---

## 这个版本为什么能 20 天完成

因为**不用自己训练模型**了：
- 口罩检测有现成开源权重（GitHub / Ultralytics Hub / Roboflow Universe）
- 不需要采集数据集、不需要标注、不需要等训练
- 模型相关工作量从原来的 6 天压缩到 **1–2 天**

省下来的时间用在：后端完善、调优、文档、演示彩排。

| 能完成 ✅ | 延后 ❌ |
|---|---|
| 后端全部核心接口 | 完整前端（用 Swagger + 极简 HTML） |
| 口罩检测（现成模型，较稳） | SignalR 实时看板 |
| 模拟上报 + 真实 Jetson 联调 | 图片云存储、多路摄像头 |
| 基础合规率统计 | 正式部署/CI |

---

## 每周主线

| 周 | 天数 | 主线 |
|---|---|---|
| 第 1 周 | D1–D7 | 后端骨架 + 核心业务（不依赖硬件） |
| 第 2 周 | D8–D12 | Jetson 接入（现成模型 + 判定 + 联调） |
| 第 3 周 | D13–D20 | 统计 + 优化 + 前端 + 文档 + 彩排 |

---

## 第 1 周：后端（不依赖 Jetson，先把业务跑通）

### Day 1｜环境与建库
**学习**：EF Core Code First 迁移；SQL Server Express 安装
**动手**
- 装 VS 2022 + .NET 8 SDK + SQL Server Express + SSMS
- 建 Web API 项目，装 EF Core SqlServer 包
- 定义 5 个实体（User/Device/Detection/Violation/ComplianceRule）
- Add-Migration + Update-Database，检查表和索引

**验收**
- [x] 5 张表 + 索引建好
- [x] 项目能跑，Swagger 页面打开

### Day 2｜上报接口 + 违规生成
**学习**：Controller 接收 JSON；DTO 绑定；SaveChanges 事务
**动手**
- 实现 `POST /api/detections`：接收 persons 数组
- 存 Detections（person + mask 各一条）
- 按 violations 生成 Violation 记录
- ApiKey 校验（查 Devices 表）

**验收**
- [x] Swagger 手动上报一条，两张表都有数据
- [x] 错误 ApiKey 返回 401

### Day 3｜模拟脚本 + 违规查询
**学习**：分页 Skip/Take；Where 动态条件；投影到 DTO
**动手**
- Python 模拟脚本（随机戴/不戴口罩，每 5 秒一条）
- `GET /api/violations`：按状态/时间过滤 + 分页
- 返回 `{ items, total, page, pageSize }`

**验收**
- [x] 脚本跑 1 分钟，违规列表有数据
- [x] 分页正确，total 准确

### Day 4｜设备管理 + 心跳
**学习**：BackgroundService；时间比较
**动手**
- 设备 CRUD
- `POST /api/devices/heartbeat`
- 后台服务每 30 秒检查：超 60 秒 → Offline

**验收**
- [x] 停脚本 60 秒后设备变 Offline
- [x] 重启后恢复 Online

### Day 5｜合规规则配置
**学习**：配置表；上报时读取规则
**动手**
- ComplianceRules CRUD（RequireMask 开关、MinConfidence、ConfirmFrames）
- 上报时读该设备规则裁决（设备上报的 `violations` 不参与，判定权在服务端）

**验收**
- [x] 关掉 RequireMask 后，上报无口罩不再生成违规
  - 实测：关规则 + 带 `violations:["no_mask"]` → `violationsCreated = 0`；开回来 → `1`

### Day 6｜违规处理闭环
**学习**：状态机；PATCH 语义
**动手**
- `GET /api/violations/{id}` 详情
- `PATCH /api/violations/{id}/handle`：改状态 + 处理人 + 备注 + 时间

**验收**
- [x] Open → Handled，三个字段正确落库
  - 实测：`status=Handled`、`handledBy=1`、`handledAt=2026-10-30T08:41:28Z`、`remark` 长度 14；重复处理 409；待处理 `total` 43 → 42

### Day 7｜JWT 认证 + 周复盘
**学习**：JWT 结构；[Authorize]；角色授权
**动手**
- `POST /api/auth/login` + 密码哈希
- 配置 JWT 签发/验证
- 查询接口加 [Authorize]；规则修改仅 Admin

**验收**
- [x] 不带 Token 返回 401；带 Token 正常
  - 实测：无 token 401、错密码 401、篡改 token 401；admin token 200；operator 调写接口 403
- [x] 复盘本周问题清单
  - 见上方「第 1 周复盘」

---

## 第 2 周：Jetson 接入（不用训练模型）

### Day 8｜拿到现成口罩模型并在 PC 验证
**动手**
- 找现成权重：GitHub 搜 "face mask detection YOLOv8"、Ultralytics Hub、Roboflow Universe
- 下载 .pt 或 .onnx，用 ultralytics 在 PC 上跑几张网图
- 确认能输出 mask / no_mask（或 face + mask 两类）

**验收**
- [x] PC 上推理出正确结果（截图留存）
  - 实测：15 张样本全部检出，带框图在 `runs\detect\runs\check\`
- [x] 记录：模型来源、类别名、输入尺寸、置信度表现
  - 见 `docs/model-eval.md`：640 输入、45 with / 8 without / 1 incorrect、平均 165.7 ms（CPU）

**如果现成权重效果差** → 用 Roboflow 口罩数据集在 Colab 微调 50 epoch（半天）

### Day 9｜判定逻辑开发
**动手**
- 写判定模块：ROI 过滤 → 判断人脸/口鼻区域附近是否有 mask 框 → 多帧确认（3 帧）→ 生成 persons
- 同一 trackId 5 秒只记一次
- 本地打印结果验证

**验收**
- [x] 真人走过：戴口罩 → compliant=true；不戴 → violations=["no_mask"]
  - 实测：图片模式 28 条 person，26 条 `compliant:true`、2 条 `no_mask`；合成视频 `mask_synth` 2 条 compliant、`nomask_synth` 1 条 `no_mask`（**用合成视频替代真实录像**）
- [x] 一人多次走过只产生一条违规
  - 实测：10 秒视频同一 trackId 只在 t≈0.1s 与 t≈5.1s 各报一次，5 秒窗口生效
- [ ] 真实摄像头录像验证（待 Day 10 上 Jetson 后补）

### Day 10｜部署到 Jetson（TensorRT）
**动手**
- 拷模型到 Jetson，导出 TensorRT engine（FP16）
- 跑通：读摄像头 → 推理 → 画框显示
- 测 FPS

**验收**
- [x] Jetson 单路 ≥ 10 FPS
  - 实测：**15.4–15.6 FPS**（采集 33 ms + 推理 23 ms + 编码 8 ms = 64 ms，串行）
- [x] 画面能看到检测框
  - 实测：`without_mask 0.83` / `0.62` 正常显示，FP16 生效

### Day 11｜真实联调（Jetson → 后端）
**动手**
- 上报逻辑接真实接口（批量 1–5 秒）
- 后端收到数据，违规入库
- 查库 + Postman 核对

**验收**
- [x] 真人走过 → 后端 Violations 出现记录
  - 实测：Detections 新增 29 条、Violations 新增 24 条（`TrackId = p-1…p-6`）；`mask=0.503` 不违规、`mask=0.365` 违规
- [ ] 断网重连不崩（异常处理）
  - 代码已用 `try/except` 兜住（失败只打 `[REPORT] failed`），**尚未实测**

### Day 12｜阈值调优 + 周复盘
**动手**
- 测试 20 次进出，统计误报/漏报
- 调置信度、ConfirmFrames、ROI
- 记录参数表

**验收**
- [x] 误报明显下降
  - 实测：`conf=0.25` 不戴 10 次漏 1 次；`conf=0.15` 全中；落库最低违规置信度 0.202 → 生产定 **0.20**（见 `docs/model-eval.md`）
- [x] 写下"哪些场景识别不了"（为答辩准备）
  - 夜景/逆光未测；远处小脸（<6% 画面宽）被 ROI 过滤；track 碎裂会把一个人拆成多条；口罩拉下巴能识别但置信度偏低

---

## 第 3 周：统计 + 优化 + 演示

### Day 13｜统计接口
**学习**：GROUP BY 聚合；日期函数；EF 分组
**动手**
- `GET /api/stats/compliance?date=`：检测人数、合规数、合规率
- `GET /api/stats/daily?days=7`：趋势

**验收**
- [x] 人工核对 1 天数据，合规率正确
  - 实测：`date=2026-11-02` → 93 人次 / 74 违规 / 合规 19 / 合规率 0.2043，与 SQL `(93-74)/93` 一致；`daily?days=3` 返回 3 条且日期连续

### Day 14｜性能优化
**动手**
- 灌 10 万条数据
- 执行计划检查索引命中
- 分页压测（深分页是否变慢，必要时 Keyset）
- 检查日志里的重复 SQL（N+1）

**验收**
- [x] 10 万条下违规分页 < 200ms
  - 实测：10 万条 Detections 下，违规分页 `page=1` 21.6 ms、`page=250` 24.1 ms；深分页 SQL（OFFSET 80000）74–87 ms
- [x] 无 N+1
  - 列表用投影一次查询；统计 `compliance` 3 条查询、`daily` 7 条（已知，可优化）

### Day 14 收尾（已完成）

1. ✅ `DailyStat` 实体 + `DailyStats` 表（`StatDate` 用 `DateOnly` → SQL `date`）
2. ✅ `DailyStatsWorker`：每小时重算最近 7 天（幂等 upsert）+ 清理（Detections 30 天 / Violations 180 天，`DELETE TOP (5000)` 分批）
3. ✅ 实测：汇总出 11-02（93/74/19）、10-31（26/24/2）；插一条 60 天前数据，重启后 Worker 自动删除
4. ✅ 性能结论：窄索引 `IX_Violations_DetectedAt_Id` 未被优化器采用（回表成本高），深分页瓶颈在 OFFSET 而非索引；Keyset 暂不实施

### Day 15｜极简前端
**动手**
- 单页 HTML（fetch 调 API）：违规列表 + 处理按钮 + 合规率卡片
- 时间紧就用 Swagger 演示

**验收**
- [ ] 页面上能看列表、点处理、看合规率

### Day 16｜抓拍图上传（可选，做不完跳过）
**动手**
- Jetson 违规时上传图片（multipart 或 base64）
- 后端存文件系统，表里存路径
- 前端能查看图片

**验收**
- [ ] 违规详情能看到抓拍图

### Day 17｜文档
**动手**
- README：背景、架构图、接口清单、部署步骤
- 整理：SQL 设计说明、优化前后对比、踩坑记录

**验收**
- [ ] 别人看 README 能跑起来

### Day 18｜彩排与改进
**动手**
- 完整走演示流程 3 次（戴/不戴/多人）
- 修掉演示时暴露的问题

**验收**
- [ ] 演示 3 次不翻车

### Day 19｜缓冲日（补作业）
**动手**
- 补齐任何未完成项（优先：统计、前端、文档）
- 准备 3 个面试问题的答案

**验收**
- [ ] 交付物清单全部打勾

### Day 20｜总结与演示
**动手**
- 录 1 分钟演示视频
- 写总结：20 天收获、不足、下一步
- 更新简历项目描述

**验收**
- [ ] 能用 2 分钟讲清整个系统
- [ ] 作品集完整

---

## 风险预案

| 风险 | 概率 | 应对 |
|---|---|---|
| 现成模型效果差 | 中 | 用 Roboflow 口罩数据集微调 50 epoch（半天）；或换 Ultralytics Hub 其他权重 |
| Jetson 跑不动 | 低 | 输入尺寸降到 320；降帧（每 2 帧推理一次） |
| 误报多 | 中 | ConfirmFrames 加到 5；置信度 0.6；ROI 缩小 |
| 后端某天卡住 | 中 | 后端与 Jetson 两条线互不阻塞，先做另一条 |
| 时间不够 | 中 | 砍：抓拍图上传、趋势统计、极简前端（用 Swagger） |

## 扩展功能备忘（主线跑通后再做）

### 工牌识别 —— 已决定延后

- **结论**：不进 MVP。主线（Day 1–12）跑通、联调稳定后再评估，不占当前任何时间
- **技术选型已定**：工牌印二维码 + 相机读码，**不选 RFID**
  - 二维码的关键优势：码出现在谁的人体框里就绑谁，不需要"最近 5 秒刷过哪张卡"的时间窗猜测，不装闸机也能做到一对一
  - RFID 仍要解决"刷卡的人 vs 画面里的人"如何对应，通常得配闸机，硬件联调成本另算
- **硬门槛（不满足就不开工）**：相机距人 1.5–2 m、1080p、二维码在画面里 ≥ 60 px；工牌用哑光硬卡套、码宽 ≥ 10 cm
- **数据模型**：新增 `Employees`（EmployeeNo / Name / BadgeCode / Department / Shift），`Violations` 加可空 `EmployeeId` 与 `(EmployeeId, DetectedAt)` 索引
- **兜底原则**：读不到码也照常记违规，`Remark` 标 `badge_unread`——不能因为认不出身份就放行
- **工作量**：PC 上半天验证，Jetson 联调半天到 1 天
- **执行位置**：Day 19 缓冲日，或 Day 12 联调后有余量时

## 每天时间模板

| 时段 | 内容 |
|---|---|
| 0–45min | 查资料/看文档（只查今天用的） |
| 45–105min | 动手（必须产出可运行的东西） |
| 105–120min | 复盘（今天完成什么、卡在哪、明天第一件事） |

## 交付物清单

- [ ] 可运行 C# Web API（Swagger 可演示）
- [ ] SQL Server 数据库（5 张表 + 索引）
- [ ] Jetson 推理 + 判定 + 上报
- [ ] 口罩检测模型（现成权重，不用训练）
- [ ] 极简前端 或 Swagger 演示
- [ ] README + 架构图 + 接口文档
- [ ] 演示流程脚本

## 一句提醒

**最大风险从"模型训练"变成了"现成模型效果"**——所以 D8 一定要先在 PC 上验证好再上 Jetson。如果 D8 发现现成权重不行，当天就转去微调，别拖到后面。

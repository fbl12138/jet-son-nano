# 交接文档 · 新会话从这里开始

> 生成于 2026-10-30；Day 14 通过验收后更新。已完成 Day 1–14，下一步是 Day 15。
> 配套文档：`ai-monitor-20day-plan.md`（20 天计划 + 顶部「进度同步」，**那份是进度的唯一事实来源**）
> 方案文档：`jetson-object-detection-project.md`（业务定义、库表设计、API 清单）

---

## 一、一句话现状

「工厂入口口罩检测」系统。后端 ASP.NET Core（.NET 10）+ EF Core + SQL Server 已经跑通：**设备上报 → 按规则裁决 → 违规落库 → 分页查询 → 设备心跳与离线判定 → 合规规则配置 → 违规处理闭环（Open → Handled）→ JWT 认证与角色授权 → 合规率统计 → 日汇总与数据过期**；口罩模型 PC 验证完成，判定模块已跑通；Jetson 已部署口罩模型（TensorRT FP16，15.5 FPS），并通过 HTTP 上报落库；时间戳已统一为 UTC。**前端未开始。**

**当前位置：Day 15（极简前端）未开工。**

---

## 二、环境事实（新会话别猜）

| 项 | 值 |
|---|---|
| 代码根目录 | `D:\Project\VS project\Jestnano` |
| 后端项目 | `ObjectDetection/`（`ObjectDetection.csproj`，net10.0） |
| SDK | .NET **10.0.401**（计划里写的是 .NET 8，实际用 10，写法一致） |
| 数据库 | 本机 SQL Server（服务 `MSSQLSERVER`），库名 `ObjectDetectionDb` |
| 连接串 | `Server=localhost;Database=ObjectDetectionDb;Trusted_Connection=True;TrustServerCertificate=True;` |
| 端口 | http `5027`，https `7205`（见 `Properties/launchSettings.json`） |
| Swagger | `http://localhost:5027/swagger`（Swashbuckle 10.3.1） |
| Python | 3.14，**命令用 `py`**；`python` 指向 Microsoft Store 空壳，无输出 |
| 设备种子 | `DeviceCode = 'Jetson-01'`（大写 J）、`ApiKey = 'dev-key-001'`、`Location = A栋1楼东门` |
| 管理账号 | `admin` / `Admin@123`（Role=Admin）、`operator` / `User@123`（Role=User）；仅 Development 首次启动自动种（`Users` 非空则跳过） |
| JWT 配置 | `appsettings.json` → `Jwt:Key`（≥32 字节）/`Issuer=objectDetection`/`Audience=objectDetectionClient`/`ExpireMinutes=120` |
| 局域网联调 | PC `192.168.31.95`、Jetson `192.168.31.219`（用户 `admin1`）；后端必须绑 `0.0.0.0`：`dotnet run --urls http://0.0.0.0:5027`，否则 Jetson 访问返回 `000`（默认只监听 `127.0.0.1`） |

**这个目录下 DSH 的受限命令行起不来**（Windows 权限导致 `grantWrite` 失败），跑 `pwsh` 需要 `danger-full-access`；文件读写工具正常。

---

## 三、代码地图

| 文件 | 职责 | 状态 |
|---|---|---|
| `Models/{User,Device,Detection,Violation,ComplianceRule}.cs` | 5 个实体 | 完成 |
| `Data/AppDbContext.cs` | 索引、字段长度、精度 | 完成 |
| `Migrations/*InitialCreate*` | 5 表 + 7 索引，已 `Update-Database` | 已应用 |
| `Services/DetectionService.cs` | 上报核心：ApiKey 校验 → 写 Detections → `Meets` + `EvaluateByRule` 按规则表裁决（设备上报的 `violations` 不参与） | 完成，已接 `MinConfidence` |
| `Program.cs` | DI、Swagger、JWT 认证/授权（`MapInboundClaims=false`、`RoleClaimType="role"`）、仅开发环境的账号种子、心跳后台服务 | 完成 |
| `Controllers/AuthController.cs` | `POST /api/auth/login`：`PasswordHasher` 校验 → 签发 HS256 token（`sub`/`name`/`role`） | 完成 |
| `Controllers/DetectionsController.cs` | `POST /api/detections`，401 处理；**只认 `X-Api-Key`** | 完成 |
| `Controllers/ViolationsController.cs` | `GET /api/violations`（过滤 + 分页 + 投影）、`GET /api/violations/{id}`（详情）、`PATCH /api/violations/{id}/handle`（`Open → Handled`，非 Open 返回 409）；类级 `[Authorize]`，处理仅 Admin，`handledBy` 取自 token | 完成 |
| `Controllers/DevicesController.cs` | `POST /api/devices/heartbeat`（`[AllowAnonymous]` + ApiKey）、`GET /api/devices`、`GET/PUT /api/devices/{id}/rule`（PUT 为 upsert，仅 Admin） | 完成，缺设备 POST/PUT |
| `BackgroundServices/DeviceHeartbeatMonitor.cs` | 15 秒轮询，超 60 秒置 `Offline` | 完成并实测 |
| `Dtos/*` | `PersonResultDto`、`DetectionReportDto`、`ReportResult`、`PagedResult<T>`、`ViolationDto`、`ViolationDetailDto`、`HandleViolationRequestDto`、`DeviceDto`、`HeartbeatRequestDto`、`ComplianceRuleDto`、`UpdateRuleRequestDto`、`LoginRequestDto` | 完成 |
| `scripts/mock_jetson.py` | 模拟 Jetson：检测每 5 秒、心跳每 20 秒（双线程） | 完成 |
| `scripts/check_model.py` | 批量跑图，打印 `label/conf/bbox`，Day 9 判定模块的雏形 | 完成 |
| `scripts/judge.py` | 判定模块：类别映射 → ROI/尺寸过滤 → 多帧确认 → 5 秒去重 → persons JSON | 完成（逻辑层） |
| `scripts/make_synthetic_video.py` | 无真实录像时，用数据集图片合成 10 秒视频验证时序逻辑 | 完成 |
| `configs/bytetrack_mask.yaml` | 低阈值 ByteTrack（`new_track_thresh=0.2`），避免丢低分违规框 | 完成 |
| `models/best.pt` | 口罩 YOLOv8n 三分类权重（6.26 MB，MIT，别提交进 git） | 已就绪 |
| `docs/model-eval.md` | 模型评估卡：来源、类别、320/640 实测、结论 | 完成 |
| `WeatherForecast*.cs` | 模板残留 | 可删，不影响 |

---

## 四、数据现状

- `Devices`：1 行 —— `Jetson-01` / `dev-key-001` / A栋1楼东门
- `ComplianceRules`：`DeviceId = 1`，`RequireMask = 1`，`MinConfidence = 0.5000`，`ConfirmFrames = 3`，`Enabled = 1`（Day 5 验收后已恢复默认）
- `Violations`：**44** 条 —— 43 条 `Open` + 1 条 `Handled`（Id 44，用户手工测试留下的）
- `Detections`：**178** 行
- `Users`：**2 行** —— `admin` / `Admin@123`（Admin）、`operator` / `User@123`（User）

重建种子数据：

```sql
USE ObjectDetectionDb;
INSERT INTO Devices (DeviceCode, Name, Location, ApiKey, Status, LastHeartbeatAt, CreatedAt)
VALUES ('Jetson-01', N'一号车间入口', N'A栋1楼东门', 'dev-key-001', 'Offline', NULL, SYSDATETIME());

INSERT INTO ComplianceRules (DeviceId, RequireMask, RequireHelmet, MinConfidence, ConfirmFrames, Enabled, UpdatedAt)
SELECT Id, 1, 0, 0.5, 3, 1, SYSDATETIME() FROM Devices WHERE DeviceCode = 'Jetson-01';
```

---

## 五、已经实测通过的验收（可直接引用）

| Day | 验收 | 实测结果 |
|---|---|---|
| 2 | 上报落库 + 401 | 正例 201、`saved=1 / violationsCreated=0`、Detections 落 2 行（person + mask 0.92）；反例 `violationsCreated=1`；错误 ApiKey 401 |
| 3 | 脚本灌数据 + 分页 | 脚本 5 分钟灌入 27 条违规；`?pageSize=20&page=2` 返回 7 条，`total=27`、`totalPages=2` |
| 4 | 心跳与离线 | 心跳 200；停脚本后**接口第 56 秒、数据库第 66 秒**转 `Offline`；重启 25 秒内恢复 `Online` |
| 5 | 规则开关与阈值 | 关 `RequireMask` + 带 `violations:["no_mask"]` → `violationsCreated = 0`；开回来 → `1`；阈值 0.95 + `mask=0.6` → `1`，0.5 → `0`；`Enabled=false` → `0`；`minConfidence=1.5` → 400；设备 999 → 404；违规被抑制时 Detections 仍落库（12 项全过） |
| 6 | 违规处理闭环 | `GET /api/violations/43` → 200，处理三字段为 `null`；`PATCH .../43/handle` → 200，`status=Handled`、`handledAt=08:41:28Z`；重复 PATCH → 409；待处理 `total` 43 → 42；`/999999` → 404；`handledBy:0` → 400；`remark` 301 字 → 400；库里四字段一致（9 项全过） |
| 7 | JWT 认证与角色授权 | 无 token 401、错密码 401、篡改 token 401；admin token 200；operator 处理违规/改规则 → 403、读列表/读规则 → 200；admin 处理写入 `handledBy=1`（body 塞 999 无效）；设备上报 201、心跳 200 均不受 JWT 影响；登录前后密码哈希不变（12 项全过） |

---

## 六、已知问题与遗留（按重要度）

1. **时间戳混用**：客户端传 `+08:00` 本地时间，`CreatedAt` / `HandledAt` 用 `DateTime.UtcNow`。另外 EF 从 `datetime2` 读出来的 `DateTime` 是 `Kind=Unspecified`，JSON 不带 `Z`（同一时刻 PATCH 响应带 `Z`、GET 详情不带），前端会差 8 小时。Day 13 统一。
2. **处理接口有读-改-写竞态**：两个并发 PATCH 可能都读到 `Open`、都成功。409 只挡顺序重复。彻底修用 `UPDATE ... WHERE Status='Open'` 或 `RowVersion`（Day 14 或缓冲日）。
3. **JWT Key 明文**：`appsettings.json` 里的开发密钥不能进生产；生产用环境变量/Key Vault。另外没有刷新 token，2 小时到期只能重新登录。
4. **设备 CRUD 只做了读**，没有 `POST /api/devices`、`PUT`（演示够用，可延后）。
5. 没有 git 仓库 —— 建议尽快 `git init` + `.gitignore`（`bin/`、`obj/`、`.vs/`、`__pycache__/`）。
6. `WeatherForecast.cs` 与对应 Controller 是模板残留。
7. `GET /api/devices` 没有分页（设备数量少，可先不管）。
8. `ConfirmFrames` 只在库里存着、判定逻辑在 Day 9 的 Jetson 端 —— **不是 bug，别去服务端补**。

## 七、踩过的坑（新会话容易再犯）

- `PersonResultDto.BBox` 加了 `[JsonPropertyName("bbox")]`，**JSON 字段名是 `bbox`**（小写）；不加会变成 `bBox`
- `bbox` 顺序是 `[x, y, w, h]`，早期代码把 W/H 写反过，已修
- `DeviceCode` 在 SQL 里不区分大小写（`jetson-01` 能查到 `Jetson-01`），但 `ApiKey` 是 C# 里比较，**区分大小写**
- `Skip/Take` 前必须有 `OrderBy`，否则 SQL Server 报 `Incorrect syntax near 'OFFSET'`
- `CountAsync` 必须放在 `Skip/Take` 之前
- `BackgroundService` 里不能注入 `AppDbContext`（Scoped），要用 `IServiceScopeFactory`；`ExecuteAsync` 里必须 `try/catch`，否则异常会停掉整个应用
- 直接返回实体 `Violation` 会把 `Device.ApiKey` 序列化出去 —— 必须投影到 DTO
- SQL 里算“离线几秒”要用 `GETUTCDATE()`，不是 `SYSDATETIME()`（存的是 UTC）
- 规则 `PUT` 是**整份替换**：漏传 `enabled` 会被默认成 `false`，漏传 `minConfidence` 会变 `0.5` —— 改规则必须传全 5 个字段
- 阈值校验要用 `[Range(typeof(decimal), "0", "1")]`；`[Range(0, 1)]` 的 int 重载作用在 decimal 上会走整数转换
- PowerShell 给 `curl.exe` 传内联 JSON 会被剥引号（后端报 `'x' is an invalid start of a property name`）；用 `--data-binary "@文件.json"` + `Set-Content -Encoding utf8`
- `SaveAsync` 查规则时带 `Enabled`：规则停用等价于“没配规则”，不再生成违规
- EF 从 `datetime2` 读回的 `DateTime` 是 `Kind=Unspecified`：同一个 `HandledAt`，PATCH 响应带 `Z`、GET 详情不带 `Z`，前端会差 8 小时 —— Day 13 统一
- **ByteTrack 默认 `new_track_thresh=0.6`**：置信度低于 0.6 的框不会建轨迹，而“没戴口罩”的框常只有 0.19–0.46 —— 直接用默认值会**漏掉违规**。必须用 `configs/bytetrack_mask.yaml`（0.2）
- **视频模式下没有 `trackId` 不能输出**：跟踪器一丢 ID，代码若回退到逐帧输出，10 秒能刷 17 条；判定层要跳过无 ID 的框
- 口罩模型对“没戴”的置信度可低至 **0.19**，`conf=0.25` 会漏检；生产阈值 Day 12 用真实数据定（可能要 0.15–0.20）
- **这台 Jetson 的 docker 默认桥接网络不可用**（`iptables raw` 表缺失，报 `failed to create endpoint`），所有 `docker run` 必须加 `--network host`；`real_time_yolo.sh` 已带
- 摄像头是 **CSI 的 F306**（`fzcama` → `/dev/video0`、`fzcamb` → `/dev/video4`），不是 USB；只出 `UYVY`，分辨率 640x514–3840x2160
- TensorRT engine 的命名规则：管线按 `$MODEL_DIR/$MODEL$ENGINE_SUFFIX.engine` 找文件，`fp16` 时后缀是 `-fp16`，所以导出后要改名（`best.engine` → `best-fp16.engine`）
- `precision.txt` 是 root 属主时，普通用户改不动；先 `sudo chown -R admin1:admin1 yolo_models`

---

## 八、Day 15 的具体起点

**目标**：一个单页 HTML，能**登录 → 看违规列表 → 点处理 → 看合规率**。演示时这一页就是“产品”。

**现状**：
- 后端接口齐全：`/api/auth/login`、`/api/violations`、`/api/violations/{id}/handle`、`/api/stats/compliance`、`/api/stats/daily`
- 后端已绑 `0.0.0.0:5027`；静态文件还没开（需要 `app.UseStaticFiles()` + `wwwroot/`）
- 账号：`admin/Admin@123`（Admin）、`operator/User@123`（User）

**建议实现顺序**：
1. `wwwroot/index.html` + `Program.cs` 加 `app.UseStaticFiles();`
2. **登录区**：两个输入框 + 按钮 → `POST /api/auth/login` → token 存 `sessionStorage`
3. **列表区**：`GET /api/violations?status=Open&page=1&pageSize=10` → 渲染表格（Id/设备/类型/时间/状态）
4. **处理按钮**：`PATCH /api/violations/{id}/handle`，body 只传 `remark`，成功后刷新列表
5. **统计卡片**：`GET /api/stats/compliance` + `/api/stats/daily?days=7`，显示“今日人次/违规/合规率”
6. **筛选**：状态下拉（Open/Handled）+ 分页上一页/下一页

**注意**：
- 同源访问（页面也由 5027 提供），**不需要配 CORS**
- 时间显示：接口返回 UTC（带 `Z`），页面用 `new Date(x).toLocaleString('zh-CN', { timeZone: 'Asia/Shanghai' })`
- 401 时清空 token 回到登录框；403 提示“没有权限”
- 不要把管理员密码写进 JS 提交（演示可预填输入框，但别硬编码在 fetch 里）
- 没有构建工具，纯 `fetch` + 原生 DOM，**不要引入 Vue/React**（时间不够）

**验收**：
- 浏览器打开 `http://localhost:5027/`，登录取到 token
- 列表显示 Open 违规；点“处理”后该条从列表消失、`status` 变 Handled
- 卡片上的合规率与 `/api/stats/compliance` 一致

---

## 九、快速恢复现场

```powershell
# 后端（窗口 1）
cd "D:\Project\VS project\Jestnano\ObjectDetection"
dotnet run

# 模拟设备（窗口 2）
cd "D:\Project\VS project\Jestnano"
py scripts\mock_jetson.py
```

```powershell
# 常用查库
sqlcmd -S localhost -E -W -d ObjectDetectionDb -Q "SELECT TOP 5 Id, ViolationTypes, Status FROM Violations ORDER BY Id DESC;"
sqlcmd -S localhost -E -W -d ObjectDetectionDb -Q "SELECT DeviceCode, Status, LastHeartbeatAt FROM Devices;"
```

```powershell
# 常用接口（除登录/设备上报/心跳外，都要 Authorization）
$t = (Invoke-RestMethod -Method Post -Uri http://localhost:5027/api/auth/login `
  -ContentType 'application/json' -Body '{"username":"admin","password":"Admin@123"}').token

curl.exe -s http://localhost:5027/api/devices -H "Authorization: Bearer $t"
curl.exe -s "http://localhost:5027/api/violations?status=Open&pageSize=5&page=1" -H "Authorization: Bearer $t"
curl.exe -s http://localhost:5027/api/devices/1/rule -H "Authorization: Bearer $t"
# 改规则（PUT 全量，5 个字段都要传；仅 Admin）：body 存成 rule.json，再 --data-binary "@rule.json"

curl.exe -s http://localhost:5027/api/violations/44 -H "Authorization: Bearer $t"
# 处理违规（仅 Admin；body 只需 remark）
Set-Content handle.json '{"remark":"已提醒"}' -Encoding utf8
curl.exe -s -X PATCH http://localhost:5027/api/violations/44/handle -H "Content-Type: application/json" -H "Authorization: Bearer $t" --data-binary "@handle.json"

# 设备上报/心跳仍走 ApiKey，不需要 token
curl.exe -s -X POST http://localhost:5027/api/devices/heartbeat -H "Content-Type: application/json" -H "X-Api-Key: dev-key-001" --data-binary "@hb.json"
```

---

## 十、协作约定（给接手的 AI）

- 用户**自己敲代码**，要的是「写代码的顺序 + 每段为什么 + 逐行注释讲解」，**不要直接改他的源文件**（除非他明确说“你来改/你来写”）
- 全程中文；解释要给类比和“为什么这么设计”，不只是“怎么写”
- 每完成一天，更新 `ai-monitor-20day-plan.md` 顶部「进度同步」表格和各天验收勾选
- 测试可以由 AI 直接做（跑 `curl.exe` / `sqlcmd` 验证），做完把**实测数字**写进计划
- 用户会主动问“能不能做 X”（工牌识别、刷卡等），这类扩展已经决定延后，见计划里「扩展功能备忘」一节，别把主线带偏
- **每条命令都要先说明“这条在干什么、跑完看什么”**，不要只甩命令块（用户 2026-10-31 明确要求）
- 命令默认在项目根目录 `D:\Project\VS project\Jestnano` 下执行；涉及相对路径时要提醒先 `cd`

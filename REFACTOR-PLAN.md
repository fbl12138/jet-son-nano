# Jestnano / ObjectDetection 后端改造计划

> 目标：把这个"能跑的学习项目"改造成"能写进简历、能撑 15 分钟面试深聊"的工程作品。
> 定位：**补齐工程化，不推倒重来**——现有代码质量已经在中上水平，别重构掉自己写对的东西。
> 用法：按阶段推进，每个步骤都有「验收标准」，做到验收通过再进下一步。每步都能单独提交。

---

## 进度（截至 2026-11-13）

| 阶段 | 内容 | 状态 |
|---|---|---|
| 0 | git 基线 · 依赖漏洞 · 清理模板 | ✅ |
| 1 | 测试工程 · 全局异常处理 · Serilog | ✅ |
| 2.1 | `MonitoringOptions` 配置集中 | ✅ |
| 2.2 | 状态枚举化（EF 值转换 + JSON 转换器保契约） | ✅ |
| 2.3 | 抽取 `IDeviceService`（127 → 51 行） | ✅ |
| 2.4 | 抽取 `IViolationService`（110 → 66 行） | ✅ |
| 2.5 | 抽取 `IStatsService`（78 → 26 行） | ✅ |
| 2.6 | `TimeProvider` + `DailyWindow` / `ComplianceMath` 纯函数 | ✅ |
| 3.1 | 时区错位修复（D1） | ✅ |
| 3.2 | 复用 `DailyStats` 聚合表（D2） | ✅ |
| 3.3 | 修复 N+1（D4） | ✅ |
| 3.4 | `ConfirmFrames` 职责收敛（D3，已删除并记 ADR） | ✅ |
| 3.5 | ApiKey 哈希 + 恒定时间比较（D7） | ✅ |
| 4 | 健康检查 · 限流 · Docker · README | ✅ |
| 4.5 | CI/CD：GitHub Actions（编译/测试/前端/镜像）+ GHCR 镜像发布 | ✅ |

**测试**：33 个（`ComplianceEvaluator` 9 · `DailyWindow` 4 · `ComplianceMath` 5 · `StatsClock` 3 · `StatsService` 5 · `ApiKeyHasher` 7）

> 3.1 的修复方法是把「今天」交给 `StatsClock`（由 `TimeProvider` 驱动，固定上海时区），
> 并用 `FixedTimeProvider` 在测试里钉住「UTC 服务器 + 上海次日」这个场景——
> 因为本机是 +08:00，这个 bug 在开发机上永远复现不了。

---

## 0. 先看体检结果（我实测的，不是猜的）

| 项目 | 结果 |
|---|---|
| 编译 | ✅ 通过，0 错误 |
| 规模 | 32 个 C# 文件，1086 行（不含迁移） |
| 技术栈 | .NET 10 / EF Core 10 + SQL Server / JWT / Vue3 + Vite + TS + Element Plus |
| 测试 | ❌ 全局只有一个 `.csproj`，**没有任何测试** |
| 版本控制 | ❌ 根目录**没有 `.git`** |
| 依赖安全 | ⚠️ `System.Security.Cryptography.Pkcs 9.0.11` 有 **High** 级漏洞（[GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx)） |
| 前端 | ✅ 已分离（`web/`），Vite 代理 `/api` → `:5027`，token 存 `sessionStorage` |

**已经做对、不要动的地方**（这些都是加分项，别改坏）：

- `AppDbContext` 里的索引设计（`DeviceId+DetectedAt`、`LabelCode+DetectedAt`）、字段长度、`HasPrecision`
- 全局 UTC 存储 + `ValueConverter` 统一 `DateTimeKind`
- `AsNoTracking()` 用在该用的地方
- `PasswordHasher<User>`（PBKDF2，不是 MD5）+ `SuccessRehashNeeded` 处理
- `BackgroundService` 用 `IServiceScopeFactory` 建 scope（这是正确写法，很多人会踩坑）
- `DevicesController.Get` 里"EF 算不了时间相减，所以拉回内存算"——这个判断是对的
- `ViolationsController` 的 `page/pageSize` 钳制、`Status != "Open"` 的并发保护意识

---

## 1. 缺陷清单

编号固定，后面的步骤直接引用。

| 编号 | 缺陷 | 位置 | 严重度 |
|---|---|---|---|
| **D1** | `/api/stats/daily` 用 `DateTime.Today`（服务器本地日），而同文件 `Compliance` 用上海时区。**服务器不在 +08:00 时日期会错一天**——容器化部署到 UTC 后必然踩到 | `StatsController.cs:59` vs `:38` | 🔴 高 |
| **D2** | `DailyStats` 表**只写不读**：`DailyStatsWorker` 每小时回填，但 `/api/stats/daily` 完全没用它，实时 `CountAsync` 循环最多 62 次查询 | `StatsController.cs:60-73` | 🔴 高 |
| **D3** | `ConfirmFrames` 是**死字段**：实体、DTO、迁移、校验都有，但 `EvaluateByRule` 从不使用 | `DetectionService.cs:34-43` | 🟠 中 |
| **D4** | `DailyStatsWorker` 按设备循环逐个 `FirstOrDefaultAsync`（N+1） | `DailyStatsWorker.cs:62` | 🟠 中 |
| **D5** | 只回填 `i=1..7`，**今天的数据永远不统计**（若是有意为之，需注释说明） | `DailyStatsWorker.cs:42` | 🟠 中 |
| **D6** | 心跳超时 60s、间隔 15s **硬编码在两处**，双份真相 | `DevicesController.cs:17`、`DeviceHeartbeatMonitor.cs:14-15` | 🟠 中 |
| **D7** | `ApiKey` 明文入库，且用 `!=` 比较（非恒定时间） | `DevicesController.cs:42`、`DetectionService.cs:47` | 🟠 中 |
| **D8** | JWT 密钥明文在 `appsettings.json` | `appsettings.json:9` | 🟠 中 |
| **D9** | 无全局异常处理，只有一处 try/catch；`DetectionService` 用 `UnauthorizedAccessException` 表达业务"设备未授权" | `Program.cs:69`、`DetectionService.cs:48` | 🟠 中 |
| **D10** | 状态用字符串：`"Online"/"Offline"`、`"Open"/"Handled"` 散落各处 | 多处 | 🟡 低 |
| **D11** | 模板残留：`WeatherForecast.cs`、`WeatherForecastController.cs`、`.http` 仍指向 weatherforecast | — | 🟡 低 |
| **D12** | 无日志框架、无健康检查、无限流 | — | 🟡 低 |
| **D13** | `Detection` 没有 `Device` 导航属性（`Violation` 有），不一致 | `Detection.cs` | 🟡 低 |
| **D14** | 依赖漏洞 `Pkcs 9.0.11` | — | 🔴 高 |

---

## 阶段 0：先建安全网（约 2 小时）

> 没有这一步，后面任何改动都可能把能跑的版本改坏，且无法回退。

### 步骤 0.1 初始化 git 并锁定基线

```bash
cd "D:\Project\VS project\Jestnano"
git init
```

建 `.gitignore`（**放根目录**，覆盖前后端）：

```gitignore
# .NET
bin/
obj/
*.user
*.suo

# 发布产物
deploy/

# 前端
web/node_modules/
web/dist/
web/node_modules/.tmp/

# 日志/临时
*.log
logs/
```

然后：

```bash
git add .
git commit -m "chore: baseline before refactor"
git tag baseline
```

**验收**：`git status` 干净；`git show --stat baseline` 里**不含** `bin/ obj/ node_modules/ deploy/`。
**面试素材**：动手前先打 tag 锁基线，出问题能一键回退——这是工程习惯的第一个证据。

### 步骤 0.2 修依赖漏洞（D14）

```bash
dotnet list ObjectDetection/ObjectDetection.csproj package --vulnerable --include-transitive
```

这个包是 `Microsoft.Data.SqlClient` 传递进来的。处理顺序：

1. 先试 `dotnet list ... package --outdated --include-transitive`，看 `Microsoft.Data.SqlClient` 有没有新版
2. 升级 `Microsoft.EntityFrameworkCore.SqlServer` / `Microsoft.Data.SqlClient` 到最新补丁版
3. 如果原库还没修，在 csproj 里显式加一条 `PackageReference` 把 `System.Security.Cryptography.Pkcs` **钉到已修复版本**（具体版本号以上一步 `--outdated` 或 [NuGet 页面](https://www.nuget.org/packages/System.Security.Cryptography.Pkcs) 为准，别照抄我这里的占位值）

```xml
<PackageReference Include="System.Security.Cryptography.Pkcs" Version="<已修复版本>" />
```

**验收**：上面那条 `--vulnerable` 命令输出为空，且 `dotnet build` 通过。
**面试素材**：能说出"传递依赖漏洞怎么发现、为什么不能只升直接依赖"。

### 步骤 0.3 删模板残留（D11）

删除：

- `ObjectDetection/WeatherForecast.cs`
- `ObjectDetection/Controllers/WeatherForecastController.cs`
- 清掉 `ObjectDetection.http` 里的 weatherforecast，换成你自己的接口示例

```http
@host = http://localhost:5027

### 登录
POST {{host}}/api/auth/login
Content-Type: application/json

{ "username": "admin", "password": "Admin@123" }

### 设备列表（先跑登录，把 token 贴进来）
GET {{host}}/api/devices
Authorization: Bearer {{token}}
```

**验收**：编译通过，搜索 `WeatherForecast` 无结果。

---

## 阶段 1：质量基线（约 1 天）

> 这一步产出的东西，是面试官判断"你有没有工程习惯"的直接信号。

### 步骤 1.1 建测试工程（解决"零测试"）

```bash
cd "D:\Project\VS project\Jestnano"
dotnet new xunit -o tests/ObjectDetection.Tests
dotnet add tests/ObjectDetection.Tests reference ObjectDetection/ObjectDetection.csproj
dotnet new sln -n ObjectDetection --force
dotnet sln add ObjectDetection/ObjectDetection.csproj tests/ObjectDetection.Tests/ObjectDetection.Tests.csproj
```

> 建议顺带把 `ObjectDetection/ObjectDetection.slnx` 删掉，解决方案文件放根目录，别塞在项目里。

### 步骤 1.2 把可测逻辑抽出来（为测试而设计）

现在 `EvaluateByRule` 是 `DetectionService` 里的 `private static`，测不到。把它抽成独立的领域服务：

**新建** `ObjectDetection/Domain/ComplianceEvaluator.cs`：

```csharp
using ObjectDetection.Models;

namespace ObjectDetection.Domain;

/// <summary>
/// 纯函数：根据设备合规规则判定一条人体检测是否构成违规。
/// 不依赖 EF、不依赖时间、不依赖 IO —— 所以可以零成本单测。
/// </summary>
public static class ComplianceEvaluator
{
    public static IReadOnlyList<string> Evaluate(
        IReadOnlyDictionary<string, double?> attributes,
        ComplianceRule? rule)
    {
        var types = new List<string>();
        if (rule is null || !rule.Enabled) return types;

        if (rule.RequireMask && !Meets(attributes, "mask", rule.MinConfidence))
            types.Add("no_mask");

        if (rule.RequireHelmet && !Meets(attributes, "helmet", rule.MinConfidence))
            types.Add("no_helmet");

        return types;
    }

    private static bool Meets(IReadOnlyDictionary<string, double?> attrs, string label, decimal min)
        => attrs.GetValueOrDefault(label) is double conf && conf >= (double)min;
}
```

然后把 `DetectionService.cs:63` 改成：

```csharp
var types = ComplianceEvaluator.Evaluate(person.Attributes, rule);
```

并删掉 `DetectionService` 里原来的 `Meets` 和 `EvaluateByRule`。

**新建** `tests/ObjectDetection.Tests/ComplianceEvaluatorTests.cs`：

```csharp
using ObjectDetection.Domain;
using ObjectDetection.Models;
using Xunit;

namespace ObjectDetection.Tests;

public class ComplianceEvaluatorTests
{
    private static ComplianceRule Rule(decimal min = 0.5m) => new()
    {
        Enabled = true,
        RequireMask = true,
        RequireHelmet = false,
        MinConfidence = min
    };

    [Fact]
    public void 规则为空_不违规()
        => Assert.Empty(ComplianceEvaluator.Evaluate(new Dictionary<string, double?>(), null));

    [Fact]
    public void 规则未启用_不违规()
    {
        var rule = Rule();
        rule.Enabled = false;
        var attrs = new Dictionary<string, double?> { ["mask"] = 0.99 };
        Assert.Empty(ComplianceEvaluator.Evaluate(attrs, rule));
    }

    [Fact]
    public void 未检测到口罩_判定未戴口罩()
    {
        var types = ComplianceEvaluator.Evaluate(new Dictionary<string, double?>(), Rule());
        Assert.Single(types);
        Assert.Equal("no_mask", types[0]);
    }

    [Fact]
    public void 口罩置信度低于阈值_判定未戴口罩()
    {
        var attrs = new Dictionary<string, double?> { ["mask"] = 0.3 };
        Assert.Equal(new[] { "no_mask" }, ComplianceEvaluator.Evaluate(attrs, Rule(0.5m)));
    }

    [Fact]
    public void 口罩置信度恰好等于阈值_视为达标()
    {
        // 边界：代码是 conf >= min，等于应通过
        var attrs = new Dictionary<string, double?> { ["mask"] = 0.5 };
        Assert.Empty(ComplianceEvaluator.Evaluate(attrs, Rule(0.5m)));
    }

    [Fact]
    public void 同时要求口罩和安全帽_可返回多条违规()
    {
        var rule = Rule();
        rule.RequireHelmet = true;
        var types = ComplianceEvaluator.Evaluate(new Dictionary<string, double?>(), rule);
        Assert.Equal(2, types.Count);
        Assert.Contains("no_mask", types);
        Assert.Contains("no_helmet", types);
    }
}
```

**验收**：`dotnet test` 通过，6 个测试全绿。
**面试素材**："我把判定逻辑抽成纯函数，因为它是不依赖 IO 的领域规则——这样测试不用起数据库，跑一次 200ms。"

### 步骤 1.3 全局异常处理（D9）

**新建** `ObjectDetection/Infrastructure/GlobalExceptionHandler.cs`：

```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ObjectDetection.Infrastructure;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext http, Exception ex, CancellationToken ct)
    {
        var (status, title) = ex switch
        {
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "设备未授权"),
            KeyNotFoundException        => (StatusCodes.Status404NotFound, "资源不存在"),
            ArgumentException           => (StatusCodes.Status400BadRequest, "请求参数不合法"),
            _                           => (StatusCodes.Status500InternalServerError, "服务器内部错误")
        };

        if (status >= 500)
            logger.LogError(ex, "未处理异常 {Method} {Path}", http.Request.Method, http.Request.Path);
        else
            logger.LogWarning(ex, "业务异常 {Method} {Path}", http.Request.Method, http.Request.Path);

        http.Response.StatusCode = status;
        await http.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            // 5xx 绝不把异常细节返回给客户端
            Detail = status >= 500 ? null : ex.Message,
            Instance = http.Request.Path
        }, ct);

        return true;
    }
}
```

`Program.cs` 注册（放在 `builder.Services.AddControllers()` 附近）：

```csharp
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
```

中间件顺序（**放在管道最前面**，紧跟 `var app = builder.Build();`）——异常处理是"外层捕获"，位置越靠前能捕获的异常越多：

```csharp
var app = builder.Build();

app.UseExceptionHandler();   // ← 第一个中间件，捕获后面所有环节的异常

if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

加了以后，`DetectionsController` 里那个 try/catch 就可以删了：

```csharp
[HttpPost]
public async Task<IActionResult> Report(
    [FromBody] DetectionReportDto dto,
    [FromHeader(Name = "X-Api-Key")] string? apikey)
{
    if (string.IsNullOrWhiteSpace(apikey))
        return Unauthorized(new { message = "缺少ApiKey" });

    var result = await _dete.SaveAsync(dto, apikey);
    return Created("/api/detections", result);
}
```

**验收**：故意让接口抛异常（比如临时在 `DetectionService` 里 `throw new InvalidOperationException("test")`），客户端收到 `500` + 标准 `ProblemDetails` JSON，**不含堆栈**。
**面试素材**："异常统一在管道里处理，控制器只表达成功路径；5xx 不泄露内部细节。"

### 步骤 1.4 结构化日志

```bash
dotnet add ObjectDetection/ObjectDetection.csproj package Serilog.AspNetCore
```

`Program.cs` 顶部：

```csharp
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("app", "object-detection"));
```

`appsettings.json` 加：

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": { "Microsoft.AspNetCore": "Warning", "Microsoft.EntityFrameworkCore": "Warning" }
  },
  "WriteTo": [
    { "Name": "Console" },
    { "Name": "File", "Args": { "path": "logs/log-.txt", "rollingInterval": "Day", "retainedFileCountLimit": 14 } }
  ]
}
```

**验收**：启动后 `logs/` 下有按天滚动的文件；一次 500 请求能在日志里按 `TraceId` 串起来。

---

## 阶段 2：分层演进（约 3 天）

> 现在 6 个 Controller 里，5 个直接注入 `AppDbContext` 写查询，只有 `AuthController` 之外的一个 `DetectionService`。
> 目标不是"为了分层而分层"，而是**让 Controller 只做 HTTP 语义**（状态码、DTO 形状），业务和查询搬到 Service。

### 步骤 2.1 配置集中：消灭双份真相（D6）

**新建** `ObjectDetection/Options/MonitoringOptions.cs`：

```csharp
namespace ObjectDetection.Options;

public sealed class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    /// <summary>超过这个秒数没心跳就算离线</summary>
    public int HeartbeatTimeoutSeconds { get; set; } = 60;

    /// <summary>心跳检测扫描间隔（秒）</summary>
    public int HeartbeatIntervalSeconds { get; set; } = 15;

    /// <summary>扫描记录保留天数</summary>
    public int DetectionRetentionDays { get; set; } = 30;

    /// <summary>违规记录保留天数</summary>
    public int ViolationRetentionDays { get; set; } = 180;

    /// <summary>统计回填天数</summary>
    public int StatsBackfillDays { get; set; } = 7;
}
```

`Program.cs`：

```csharp
builder.Services.Configure<MonitoringOptions>(
    builder.Configuration.GetSection(MonitoringOptions.SectionName));
```

`appsettings.json`：

```json
"Monitoring": {
  "HeartbeatTimeoutSeconds": 60,
  "HeartbeatIntervalSeconds": 15,
  "DetectionRetentionDays": 30,
  "ViolationRetentionDays": 180,
  "StatsBackfillDays": 7
}
```

改 `DevicesController`：删掉 `private const int HeartbeatTimeoutSeconds = 60;`，构造函数注入 `IOptions<MonitoringOptions> options`，用 `options.Value.HeartbeatTimeoutSeconds`。
改 `DeviceHeartbeatMonitor` 和 `DailyStatsWorker`：同样注入 `IOptions<MonitoringOptions>`。

**验收**：全局搜索 `60`、`15`、`30`、`180`，这些魔法数字不再出现在两个地方。
**面试素材**："配置项集中到一个 Options 类，避免控制器和后台任务各维护一份超时时间。"

### 步骤 2.2 状态枚举化（D10）

**新建** `ObjectDetection/Models/Enums.cs`：

```csharp
namespace ObjectDetection.Models;

public enum DeviceStatus { Offline = 0, Online = 1 }

public enum ViolationStatus { Open = 0, Handled = 1 }
```

改实体：

```csharp
public DeviceStatus Status { get; set; } = DeviceStatus.Offline;
public ViolationStatus Status { get; set; } = ViolationStatus.Open;
```

`AppDbContext` 里加字符串转换（**列还是字符串，代码里是枚举**，对既有数据零风险，因为名字和值完全对应）：

```csharp
b.Entity<Device>().Property(d => d.Status).HasMaxLength(20).HasConversion<string>();
b.Entity<Violation>().Property(v => v.Status).HasMaxLength(20).HasConversion<string>();
```

然后：

```bash
dotnet ef migrations add EnumDeviceAndViolationStatus
dotnet ef database update
```

**注意**：这次迁移**不改变表结构**（还是 nvarchar(20)），只是让 C# 侧强类型。如果 `dotnet ef` 生成了一条 `AlterColumn`，说明 `MaxLength` 没对上，检查一下。

改完把这些地方同步掉：`DevicesController`、`ViolationsController`、`DeviceHeartbeatMonitor`、`DetectionService`。

**验收**：搜索 `"Online"`、`"Open"` 在 C# 里不再出现（前端 JSON 序列化会变成 `"Online"` 字面量的前提是配置了枚举转换——确认 `AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))`，否则前端会收到数字 0/1，**这一步要同时改前端类型定义**）。
**面试素材**："用字符串存状态时，拼错一个字母就是线上 bug；枚举 + 字符串转换既保证类型安全，又不用改表结构。"

> ⚠️ 这一步会动到前后端契约，是你第一个"完整改造"——建议单独开分支 `feat/enum-status`，改完前后端一起提交。

### 步骤 2.3 抽 `IDeviceService`

现在 `DevicesController` 129 行里混了：心跳写入、列表查询 + 内存离线计算、规则读取、规则 upsert、实体→DTO 映射。

**新建** `ObjectDetection/Services/IDeviceService.cs`：

```csharp
using ObjectDetection.Dtos;

namespace ObjectDetection.Services;

public interface IDeviceService
{
    Task<HeartbeatResponseDto> HeartbeatAsync(string deviceCode, string apiKey, CancellationToken ct);
    Task<List<DeviceDto>> ListAsync(CancellationToken ct);
    Task<ComplianceRuleDto?> GetRuleAsync(int deviceId, CancellationToken ct);
    Task<ComplianceRuleDto?> UpsertRuleAsync(int deviceId, UpdateRuleRequestDto dto, CancellationToken ct);
}
```

**新建** `ObjectDetection/Services/DeviceService.cs`，把控制器里的逻辑**原样搬过来**（先搬运，不优化），加上 `AsNoTracking`、`ConfigureAwait` 不用加（ASP.NET Core 没有同步上下文，加了是噪音）。

控制器瘦成：

```csharp
[ApiController]
[Route("api/devices")]
[Authorize]
public class DevicesController(IDeviceService devices) : ControllerBase
{
    [HttpPost("heartbeat")]
    [AllowAnonymous]
    public async Task<ActionResult<HeartbeatResponseDto>> Heartbeat(
        [FromBody] HeartbeatRequestDto dto,
        [FromHeader(Name = "X-Api-Key")] string? apikey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(apikey))
            return Unauthorized(new { message = "缺少ApiKey" });
        return Ok(await devices.HeartbeatAsync(dto.DeviceCode, apikey, ct));
    }

    [HttpGet]
    public async Task<ActionResult<List<DeviceDto>>> Get(CancellationToken ct)
        => Ok(await devices.ListAsync(ct));

    [HttpGet("{id:int}/rule")]
    public async Task<ActionResult<ComplianceRuleDto>> GetRule(int id, CancellationToken ct)
        => await devices.GetRuleAsync(id, ct) is { } rule ? Ok(rule) : NotFound(new { message = "设备不存在或未配置规则" });

    [HttpPut("{id:int}/rule")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ComplianceRuleDto>> PutRule(
        int id, [FromBody] UpdateRuleRequestDto dto, CancellationToken ct)
        => await devices.UpsertRuleAsync(id, dto, ct) is { } rule ? Ok(rule) : NotFound(new { message = "设备不存在" });
}
```

注册：`builder.Services.AddScoped<IDeviceService, DeviceService>();`

**验收**：`DevicesController` 降到 50 行以内；用 `ObjectDetection.http` 把 4 个接口全跑一遍，行为不变。

### 步骤 2.4 抽 `IViolationService`

同理处理 `ViolationsController`（109 行）：分页查询、详情、处理（`Patch`）。

注意保留你现在写对的这两点，别在搬运时丢掉：

- `page = Math.Max(page, 1)` / `pageSize = Math.Clamp(pageSize, 1, 100)`
- `if (v.Status != ViolationStatus.Open) return Conflict(...)` 的**乐观并发保护**

**验收**：`/api/violations?status=Open&pageSize=999` 返回 `pageSize=100`；重复处理同一条返回 `409`。

### 步骤 2.5 抽 `IStatsService`（顺便为阶段 3 铺路）

`StatsController` 有 `DayRange`、`Rate` 两个私有静态方法 + 时区常量。把整个统计逻辑搬到 `StatsService`，`StatsController` 只留 2 个 action。

**验收**：`StatsController` 降到 30 行以内。

### 步骤 2.6 引 `TimeProvider`（为时区 bug 做铺垫）

```csharp
builder.Services.AddSingleton(TimeProvider.System);
```

Service 里注入 `TimeProvider`，用 `timeProvider.GetUtcNow()` 替代 `DateTime.UtcNow`。

**验收**：全局搜索 `DateTime.UtcNow`，只剩 Program.cs 的种子数据（或也没有）。

> 这是 .NET 8+ 的现代写法，也是"可测试性"的标准答案：测试时注入 `FakeTimeProvider`（`Microsoft.Extensions.TimeProvider.Testing` 包）就能测"跨天"逻辑。

---

## 阶段 3：修真实缺陷（约 3 天）

> 这一阶段的每一步都是一个**面试故事**——因为它们都是"上线才会暴露"的问题。

### 步骤 3.1 修时区错位（D1）🔥

**问题**：`StatsController.Daily` 第 59 行 `var today = DateTime.Today;` 取的是**服务器本地日期**，但 `DayRange` 把它当作上海本地日期转换成 UTC。服务器在 UTC 时区（容器！）时，`/api/stats/daily` 的日期整体错一天，而 `/api/stats/compliance` 是对的（它用 `TimeZoneInfo.ConvertTime(DateTime.UtcNow, Tz).Date`）。

**修法**：统一走 `TimeProvider` + 上海时区，抽一个 `AppClock`：

```csharp
public sealed class AppClock(TimeProvider timeProvider)
{
    private static readonly TimeZoneInfo Tz =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");

    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public DateOnly TodayLocal =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Tz));
}
```

`StatsService` 里所有"今天"都从 `clock.TodayLocal` 取，删掉自己那份 `TimeZoneInfo`。

**验收**（这才是关键——要能**证明**修好了）：

临时把 `TZ` 环境变量改成 UTC 启动，对比两个接口：

```bash
# Windows PowerShell
$env:TZ="UTC"; dotnet run --project ObjectDetection
# Linux / 容器
docker run -e TZ=UTC ...
```

- 修之前：`/api/stats/daily` 与 `/api/stats/compliance` 的日期不一致
- 修之后：两个接口日期一致，且都等于当前的上海日期

**面试素材**（这是最强的一个）："容器化之后接口日期错了一天。原因是 `DateTime.Today` 取服务器本地时间，而我按上海时区做了转换。我把时钟抽象成 `TimeProvider` 注入，并写了一个跨时区的集成测试锁住行为。"

### 步骤 3.2 让 `DailyStats` 真正被使用（D2）🔥

**问题**：`DailyStatsWorker` 每小时花力气回填 `DailyStats`，但 `/api/stats/daily` 根本不读它，而是实时循环 `CountAsync`（`days` 最大 31，一次请求最多 62 次数据库往返）。

**修法**：

```csharp
public async Task<List<DailyStatDto>> DailyAsync(int days, CancellationToken ct)
{
    days = Math.Clamp(days, 1, 31);
    var today = clock.TodayLocal;
    var from = today.AddDays(-(days - 1));

    var rows = await db.DailyStats.AsNoTracking()
        .Where(s => s.StatDate >= from && s.StatDate <= today)
        .GroupBy(s => s.StatDate)
        .Select(g => new
        {
            Date = g.Key,
            Persons = g.Sum(x => x.Persons),
            Violations = g.Sum(x => x.Violations)
        })
        .ToListAsync(ct);

    return rows
        .OrderBy(r => r.Date)
        .Select(r => new DailyStatDto
        {
            Date = r.Date.ToString("yyyy-MM-dd"),
            PersonDetections = r.Persons,
            Violations = r.Violations,
            ComplianceRate = Rate(r.Persons, r.Violations)
        })
        .ToList();
}
```

**验收**：`days=30` 时 SQL 只有 1 条（用 Serilog 的 EF Core 日志确认），响应时间和 `days=7` 基本一样。

**同时解决 D5**：`DailyStatsWorker` 现在不统计今天的数据，所以 `DailyAsync` 里 `today` 这天必然是空的。两个选择，**选一个并在代码注释里写清楚理由**：

- A. 接受"只有完整天才统计"——`DailyAsync` 只返回到 `today.AddDays(-1)`，并在 DTO 里加 `IsPartial` 标记
- B. worker 增加一次增量统计今天的逻辑（注意：今天的数据会随天数增长，需要 upsert）

**推荐 A**：语义清晰、无重复计算。做法是把查询上界从 `today` 改成 `today.AddDays(-1)`：

```csharp
var lastCompleteDay = today.AddDays(-1);
var from = lastCompleteDay.AddDays(-(days - 1));
// .Where(s => s.StatDate >= from && s.StatDate <= lastCompleteDay)
```

并在方法上注释："今天数据不完整，由 worker 次日回填"。这样接口永远不会返回一堆"半天数据"，前端也不用处理空值。

### 步骤 3.3 干掉 N+1（D4）

`DailyStatsWorker.cs:57-72`：

```csharp
// 现在：每个设备一次查询 → 7 天 × N 设备
foreach (var deviceId in deviceIds)
{
    var row = await db.DailyStats.FirstOrDefaultAsync(s => s.StatDate == day && s.DeviceId == deviceId, ct);
    ...
}
```

改成一次查全 + 内存字典：

```csharp
var dayStats = await db.DailyStats
    .Where(s => s.StatDate == day && deviceIds.Contains(s.DeviceId))
    .ToDictionaryAsync(s => s.DeviceId, ct);

foreach (var deviceId in deviceIds)
{
    if (!dayStats.TryGetValue(deviceId, out var row))
    {
        row = new DailyStat { StatDate = day, DeviceId = deviceId };
        db.DailyStats.Add(row);
    }
    row.Persons = persons.GetValueOrDefault(deviceId);
    row.Violations = vios.GetValueOrDefault(deviceId);
    row.Compliant = Math.Max(0, row.Persons - row.Violations);
    row.UpdatedAt = clock.UtcNow;
}
```

顺带把 `persons.FirstOrDefault(...)?.Count` 也换成 `ToDictionary`，消掉 O(n²)。

**验收**：用 SQL Server Profiler 或 EF 日志数一下：`days=7`、10 个设备时，修改前约 70 次查询，修改后 8 次（7 天 × 1 次 + 1 次保存）。
**面试素材**："N+1 是后台任务里最容易藏的性能问题——单次看不慢，跑 7 天 × N 设备就放大成几百次往返。"

### 步骤 3.4 让 `ConfirmFrames` 不再是死字段（D3）⭐

**这是本计划里最有"设计味"的一步，也是最好的面试题。**

现状：规则里能配"连续 3 帧确认"，但服务端从不看它——因为它需要**跨报文的时序状态**，而 `DetectionService` 现在是完全无状态的。

三个方案，**选哪个都要在 ADR 里写清取舍**：

| 方案 | 做法 | 优点 | 缺点 |
|---|---|---|---|
| A. 移到设备端 | 删掉服务端字段，Jetson 端做帧确认 | 语义正确（服务端根本没有"帧"概念）、零状态 | 需要改 Python 端；规则下发仍是服务端 |
| B. 服务端滑窗 | 内存 `ConcurrentDictionary<(deviceId,trackId), count>` | 实现简单 | 多实例部署失效；重启丢状态 |
| C. Redis 计数 | 用 Redis 做滑窗计数 | 多实例正确 | 引入新依赖 |

**推荐 A**，理由：`DetectionReportDto` 是**一批检测结果**，服务端拿到的粒度是"一次上报"，不是"一帧"。`ConfirmFrames` 属于视频流时序逻辑，天然属于边缘端。把它从 `ComplianceRule` 移除，改由设备端配置。

如果选 A，就删字段 + 一次迁移，并在 `docs/decisions/ADR-001-confirm-frames.md` 里写清楚。**删代码也是重构**，而且是最能体现判断力的那种。

> 如果你想让项目"更像企业级"，选 B 也可以，但要接受两个限制并写进文档。

**验收**：字段要么被真正使用（有测试覆盖），要么被移除（迁移 + ADR）。不允许"存着但没用"。
**面试素材**："实现前我先问：服务端到底有没有'帧'这个概念？没有。所以我把这个配置移回设备端——删掉一个字段比加一个分布式状态更正确。"

### 步骤 3.5 ApiKey 哈希 + 恒定时间比较（D7）

**问题**：明文存储 + `!=` 比较（可被时序攻击探测）+ 一旦库泄露所有设备失守。

**修法**：

```csharp
using System.Security.Cryptography;
using System.Text;

public static class ApiKeyHasher
{
    public static string Hash(string apiKey)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));

    public static bool Verify(string apiKey, string storedHash)
    {
        var computed = Encoding.UTF8.GetBytes(Hash(apiKey));
        var stored = Encoding.UTF8.GetBytes(storedHash);
        // 长度不同时 FixedTimeEquals 直接返回 false，不会提前退出
        return CryptographicOperations.FixedTimeEquals(computed, stored);
    }
}
```

实体改成 `ApiKeyHash`，迁移时把现有明文 key 哈希进去：

```csharp
// 迁移里的一次性数据转换
migrationBuilder.Sql("UPDATE Devices SET ApiKeyHash = UPPER(CONVERT(varchar(64), HASHBYTES('SHA2_256', ApiKey), 2))");
```

> ⚠️ `HASHBYTES` 返回的大小写和 `Convert.ToHexStringLower` 要一致——建议统一成小写：`LOWER(CONVERT(...))`。

**验收**：数据库里再也看不到明文 key；用一个错误 key 请求返回 401；正确 key 正常。
**面试素材**："设备密钥和用户密码一样不能明文存；比较要用 `FixedTimeEquals`，否则逐字节比较会泄露前缀匹配长度。"

---

## 阶段 4：交付工程化（约 3 天）

### 步骤 4.1 健康检查（D12）

```bash
dotnet add ObjectDetection/ObjectDetection.csproj package Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore
```

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("db");
```

```csharp
app.MapHealthChecks("/health");
```

**验收**：`GET /health` 返回 `Healthy`；把 SQL Server 停掉，返回 `Unhealthy`。

### 步骤 4.2 设备上报限流（D12）

```csharp
builder.Services.AddRateLimiter(o =>
{
    o.AddFixedWindowLimiter("device", opt =>
    {
        opt.PermitLimit = 60;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
...
app.UseRateLimiter();
```

控制器上：`[EnableRateLimiting("device")]`（加在 `Heartbeat` 和 `Report` 上）。

**验收**：`for` 循环打 100 次，第 61 次开始返回 429。
**面试素材**："边缘设备可能因为网络重试风暴打爆服务端，所以我给上报接口单独限流，和管理端接口隔离开。"

### 步骤 4.3 Dockerfile（你 Ubuntu VM 里正好能跑）

在仓库根目录建 `Dockerfile`：

```dockerfile
# ---- 1. 前端构建 ----
FROM node:24-alpine AS web
WORKDIR /src/web
COPY web/package*.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

# ---- 2. 后端构建 ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ObjectDetection/ObjectDetection.csproj ObjectDetection/
RUN dotnet restore ObjectDetection/ObjectDetection.csproj
COPY ObjectDetection/ ObjectDetection/
# 用构建好的前端覆盖 wwwroot
COPY --from=web /src/web/dist/ ObjectDetection/wwwroot/
RUN dotnet publish ObjectDetection/ObjectDetection.csproj -c Release -o /app/publish --no-restore

# ---- 3. 运行 ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ObjectDetection.dll"]
```

`.dockerignore`：

```
**/bin/
**/obj/
web/node_modules/
web/dist/
deploy/
.git/
*.log
```

**验收**：`docker build -t object-detection .` 成功，镜像能起（连不上库也要能启动并报健康检查失败，而不是崩溃）。

> 📌 注意：`mcr.microsoft.com/mssql/server` **只有 amd64**。你的后端如果跑在 Jetson（arm64）上，SQL Server 容器起不来——这也是为什么架构上"后端跑在服务器、Jetson 只跑推理"是对的。

### 步骤 4.4 docker-compose

```yaml
services:
  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "${DB_PASSWORD:?set DB_PASSWORD}"
    ports: ["1433:1433"]
    volumes: ["mssql-data:/var/opt/mssql"]

  app:
    build: .
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__Default: "Server=db;Database=ObjectDetectionDb;User Id=sa;Password=${DB_PASSWORD};TrustServerCertificate=True"
      Jwt__Key: "${JWT_KEY:?set JWT_KEY}"
    ports: ["5027:8080"]
    depends_on: [db]

volumes:
  mssql-data:
```

配 `.env.example`（**不要提交真实 `.env`**）：

```
DB_PASSWORD=ChangeMe_Strong123
JWT_KEY=ChangeMe_Use_At_Least_32_Chars_Secret
```

**验收**：`docker compose up` 后 `http://<vm-ip>:5027/health` 返回 Healthy，前端页面能打开。
**面试素材**："用环境变量覆盖连接串和密钥，`.env` 不进版本库——配置和密钥分离。"

### 步骤 4.5 GitHub Actions

在根目录建 `.github/workflows/ci.yml`：

```yaml
name: ci

on:
  push:
    branches: [main]
  pull_request:

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release
      - run: dotnet test --no-build -c Release --logger "trx;LogFileName=results.trx"
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results
          path: '**/*.trx'

  web:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '24' }
      - run: npm ci
        working-directory: web
      - run: npm run build
        working-directory: web
```

**验收**：推送后 Actions 绿标；README 里贴 badge。

### 步骤 4.6 写 README

这是**简历落地页**，比代码还重要。模板：

```markdown
# ObjectDetection — 边缘 AI 安全合规检测系统

基于 Jetson Nano 的实时检测 + .NET 10 后端。
（一句话说清：边缘端跑 YOLO 推理，后端做设备管理、违规闭环、合规统计。）

## 架构
（贴一张图：Jetson 设备 → HTTP 上报 → ASP.NET Core → SQL Server → Vue3 前端）

## 技术栈
.NET 10 / EF Core 10 / JWT / Vue 3 + Vite + TS / Docker / xUnit

## 快速开始
docker compose up -d
# 或本地
dotnet run --project ObjectDetection

## 关键设计
- **设备鉴权**：边缘设备用 X-Api-Key（无交互登录），管理端用 JWT
- **时区**：全库存 UTC，展示层按 Asia/Shanghai 聚合；`TimeProvider` 注入便于测试
- **统计**：`DailyStatsWorker` 每小时回填，`/api/stats/daily` 只读聚合表（1 次查询）
- **异常**：`IExceptionHandler` 统一输出 `ProblemDetails`，5xx 不泄露细节
- **限流**：上报接口独立固定窗口，防止重试风暴

## 设计决策
见 `docs/decisions/`

## API
（Swagger 地址 / 或贴关键接口表）
```

---

## 2. 日程表

### 完整版（14 天，每天 2 小时）

| 天 | 阶段 | 步骤 |
|---|---|---|
| 1 | 0 | 0.1 git 基线 → 0.2 依赖漏洞 → 0.3 删模板 |
| 2 | 1 | 1.1 测试工程 → 1.2 抽 `ComplianceEvaluator` + 6 个单测 |
| 3 | 1 | 1.3 全局异常 → 1.4 Serilog |
| 4-5 | 2 | 2.1 Options → 2.2 枚举化 |
| 6 | 2 | 2.3 `IDeviceService` |
| 7 | 2 | 2.4 `IViolationService` |
| 8 | 2 | 2.5 `IStatsService` → 2.6 `TimeProvider` |
| 9 | 3 | **3.1 时区 bug**（写测试锁住） |
| 10 | 3 | **3.2 `DailyStats` 复用** → 3.3 修 N+1 |
| 11 | 3 | **3.4 `ConfirmFrames` 取舍** → 3.5 ApiKey 哈希 |
| 12 | 4 | 4.1 健康检查 → 4.2 限流 |
| 13 | 4 | 4.3 Dockerfile → 4.4 compose |
| 14 | 4 | 4.5 CI → 4.6 README |

### 精简版（4 天，时间紧就只做这些）

如果只有 4 天，**只做这 6 步**，它们的性价比最高：

| 优先级 | 步骤 | 为什么 |
|---|---|---|
| 1 | 0.1 git + 0.2 依赖漏洞 | 没有仓库，其他都白做 |
| 2 | 1.1+1.2 测试 | 从 0 到 1 的差别，面试必问 |
| 3 | 1.3 全局异常 | 5 分钟工作量，工程感提升明显 |
| 4 | **3.1 时区 bug** | 唯一的"真 bug 故事"，含金量最高 |
| 5 | **3.2 复用 DailyStats** | 性能 + 数据一致性双料故事 |
| 6 | 4.6 README | 简历落地页 |

> 阶段 2（分层）虽然重要，但**它不产生故事**——面试官看不出你"抽了 Service"。所以时间紧时它是第一个被砍的。

---

## 3. 面试故事卡

改完后你应该能不看稿讲出这 6 个：

| # | 故事 | 一句话钩子 | 追问预备 |
|---|---|---|---|
| 1 | **时区错位** | "容器化后统计接口日期错了一天" | `DateTime.Today` vs `DateTime.UtcNow` 的区别；`DateTimeKind`；为什么数据库全存 UTC |
| 2 | **统计表只写不读** | "worker 白跑了三天，接口还在实时算" | 预聚合 vs 实时聚合的取舍；数据一致性怎么保证 |
| 3 | **N+1** | "10 台设备 7 天，70 次查询压成 8 次" | 怎么发现的（EF 日志/Profiler）；`ToDictionary` 为什么比 `FirstOrDefault` 在循环里好 |
| 4 | **删掉一个字段** | "`ConfirmFrames` 实现之前我先问：服务端有'帧'吗？" | 有状态 vs 无状态；多实例部署下内存状态的问题 |
| 5 | **设备鉴权** | "为什么设备不用 JWT 用 ApiKey" | 无交互登录；密钥轮换；`FixedTimeEquals` 防时序攻击 |
| 6 | **工程流水线** | "一个 PR 从 build 到 test 到镜像全自动" | 为什么 `.env` 不进库；docker 多阶段构建减小镜像 |

---

## 4. 提交与分支规范

```bash
# 一个阶段一个分支，改完自测，再合并
git checkout -b chore/baseline-safety-net      # 阶段 0
git checkout -b feat/global-exception-handler  # 1.3
git checkout -b fix/stats-timezone             # 3.1
git checkout -b perf/daily-stats-n-plus-1      # 3.3
git checkout -b refactor/extract-device-service # 2.3
```

commit 前缀用 `feat/ fix/ perf/ refactor/ test/ chore/ docs/`。

**这个习惯本身就是面试素材**：面试官看到 30 个语义化提交 + 6 个已合并 PR，比看到 1 个 "init" 提交强十倍。

---

## 5. 完成度自检

全部打勾，这个项目就能直接放简历：

- [ ] 有 GitHub 仓库，README 有架构图和快速开始
- [ ] `.gitignore` 干净（`bin/ obj/ node_modules/ deploy/` 都没进库）
- [ ] `dotnet build` 0 错误 0 警告（含依赖漏洞清零）
- [ ] `dotnet test` 通过，且**至少覆盖** `ComplianceEvaluator` 全部分支
- [ ] 全局异常处理生效，5xx 不泄露堆栈
- [ ] `StatsController` / `DevicesController` / `ViolationsController` 都 < 50 行
- [ ] `DateTime.UtcNow` 只在 `AppClock` 里出现
- [ ] `DateTime.Today` **一次都不出现**
- [ ] `/api/stats/daily?days=30` 只发 1 条 SQL
- [ ] `ConfirmFrames` 要么被使用、要么已删除（无第三种状态）
- [ ] 数据库里没有明文 ApiKey
- [ ] `/health` 可用
- [ ] 上报接口限流生效
- [ ] `docker compose up` 一键起（在 Ubuntu VM 里验证过）
- [ ] GitHub Actions 绿标
- [ ] 能不看稿讲完第 3 节的 6 个故事

---

## 附：常见卡点

| 现象 | 原因 / 解法 |
|---|---|
| `dotnet ef` 命令不存在 | `dotnet tool install --global dotnet-ef` |
| 迁移后前端收到 `status: 0/1` | `JsonStringEnumConverter` 没注册，或前端 TS 类型没改 |
| `UseExceptionHandler()` 不生效 | 顺序错：它必须是**第一个**中间件，放到 `UseAuthorization` 后面就捕获不到了 |
| 容器里日期还是错 | 镜像默认 `TZ=UTC` 是好事，别改；靠 `AppClock` 在代码里转换 |
| `docker build` 卡在 `npm ci` | `package-lock.json` 没提交，或 `.dockerignore` 把 `web/` 排除了 |
| SQL Server 容器起不来 | 密码不满足复杂度，或宿主机是 arm64（镜像只有 amd64） |

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ObjectDetection.BackgroundServices;
using ObjectDetection.Data;
using ObjectDetection.Domain;
using ObjectDetection.Services;
using Swashbuckle.AspNetCore;
using ObjectDetection.Models;
using Microsoft.AspNetCore.Identity;
using ObjectDetection.Infrastructure;
using System.Text;
using System.Text.Json.Serialization;
using ObjectDetection.Option;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<MonitoringOptions>(builder.Configuration.GetSection(MonitoringOptions.SectionName));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<IDetectionService,DetectionService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddRateLimiter(options =>
{
    // 设备上报：网络抖动时容易重试风暴，按分钟固定窗口限流
    options.AddFixedWindowLimiter("device", opt =>
    {
        opt.PermitLimit = 60;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    // 管理端登录：限定每分钟尝试次数，降低口令爆破的成功率
    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "请求过于频繁，请稍后再试" }, token);
    };
});
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<DeviceHeartbeatMonitor>();
builder.Services.AddScoped<IViolationService, ViolationService>();
builder.Services.AddScoped<IStatsService, StatsService>();

// 时间抽象：测试可注入假时钟，避免依赖系统时间
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<StatsClock>();
var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwt["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwt["Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["key"]!)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = "name",
        RoleClaimType = "role"
    };
});
builder.Services.AddHostedService<DailyStatsWorker>();
builder.Services.AddAuthorization();
var app = builder.Build();

// 容器部署：启动时自动建表/升级（由配置开关控制，默认关闭，避免误伤本地开发库）
// SQL Server 容器启动比应用慢：首次连不上时重试 10 次，避免容器直接退出进重启循环
if (builder.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    using var migrateScope = app.Services.CreateScope();
    var migrateDb = migrateScope.ServiceProvider.GetRequiredService<AppDbContext>();
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            migrateDb.Database.Migrate();
            break;
        }
        catch (Exception ex) when (attempt < 10)
        {
            app.Logger.LogWarning(ex, "数据库迁移失败（第 {Attempt} 次），3 秒后重试", attempt);
            Thread.Sleep(TimeSpan.FromSeconds(3));
        }
    }
}

app.UseExceptionHandler();
// 仅开发环境种账号：Users 为空时建 admin(Admin) / operator(User)。
// 生产千万别这么干——会自动造出弱口令管理员，生产用运维脚本或一次性接口建号
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!db.Users.Any())
    {
        var hasher = new PasswordHasher<User>();

        var admin = new User { Username = "admin", Role = "Admin" };
        admin.PasswordHash = hasher.HashPassword(admin, "Admin@123");

        var op = new User { Username = "operator", Role = "User" };
        op.PasswordHash = hasher.HashPassword(op, "User@123");

        db.Users.AddRange(admin, op);
        db.SaveChanges();
    }
}
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 容器里只监听 http:8080，开发环境也走 http，跳过重定向避免每次启动刷「无法确定 https 端口」的警告
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// 先找默认文件（index.html），再提供静态资源（JS/CSS/图片）
// 顺序不能反：UseDefaultFiles 只改写请求路径，真正返回文件的是 UseStaticFiles
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();

app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

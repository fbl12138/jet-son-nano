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

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<MonitoringOptions>(builder.Configuration.GetSection(MonitoringOptions.SectionName));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<IDetectionService,DetectionService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
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

app.UseHttpsRedirection();

// 先找默认文件（index.html），再提供静态资源（JS/CSS/图片）
// 顺序不能反：UseDefaultFiles 只改写请求路径，真正返回文件的是 UseStaticFiles
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ObjectDetection.Data;
using ObjectDetection.Dtos;
using ObjectDetection.Models;
using ObjectDetection.Services;

namespace ObjectDetection.Tests;

/// <summary>
/// 违规处置的原子性：处置靠一条「WHERE Status = Open」的条件更新完成，
/// 并发（或拿着过期状态）的第二次处置不会覆盖第一次的结果。
/// 这里用 SQLite 而不是 InMemory，因为 InMemory 不执行 ExecuteUpdate 生成的 SQL。
/// </summary>
public sealed class ViolationServiceTests : IDisposable
{
    private const long ViolationId = 1;
    private readonly SqliteConnection _connection;

    public ViolationServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var db = NewContext();
        db.Database.EnsureCreated();
        db.Devices.Add(new Device
        {
            DeviceCode = "JETSON-01",
            Name = "A栋1楼东门",
            ApiKeyHash = new string('a', 64)
        });
        db.Violations.Add(new Violation
        {
            Id = ViolationId,
            DeviceId = 1,
            ViolationTypes = "no_mask",
            PersonCount = 1,
            DetectedAt = new DateTime(2026, 11, 17, 1, 0, 0, DateTimeKind.Utc)
        });
        db.SaveChanges();
    }

    private AppDbContext NewContext()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task 处置待处理违规_返回成功并落库()
    {
        await using var db = NewContext();
        var svc = new ViolationService(db);

        var (outcome, violation) = await svc.HandleAsync(
            ViolationId, userId: 7, new HandleViolationRequestDto { Remark = "已提醒并补发口罩" });

        Assert.Equal(HandleOutcome.Success, outcome);
        Assert.NotNull(violation);
        Assert.Equal(ViolationStatus.Handled, violation!.Status);
        Assert.Equal(7, violation.HandledBy);
        Assert.Equal("已提醒并补发口罩", violation.Remark);
        Assert.NotNull(violation.HandledAt);
    }

    [Fact]
    public async Task 重复处置_返回已处理且不覆盖首次结果()
    {
        await using var db = NewContext();
        var svc = new ViolationService(db);

        await svc.HandleAsync(ViolationId, 1, new HandleViolationRequestDto { Remark = "第一次" });
        var (outcome, violation) = await svc.HandleAsync(
            ViolationId, 2, new HandleViolationRequestDto { Remark = "第二次" });

        Assert.Equal(HandleOutcome.AlreadyHandled, outcome);
        Assert.Equal(1, violation!.HandledBy);
        Assert.Equal("第一次", violation.Remark);
    }

    [Fact]
    public async Task 处置不存在的记录_返回未找到()
    {
        await using var db = NewContext();
        var svc = new ViolationService(db);

        var (outcome, violation) = await svc.HandleAsync(9999, 1, new HandleViolationRequestDto());

        Assert.Equal(HandleOutcome.NotFound, outcome);
        Assert.Null(violation);
    }

    [Fact]
    public async Task 两个管理员各自缓存了待处理状态_只有一个能成功()
    {
        await using var dbA = NewContext();
        await using var dbB = NewContext();

        // B 先把这条记录读进自己的跟踪器：模拟「页面打开时看到的是待处理」
        _ = await dbB.Violations.FirstAsync(v => v.Id == ViolationId);

        var fromA = await new ViolationService(dbA)
            .HandleAsync(ViolationId, 1, new HandleViolationRequestDto { Remark = "A 处理" });
        var fromB = await new ViolationService(dbB)
            .HandleAsync(ViolationId, 2, new HandleViolationRequestDto { Remark = "B 处理" });

        Assert.Equal(HandleOutcome.Success, fromA.outcome);
        Assert.Equal(HandleOutcome.AlreadyHandled, fromB.outcome);

        // 库里保留的是第一次处置的人与备注
        await using var db = NewContext();
        var stored = await db.Violations.AsNoTracking().FirstAsync(v => v.Id == ViolationId);
        Assert.Equal(1, stored.HandledBy);
        Assert.Equal("A 处理", stored.Remark);
    }
}

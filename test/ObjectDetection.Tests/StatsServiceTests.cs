using Microsoft.EntityFrameworkCore;
using ObjectDetection.Data;
using ObjectDetection.Domain;
using ObjectDetection.Models;
using ObjectDetection.Services;

namespace ObjectDetection.Tests;

public class StatsServiceTests
{
    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static StatsService NewService(AppDbContext db, DateTimeOffset nowUtc)
        => new(db, new StatsClock(new FixedTimeProvider(nowUtc)));

    [Fact]
    public async Task 日统计的最后一天_是上海时区的今天()
    {
        await using var db = NewDb();
        // UTC 2026-11-12 16:30 == 上海 2026-11-13 00:30
        // 旧实现 DateTime.Today 在 UTC 服务器上会得到 11-12，整体错一天
        var svc = NewService(db, new DateTimeOffset(2026, 11, 12, 16, 30, 0, TimeSpan.Zero));

        var result = await svc.GetDailyAsync(3);

        Assert.Equal(3, result.Count);
        Assert.Equal("2026-11-11", result[0].Date);
        Assert.Equal("2026-11-13", result[^1].Date);
    }

    [Fact]
    public async Task 合规统计_按上海日期切分窗口()
    {
        await using var db = NewDb();

        // 这条检测发生在 UTC 11-12 16:30，属于上海 11-13，不属于 11-12
        db.Detections.Add(new Detection
        {
            DeviceId = 1,
            LabelCode = "person",
            Confidence = 0.9m,
            DetectedAt = new DateTime(2026, 11, 12, 16, 30, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync();

        var svc = NewService(db, new DateTimeOffset(2026, 11, 13, 1, 0, 0, TimeSpan.Zero));

        var onNov13 = await svc.GetComplianceAsync(new DateTime(2026, 11, 13));
        var onNov12 = await svc.GetComplianceAsync(new DateTime(2026, 11, 12));

        Assert.Equal(1, onNov13.PersonDetections);
        Assert.Equal(0, onNov12.PersonDetections);
        Assert.Equal("2026-11-13", onNov13.Date);
    }

    [Fact]
    public async Task 不传日期时_取上海时区的今天()
    {
        await using var db = NewDb();
        var svc = NewService(db, new DateTimeOffset(2026, 11, 12, 16, 30, 0, TimeSpan.Zero));

        var result = await svc.GetComplianceAsync(null);

        Assert.Equal("2026-11-13", result.Date);
    }
}

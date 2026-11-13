using ObjectDetection.Domain;

namespace ObjectDetection.Tests;

public class StatsClockTests
{
    [Fact]
    public void 服务器在UTC时_今天仍按上海时区计算()
    {
        // UTC 2026-11-12 16:30 == 上海 2026-11-13 00:30
        // 旧的 DateTime.Today 在 UTC 服务器上会得到 11-12，少算一天
        var clock = new StatsClock(
            new FixedTimeProvider(new DateTimeOffset(2026, 11, 12, 16, 30, 0, TimeSpan.Zero)));

        Assert.Equal(new DateOnly(2026, 11, 13), clock.TodayLocal);
    }

    [Fact]
    public void 上海当天上午_仍是当天()
    {
        // UTC 2026-11-13 02:00 == 上海 2026-11-13 10:00
        var clock = new StatsClock(
            new FixedTimeProvider(new DateTimeOffset(2026, 11, 13, 2, 0, 0, TimeSpan.Zero)));

        Assert.Equal(new DateOnly(2026, 11, 13), clock.TodayLocal);
    }

    [Fact]
    public void 业务时区是上海()
    {
        var clock = new StatsClock(
            new FixedTimeProvider(new DateTimeOffset(2026, 11, 13, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal(TimeSpan.FromHours(8), clock.Zone.BaseUtcOffset);
    }
}

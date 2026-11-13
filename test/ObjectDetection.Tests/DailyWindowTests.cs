using ObjectDetection.Domain;

namespace ObjectDetection.Tests;

public class DailyWindowTests
{
    private static readonly TimeZoneInfo Shanghai =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");

    [Fact]
    public void UTC晚间_应换算成上海次日()
    {
        // UTC 2026-11-12 16:30 == 上海 2026-11-13 00:30
        var now = new DateTimeOffset(2026, 11, 12, 16, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 11, 13), DailyWindow.LocalDate(now, Shanghai));
    }

    [Fact]
    public void UTC下午_上海仍是当天()
    {
        // UTC 2026-11-12 15:59 == 上海 2026-11-12 23:59
        var now = new DateTimeOffset(2026, 11, 12, 15, 59, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 11, 12), DailyWindow.LocalDate(now, Shanghai));
    }

    [Fact]
    public void 本地日期的UTC区间_为左闭右开()
    {
        var (start, end) = DailyWindow.ForLocalDate(new DateOnly(2026, 11, 13), Shanghai);

        Assert.Equal(new DateTime(2026, 11, 12, 16, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2026, 11, 13, 16, 0, 0, DateTimeKind.Utc), end);
        Assert.Equal(TimeSpan.FromHours(24), end - start);
    }

    [Fact]
    public void 区间起点落在本地日期的零点()
    {
        var date = new DateOnly(2026, 11, 13);
        var startUtc = DailyWindow.StartUtc(date, Shanghai);

        // 把 UTC 起点换算回上海，应当正好是当天 00:00
        var backToLocal = TimeZoneInfo.ConvertTime(startUtc, Shanghai);
        Assert.Equal(date, DateOnly.FromDateTime(backToLocal));
        Assert.Equal(TimeOnly.MinValue, TimeOnly.FromDateTime(backToLocal));
    }
}

namespace ObjectDetection.Domain;

/// <summary>
/// 把「某个时区的某一天」换算成 UTC 时间区间。
/// 全是纯函数：不读系统时区、不读当前时间，所以可以直接单测，
/// 也可以在任何时区的服务器上得到一致结果。
/// </summary>
public static class DailyWindow
{
    /// <summary>把 UTC 时刻换算成指定时区的「今天」</summary>
    public static DateOnly LocalDate(DateTimeOffset nowUtc, TimeZoneInfo tz)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, tz).DateTime);

    /// <summary>某个本地日期对应的 UTC 起点</summary>
    public static DateTime StartUtc(DateOnly localDate, TimeZoneInfo tz)
    {
        var startLocal = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
    }

    /// <summary>
    /// 某个本地日期对应的 UTC 半开区间 [StartUtc, EndUtc)。
    /// 终点由「次日 00:00」换算而来，因此即使将来引入夏令时也不会算错。
    /// </summary>
    public static (DateTime StartUtc, DateTime EndUtc) ForLocalDate(DateOnly localDate, TimeZoneInfo tz)
        => (StartUtc(localDate, tz), StartUtc(localDate.AddDays(1), tz));
}

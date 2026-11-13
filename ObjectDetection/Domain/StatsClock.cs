namespace ObjectDetection.Domain;

/// <summary>
/// 业务时钟：统一提供「上海时区的今天」和「业务时区」。
/// 由 <see cref="TimeProvider"/> 驱动，测试时可注入假时钟，
/// 因此「服务器在什么时区」不再影响统计口径。
/// </summary>
public sealed class StatsClock
{
    /// <summary>业务时区：所有「一天」按上海本地时间切分</summary>
    private static readonly TimeZoneInfo Shanghai =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");

    private readonly TimeProvider _time;

    public StatsClock(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>业务时区</summary>
    public TimeZoneInfo Zone => Shanghai;

    /// <summary>业务时区下的「今天」</summary>
    public DateOnly TodayLocal => DailyWindow.LocalDate(_time.GetUtcNow(), Shanghai);
}

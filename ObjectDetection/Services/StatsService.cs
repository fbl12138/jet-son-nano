using Microsoft.EntityFrameworkCore;
using ObjectDetection.Data;
using ObjectDetection.Dtos;

namespace ObjectDetection.Services;

public sealed class StatsService : IStatsService
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");

    private readonly AppDbContext _db;

    public StatsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<StatsDtos> GetComplianceAsync(
        DateTime? date, CancellationToken cancellationToken = default)
    {
        var day = (date ?? TimeZoneInfo.ConvertTime(DateTime.UtcNow, Tz).Date).Date;
        var (start, end) = DayRange(day);

        var persons = await _db.Detections.AsNoTracking()
            .CountAsync(d => d.LabelCode == "person" && d.DetectedAt >= start && d.DetectedAt < end, cancellationToken);
        var violations = await _db.Violations.AsNoTracking()
            .CountAsync(v => v.DetectedAt >= start && v.DetectedAt < end, cancellationToken);
        var devices = await _db.Detections.AsNoTracking()
            .Where(d => d.DetectedAt >= start && d.DetectedAt < end)
            .Select(d => d.DeviceId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new StatsDtos
        {
            Date = day.ToString("yyyy-MM-dd"),
            Devices = devices,
            PersonDetections = persons,
            Violations = violations,
            Compliant = Math.Max(0, persons - violations),
            ComplianceRate = Rate(persons, violations)
        };
    }

    public async Task<List<DailyStatDto>> GetDailyAsync(
        int days, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, 31);
        var result = new List<DailyStatDto>(days);
        var today = DateTime.Today;

        for (var i = days - 1; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            var (start, end) = DayRange(day);

            var persons = await _db.Detections.AsNoTracking()
                .CountAsync(d => d.LabelCode == "person" && d.DetectedAt >= start && d.DetectedAt < end, cancellationToken);
            var violations = await _db.Violations.AsNoTracking()
                .CountAsync(v => v.DetectedAt >= start && v.DetectedAt < end, cancellationToken);

            result.Add(new DailyStatDto
            {
                Date = day.ToString("yyyy-MM-dd"),
                PersonDetections = persons,
                Violations = violations,
                ComplianceRate = Rate(persons, violations)
            });
        }

        return result;
    }

    /// <summary>把本地日期换算成 UTC 的 [起, 止) 区间</summary>
    private static (DateTime StartUtc, DateTime EndUtc) DayRange(DateTime localDay)
    {
        var startLocal = new DateTime(localDay.Year, localDay.Month, localDay.Day,
            0, 0, 0, DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, Tz);
        return (startUtc, startUtc.AddDays(1));
    }

    /// <summary>合规率，保留 4 位；无人次时返回 0</summary>
    private static double Rate(int persons, int violations)
    {
        if (persons <= 0) return 0;
        var compliant = Math.Max(0, persons - violations);
        return Math.Round((double)compliant / persons, 4);
    }
}

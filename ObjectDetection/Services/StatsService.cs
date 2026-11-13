using Microsoft.EntityFrameworkCore;
using ObjectDetection.Data;
using ObjectDetection.Domain;
using ObjectDetection.Dtos;

namespace ObjectDetection.Services;

public sealed class StatsService : IStatsService
{
    /// <summary>业务时区：统计口径按上海本地的「一天」切分</summary>
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");

    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public StatsService(AppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<StatsDtos> GetComplianceAsync(
        DateTime? date, CancellationToken cancellationToken = default)
    {
        var day = date is DateTime d
            ? DateOnly.FromDateTime(d)
            : DailyWindow.LocalDate(_time.GetUtcNow(), Tz);

        var (start, end) = DailyWindow.ForLocalDate(day, Tz);

        var persons = await _db.Detections.AsNoTracking()
            .CountAsync(x => x.LabelCode == "person" && x.DetectedAt >= start && x.DetectedAt < end, cancellationToken);
        var violations = await _db.Violations.AsNoTracking()
            .CountAsync(v => v.DetectedAt >= start && v.DetectedAt < end, cancellationToken);
        var devices = await _db.Detections.AsNoTracking()
            .Where(x => x.DetectedAt >= start && x.DetectedAt < end)
            .Select(x => x.DeviceId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new StatsDtos
        {
            Date = day.ToString("yyyy-MM-dd"),
            Devices = devices,
            PersonDetections = persons,
            Violations = violations,
            Compliant = ComplianceMath.Compliant(persons, violations),
            ComplianceRate = ComplianceMath.Rate(persons, violations)
        };
    }

    public async Task<List<DailyStatDto>> GetDailyAsync(
        int days, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, 31);

        var result = new List<DailyStatDto>(days);
        var today = DateOnly.FromDateTime(DateTime.Today);

        for (var i = days - 1; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            var (start, end) = DailyWindow.ForLocalDate(day, Tz);

            var persons = await _db.Detections.AsNoTracking()
                .CountAsync(x => x.LabelCode == "person" && x.DetectedAt >= start && x.DetectedAt < end, cancellationToken);
            var violations = await _db.Violations.AsNoTracking()
                .CountAsync(v => v.DetectedAt >= start && v.DetectedAt < end, cancellationToken);

            result.Add(new DailyStatDto
            {
                Date = day.ToString("yyyy-MM-dd"),
                PersonDetections = persons,
                Violations = violations,
                ComplianceRate = ComplianceMath.Rate(persons, violations)
            });
        }

        return result;
    }
}

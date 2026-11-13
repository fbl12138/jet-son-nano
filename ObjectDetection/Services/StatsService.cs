using Microsoft.EntityFrameworkCore;
using ObjectDetection.Data;
using ObjectDetection.Domain;
using ObjectDetection.Dtos;

namespace ObjectDetection.Services;

public sealed class StatsService : IStatsService
{
    private readonly AppDbContext _db;
    private readonly StatsClock _clock;

    public StatsService(AppDbContext db, StatsClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<StatsDtos> GetComplianceAsync(
        DateTime? date, CancellationToken cancellationToken = default)
    {
        var day = date is DateTime d
            ? DateOnly.FromDateTime(d)
            : _clock.TodayLocal;

        var (start, end) = DailyWindow.ForLocalDate(day, _clock.Zone);

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

        // 修复点：原先用 DateTime.Today（服务器本地日期）。
        // 容器默认 UTC 时，上海已经跨天而服务器还没跨，这里会整体错一天。
        var today = _clock.TodayLocal;

        for (var i = days - 1; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            var (start, end) = DailyWindow.ForLocalDate(day, _clock.Zone);

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

using Microsoft.AspNetCore.Authentication;
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

        var today = _clock.TodayLocal;
        var from = today.AddDays(-(days - 1));

        // 历史部分：一次查询拿全（按天汇总，跨设备求和）
        var history = await _db.DailyStats.AsNoTracking()
            .Where(s => s.StatDate >= from && s.StatDate < today)   // 不含今天
            .GroupBy(s => s.StatDate)
            .Select(g => new
            {
                Date = g.Key,
                Persons = g.Sum(x => x.Persons),
                Violations = g.Sum(x => x.Violations)
            })
            .ToDictionaryAsync(x => x.Date, x => (x.Persons, x.Violations), cancellationToken);

        // 今天：聚合任务只回填到昨天，所以实时算
        var (todayStart, todayEnd) = DailyWindow.ForLocalDate(today, _clock.Zone);
        var todayPersons = await _db.Detections.AsNoTracking()
            .CountAsync(x => x.LabelCode == "person"
                          && x.DetectedAt >= todayStart && x.DetectedAt < todayEnd, cancellationToken);
        var todayViolations = await _db.Violations.AsNoTracking()
            .CountAsync(v => v.DetectedAt >= todayStart && v.DetectedAt < todayEnd, cancellationToken);

        var result = new List<DailyStatDto>(days);
        for (var i = days - 1; i >= 0; i--)
        {
            var day = today.AddDays(-i);

            int persons;
            int violations;
            if (day == today)
            {
                persons = todayPersons;
                violations = todayViolations;
            }
            else if (history.TryGetValue(day, out var h))
            {
                persons = h.Persons;
                violations = h.Violations;
            }
            else
            {
                // 这一天没有任何设备上报过，补 0 保证前端图表连续
                persons = 0;
                violations = 0;
            }

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


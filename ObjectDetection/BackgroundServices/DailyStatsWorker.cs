using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ObjectDetection.Data;
using ObjectDetection.Domain;
using ObjectDetection.Models;
using ObjectDetection.Option;

namespace ObjectDetection.BackgroundServices
{
    public class DailyStatsWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DailyStatsWorker> _logger;
        private readonly IOptions<MonitoringOptions> _options;
        private readonly StatsClock _clock;
  
        public DailyStatsWorker(IServiceScopeFactory f,ILogger<DailyStatsWorker> logger,IOptions<MonitoringOptions> options,StatsClock clock)
        {
            _scopeFactory = f;
            _logger = logger;
            _options = options;
            _clock = clock;
        }
        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            while(!ct.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await BackfillAsync(db, _options.Value.StatsBackfillDays, ct);
                    await CleanupAsync(db, ct);
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex, "DailyStatsWorker failed");
                }
                await Task.Delay(TimeSpan.FromHours(1), ct); // 每小时执行一次
            }
        }
        //回填统计数据 扫描人数 违规人数 
        private async Task BackfillAsync(AppDbContext db, int days, CancellationToken ct)
        {
            var today = _clock.TodayLocal;
            var from = today.AddDays(-days);

            var (fromUtc, _) = DailyWindow.ForLocalDate(from, _clock.Zone);
            var (todayUtc, _) = DailyWindow.ForLocalDate(today, _clock.Zone);

            // 业务时区偏移（上海无夏令时，全天恒定）
            var offsetHours = _clock.Zone.BaseUtcOffset.TotalHours;

            // 关键 1：不再按天循环，一次查出全部天的数据，用 SQL 按「业务日」分组
            var persons = await db.Detections.AsNoTracking()
                .Where(d => d.LabelCode == "person" && d.DetectedAt >= fromUtc && d.DetectedAt < todayUtc)
                .GroupBy(d => new { d.DeviceId, Day = d.DetectedAt.AddHours(offsetHours).Date })
                .Select(g => new { g.Key.DeviceId, g.Key.Day, Count = g.Count() })
                .ToListAsync(ct);

            var violations = await db.Violations.AsNoTracking()
                .Where(v => v.DetectedAt >= fromUtc && v.DetectedAt < todayUtc)
                .GroupBy(v => new { v.DeviceId, Day = v.DetectedAt.AddHours(offsetHours).Date })
                .Select(g => new { g.Key.DeviceId, g.Key.Day, Count = g.Count() })
                .ToListAsync(ct);

            // 关键 2：一次读入已有聚合行，消灭 N+1
            var existing = await db.DailyStats
                .Where(s => s.StatDate >= from && s.StatDate < today)
                .ToDictionaryAsync(s => (s.StatDate, s.DeviceId), ct);

            var personCounts = persons.ToDictionary(
                x => (DateOnly.FromDateTime(x.Day), x.DeviceId), x => x.Count);
            var violationCounts = violations.ToDictionary(
                x => (DateOnly.FromDateTime(x.Day), x.DeviceId), x => x.Count);

            // 关键 3：循环里只碰内存，不碰数据库
            foreach (var key in personCounts.Keys.Union(violationCounts.Keys))
            {
                var p = personCounts.GetValueOrDefault(key);
                var v = violationCounts.GetValueOrDefault(key);

                if (!existing.TryGetValue(key, out var row))
                {
                    row = new DailyStat { StatDate = key.Item1, DeviceId = key.Item2 };
                    db.DailyStats.Add(row);
                    existing[key] = row;      // ← 别忘了登记，否则会重复插入
                }

                row.Persons = p;
                row.Violations = v;
                row.Compliant = ComplianceMath.Compliant(p, v);
                row.UpdatedAt = _clock.UtcNow;
            }

            // 关键 4：整个区间一次提交
            await db.SaveChangesAsync(ct);
        }
        private static async Task CleanupAsync(AppDbContext db,CancellationToken ct)
        {
            //扫描记录保存三十天 分批次删除
            var cutDet = DateTime.UtcNow.AddDays(-30);
            while (true)
            {
              var c = await db.Database.ExecuteSqlRawAsync(
                  "Delete Top (5000) FROM Detections where DetectedAt < {0}",cutDet);
               if(c < 5000) break;//这批删除的不够5000个 说明删完了 退出
               await Task.Delay(200, ct); //删满了5000个 休息会继续删
            }
            //违规记录保留180天
            var cutVio = DateTime.UtcNow.AddDays(-180);
            while (true)
            {
                var c = await db.Database.ExecuteSqlRawAsync(
                    "Delete Top (5000) FROM Violations where DetectedAt < {0}", cutVio);
                if (c < 5000) break;
                await Task.Delay(200, ct);
            }
        }
    }
}

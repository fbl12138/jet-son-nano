using Microsoft.EntityFrameworkCore;
using ObjectDetection.Data;
using ObjectDetection.Domain;
using ObjectDetection.Models;

namespace ObjectDetection.BackgroundServices
{
    public class DailyStatsWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DailyStatsWorker> _logger;
        private static readonly TimeZoneInfo Tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");

        public DailyStatsWorker(IServiceScopeFactory f,ILogger<DailyStatsWorker> logger)
        {
            _scopeFactory = f;
            _logger = logger;
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
                    await BackfillAsync(db, 7, ct);
                    await CleanupAsync(db, ct);
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex, "DailyStatsWorker failed");
                }
                await Task.Delay(TimeSpan.FromHours(1), ct); // 每小时执行一次
            }
        }
        //回填最近7天的统计数据 扫描人数 违规人数 
        private static async Task BackfillAsync(AppDbContext db,int days,CancellationToken ct)
        {
            var todayLocal = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(DateTime.UtcNow, Tz)); // 上海时间的“今天”
            for(int i = 1;i<=days;i++)
            {
                var day = todayLocal.AddDays(-i);
                var startLocal = day.ToDateTime(TimeOnly.MinValue);
                var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, Tz);
                var endUtc = startUtc.AddDays(1);
                var persons = await db.Detections.AsNoTracking()
                    .Where(d=>d.LabelCode == "person" && d.DetectedAt >= startUtc && d.DetectedAt < endUtc)
                    .GroupBy(d=>d.DeviceId)
                    .Select(d=>new { DeviceId = d.Key, Count = d.Count() }).ToListAsync(ct);
                var vios = await db.Violations.AsNoTracking()
                    .Where(v => v.DetectedAt >= startUtc && v.DetectedAt < endUtc)
                    .GroupBy(v => v.DeviceId)
                    .Select(v => new { DeviceId = v.Key, Count = v.Count() }).ToListAsync(ct);
                var deviceIds = persons.Select(p => p.DeviceId).Union(vios.Select(v => v.DeviceId)).Distinct();
                foreach(var deviceId in deviceIds)
                {
                    var p = persons.FirstOrDefault(p => p.DeviceId == deviceId)?.Count ?? 0;
                    var v = vios.FirstOrDefault(v => v.DeviceId == deviceId)?.Count ?? 0;

                    var row = await db.DailyStats.FirstOrDefaultAsync(s => s.StatDate == day && s.DeviceId == deviceId, ct);
                    if (row is null)
                    {
                        row = new DailyStat { StatDate = day, DeviceId = deviceId };
                        db.DailyStats.Add(row);
                    }
                    row.Persons = p;
                    row.Violations = v;
                    row.Compliant = ComplianceMath.Compliant(p, v);
                    row.UpdatedAt = DateTime.UtcNow;
                }
                await db.SaveChangesAsync(ct);
            }
            
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

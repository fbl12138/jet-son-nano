using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ObjectDetection.Data;
using ObjectDetection.Dtos;
using System.Runtime.CompilerServices;

namespace ObjectDetection.Controllers
{
   
        [ApiController]
        [Route("api/stats")]
        [Authorize]
        public class StatsController : ControllerBase
        {
            private readonly AppDbContext _db;
            private static readonly TimeZoneInfo Tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
            public StatsController(AppDbContext db)
            {
                _db = db;
            }
            private static (DateTime Statutc,DateTime EndUtc) DayRange(DateTime localDay)
            {
                var startLocal = new DateTime(localDay.Year,localDay.Month,localDay.Day,
                        0, 0, 0, DateTimeKind.Unspecified);
                var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, Tz);
                return (startUtc, startUtc.AddDays(1));
            }
            private static double Rate(int persons, int violations)
            {
                if (persons <= 0) return 0;
                var compliant = Math.Max(0, persons - violations);
                return Math.Round((double)compliant / persons, 4);
            }
            [HttpGet("compliance")]
            public async Task<ActionResult<StatsDtos>> Compliance([FromQuery] DateTime? date)
            {
                var day = (date ?? TimeZoneInfo.ConvertTime(DateTime.UtcNow,Tz).Date).Date;
                var (start,end) = DayRange(day);
                var persons = await _db.Detections.AsNoTracking()
                    .CountAsync(d => d.LabelCode == "person" && d.DetectedAt >= start && d.DetectedAt < end);
                var violations = await _db.Violations.AsNoTracking().CountAsync(v => v.DetectedAt >= start && v.DetectedAt < end);
                var device = await _db.Detections.AsNoTracking().Where(d => d.DetectedAt >= start && d.DetectedAt < end).Select(d => d.DeviceId).Distinct().CountAsync();
                return Ok(new StatsDtos
                {
                    Date = day.ToString("yyyy-MM-dd"),
                    Devices = device,
                    PersonDetections = persons,
                    Violations = violations,
                    Compliant = Math.Max(0, persons - violations),
                    ComplianceRate = Rate(persons, violations)
                });
            }
            [HttpGet("daily")]
            public async Task<ActionResult<List<DailyStatDto>>> Daily([FromQuery] int days = 7)
            {
                days = Math.Clamp(days, 1, 31);
                var result = new List<DailyStatDto>();
                var today = DateTime.Today;
                for(int i = days - 1;i>=0; i--)
                {
                    var day = today.AddDays(-i);
                    var (start, end) = DayRange(day);
                    var persons = await _db.Detections.AsNoTracking().CountAsync(d => d.LabelCode == "person" && d.DetectedAt >= start && d.DetectedAt < end);
                    var violations = await _db.Violations.AsNoTracking().CountAsync(v => v.DetectedAt >= start && v.DetectedAt < end);
                    result.Add(new DailyStatDto
                    {
                        Date = day.ToString("yyyy-MM-dd"),
                        PersonDetections = persons,
                        Violations = violations,
                        ComplianceRate = Rate(persons, violations)
                    });
                }
                return Ok(result);
            }
        }
    }


using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ObjectDetection.Dtos;
using ObjectDetection.Services;

namespace ObjectDetection.Controllers
{
    [ApiController]
    [Route("api/stats")]
    [Authorize]
    public class StatsController : ControllerBase
    {
        private readonly IStatsService _stats;

        public StatsController(IStatsService stats)
        {
            _stats = stats;
        }

        [HttpGet("compliance")]
        public async Task<ActionResult<StatsDtos>> Compliance(
            [FromQuery] DateTime? date, CancellationToken ct)
            => Ok(await _stats.GetComplianceAsync(date, ct));

        [HttpGet("daily")]
        public async Task<ActionResult<List<DailyStatDto>>> Daily(
            [FromQuery] int days = 7, CancellationToken ct = default)
            => Ok(await _stats.GetDailyAsync(days, ct));
    }
}

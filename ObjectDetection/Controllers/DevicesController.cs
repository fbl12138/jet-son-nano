using Microsoft.AspNetCore.Mvc;
using ObjectDetection.Data;
using ObjectDetection.Dtos;
using Microsoft.EntityFrameworkCore;
using ObjectDetection.Models;
using Microsoft.AspNetCore.Authorization;
using ObjectDetection.Option;
using Microsoft.Extensions.Options;


namespace ObjectDetection.Controllers
{
    [ApiController]
    [Route("api/devices")]
    [Authorize]

    public class DevicesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly  MonitoringOptions _options;
        public DevicesController(AppDbContext db,IOptions<MonitoringOptions> options)
        {
            _db = db;
            _options = options.Value;
        }
        //将实体转化为Dto
        private static ComplianceRuleDto ToDto(ComplianceRule r) => new ComplianceRuleDto
        {
            DeviceId = r.DeviceId,
            RequireMask = r.RequireMask,
            RequireHelmet = r.RequireHelmet,
            MinConfidence = r.MinConfidence,
            ConfirmFrames = r.ConfirmFrames,
            Enabled = r.Enabled,
            UpdatedAt = r.UpdatedAt
        };
        //设备报告
        [HttpPost("heartbeat")]
        [AllowAnonymous]
        public async Task<IActionResult> HeartBeat([FromBody] HeartbeatRequestDto dto, [FromHeader(Name = "X-Api-Key")] string? apikey)
        {
            if (string.IsNullOrWhiteSpace(apikey))
                return Unauthorized(new { message = "缺少ApiKey" });
            var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceCode == dto.DeviceCode);
            if (device is null || device.ApiKey != apikey)
                return Unauthorized(new { message = "设备未授权" });
            var now = DateTime.UtcNow;
            device.LastHeartbeatAt = now;
            device.Status = "Online";
            await _db.SaveChangesAsync();
            return Ok(new
            {
                deviceCode = device.DeviceCode,
                status = device.Status,
                serverTime = now
            });
        }
        //设备列表
        [HttpGet]
        public async Task<ActionResult<List<DeviceDto>>> Get()
        {
            var now = DateTime.UtcNow;
            var rows = await _db.Devices.AsNoTracking().OrderBy(d => d.Id).Select(s => new
            {
                s.Id,
                s.DeviceCode,
                s.Name,
                s.Location,
                s.Status,
                s.LastHeartbeatAt
            }).ToListAsync();
            //在内存里算离线时长EF不能算时间相减
            var items = rows.Select(d =>
            {
                int? offlineSeconds = d.LastHeartbeatAt is null ? null : (int)(now - d.LastHeartbeatAt.Value).TotalSeconds;

                var online = offlineSeconds is not null && offlineSeconds < _options.HeartbeattimeoutSeconds;
                return new DeviceDto
                {
                    Id = d.Id,
                    DeviceCode = d.DeviceCode,
                    Name = d.Name,
                    Location = d.Location,
                    // 以实时判定为准，覆盖库里可能滞后的 Status
                    Status = online ? "Online" : "Offline",
                    LastHeartbeatAt = d.LastHeartbeatAt,
                    OfflineSeconds = online ? null : offlineSeconds
                };
            }).ToList();
            return Ok(items);
        }
        //合规规则 一台设备一份
        [HttpGet("{id:int}/rule")]
       
        public async Task<ActionResult<ComplianceRuleDto>> GetRule(int id)
        {
            //区分设备不存在和设备存在但没配规则
            if (!await _db.Devices.AnyAsync(d => d.Id == id))
                return NotFound(new { message = "设备不存在" });
            //只读查询加AsNoTracking
            var rule = await _db.ComplianceRules.AsNoTracking()
                .FirstOrDefaultAsync(r => r.DeviceId == id);
            if (rule is null)
                return NotFound(new { message = "该设备未配置规则" });
            return Ok(ToDto(rule));
        }
        //修改规则对应一台设备
        [HttpPut("{id:int}/rule")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ComplianceRuleDto>> PutRule(int id, [FromBody] UpdateRuleRequestDto dto)
        {
            //查设备是否存在
            if (!await _db.Devices.AnyAsync(d => d.Id == id))
                return NotFound(new { message = "设备不存在" });
            var rule = await _db.ComplianceRules.FirstOrDefaultAsync(r => r.DeviceId == id);
            if(rule is null)
            {
                rule = new ComplianceRule { DeviceId = id };
                _db.ComplianceRules.Add(rule);
             }
            rule.RequireMask = dto.RequireMask;
            rule.RequireHelmet = dto.RequireHelmet;
            rule.MinConfidence = dto.MinConfidence;
            rule.ConfirmFrames = dto.ConfirmFrames;
            rule.Enabled = dto.Enabled;
            rule.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(ToDto(rule));

        }
    }
}

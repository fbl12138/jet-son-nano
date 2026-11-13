using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ObjectDetection.Dtos;
using ObjectDetection.Services;

namespace ObjectDetection.Controllers
{
    [ApiController]
    [Route("api/devices")]
    [Authorize]
    public class DevicesController : ControllerBase
    {
        private readonly IDeviceService _devices;

        public DevicesController(IDeviceService devices)
        {
            _devices = devices;
        }

        // 设备心跳
        [HttpPost("heartbeat")]
        [AllowAnonymous]
        public async Task<ActionResult<HeartbeatResponseDto>> HeartBeat(
            [FromBody] HeartbeatRequestDto dto,
            [FromHeader(Name = "X-Api-Key")] string? apikey,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(apikey))
                return Unauthorized(new { message = "缺少ApiKey" });

            var result = await _devices.HeartbeatAsync(dto.DeviceCode, apikey, ct);
            return result is null ? Unauthorized(new { message = "设备未授权" }) : Ok(result);
        }

        // 设备列表
        [HttpGet]
        public async Task<ActionResult<List<DeviceDto>>> Get(CancellationToken ct)
            => Ok(await _devices.ListAsync(ct));

        // 合规规则 一台设备一份
        [HttpGet("{id:int}/rule")]
        public async Task<ActionResult<ComplianceRuleDto>> GetRule(int id, CancellationToken ct)
        {
            var (deviceExists, rule) = await _devices.GetRuleAsync(id, ct);
            if (!deviceExists) return NotFound(new { message = "设备不存在" });
            if (rule is null) return NotFound(new { message = "该设备未配置规则" });
            return Ok(rule);
        }

        // 修改规则对应一台设备（upsert）
        [HttpPut("{id:int}/rule")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ComplianceRuleDto>> PutRule(
            int id, [FromBody] UpdateRuleRequestDto dto, CancellationToken ct)
        {
            var rule = await _devices.UpsertRuleAsync(id, dto, ct);
            return rule is null ? NotFound(new { message = "设备不存在" }) : Ok(rule);
        }
    }
}

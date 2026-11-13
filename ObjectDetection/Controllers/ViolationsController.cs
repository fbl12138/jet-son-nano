using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ObjectDetection.Dtos;
using ObjectDetection.Models;
using ObjectDetection.Services;

namespace ObjectDetection.Controllers
{
    [ApiController]
    [Route("api/violations")]
    [Authorize]
    public class ViolationsController : ControllerBase
    {
        private readonly IViolationService _violation;
        public ViolationsController(IViolationService violation)
        {
            _violation = violation;
        }
        [HttpGet]
        public async Task<ActionResult<PagedResult<ViolationDto>>> Get(
            [FromQuery] ViolationStatus? status,
            [FromQuery] int? deviceId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default
            )
        {
            return Ok(await _violation.QueryAsync(status, deviceId, from, to, page, pageSize, cancellationToken));
        }
        [HttpGet("{id:long}")]
        public async Task<ActionResult<ViolationDetailDto>> GetById(long id,CancellationToken cancellationToken = default)
        {
            var dto = await _violation.GetByIdAsync(id, cancellationToken);
            if (dto is null) return NotFound(new { message = "违规记录不存在" });
            return Ok(dto);
        }
        [HttpPatch("{id:long}/handle")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Patch(long id, [FromBody] HandleViolationRequestDto dto,CancellationToken cancellationToken = default)
        {// token 取用户是 HTTP 层的事，留在控制器
            if (!int.TryParse(User.FindFirst("sub")?.Value, out var userId))
                return Unauthorized(new { message = "Token中缺少用户标识" });

            var (outcome, v) = await _violation.HandleAsync(id, userId, dto,cancellationToken);

            switch (outcome)
            {
                case HandleOutcome.NotFound:
                    return NotFound(new { message = "违规记录不存在" });
                case HandleOutcome.AlreadyHandled:
                    return Conflict(new { message = $"该违规当前状态是{v!.Status},不能重复处理" });
                default:
                    return Ok(new
                    {
                        id = v!.Id,
                        status = v.Status,
                        handledBy = userId,
                        handledAt = v.HandledAt,
                        remark = v.Remark
                    });
            }
        }
        }
    }


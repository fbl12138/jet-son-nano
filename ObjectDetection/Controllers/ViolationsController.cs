using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ObjectDetection.Data;
using ObjectDetection.Dtos;

namespace ObjectDetection.Controllers
{
    [ApiController]
    [Route("api/violations")]
    [Authorize]
    public class ViolationsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public ViolationsController(AppDbContext db)
        {
            _db = db;
        }
        [HttpGet]
        public async Task<ActionResult<PagedResult<ViolationDto>>> Get(
            [FromQuery] string? status,
            [FromQuery] int? deviceId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20
            )
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var q = _db.Violations.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(v => v.Status == status);
            if (deviceId is int did)
                q = q.Where(v => v.DeviceId == did);
            if (from is DateTime f)
                q = q.Where(v => v.DetectedAt >= f);
            if (to is DateTime t)
                q = q.Where(v => v.DetectedAt <= t);
            var total = await q.CountAsync();
            var items = await q.OrderByDescending(v => v.DetectedAt).ThenByDescending(v => v.Id).Skip((page - 1) * pageSize).Take(pageSize).Select
                (v => new ViolationDto
                {
                    Id = v.Id,
                    DeviceName = v.Device!.Name,
                    TrackId = v.TrackId,
                    ViolationTypes = v.ViolationTypes,
                    Confidence = v.Confidence,
                    DetectedAt = v.DetectedAt,
                    Status = v.Status,
                    ImagePath = v.ImagePath
                }).ToListAsync();
            return Ok(new PagedResult<ViolationDto>
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            });
        }
        [HttpGet("{id:long}")]
        public async Task<ActionResult<ViolationDetailDto>> GetById(long id)
        {
            var dto = await _db.Violations.AsNoTracking().Where(c => c.Id == id).Select
                (v => new ViolationDetailDto
                {
                    Id = v.Id,
                    DeviceId = v.DeviceId,
                    DeviceName = v.Device != null ? v.Device.Name : string.Empty,
                    TrackId = v.TrackId,
                    ViolationTypes = v.ViolationTypes,
                    PersonCount = v.PersonCount,
                    Confidence = v.Confidence,
                    ImagePath = v.ImagePath,
                    DetectedAt = v.DetectedAt,
                    CreatedAt = v.CreatedAt,
                    Status = v.Status,
                    HandledBy = v.HandledBy,
                    HandledAt = v.HandledAt,
                    Remark = v.Remark
                }).FirstOrDefaultAsync();
            if (dto is null) return NotFound(new { message = "违规记录不存在" });
            return Ok(dto);
        }
        [HttpPatch("{id:long}/handle")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Patch(long id, [FromBody] HandleViolationRequestDto dto)
        {
            var v = await _db.Violations.FirstOrDefaultAsync(x => x.Id == id);
            if (v is null) return NotFound(new { message = "违规记录不存在" });
            if (v.Status != "Open") return Conflict(new { message = $"该违规当前状态是{v.Status},不能重复处理" });
            if (!int.TryParse(User.FindFirst("sub")?.Value,out var userId))
                return Unauthorized(new { message = "Token中缺少用户标识" });
            v.Status = "Handled";
            v.HandledBy = userId;
            v.HandledAt = DateTime.UtcNow;   // 存 UTC，和 CreatedAt 一致（DetectedAt 是设备时间，Day 13 统一）
            v.Remark = dto.Remark;
            await _db.SaveChangesAsync();
            return Ok(new
            {
                id = v.Id,
                status = v.Status,
                handledBy = userId,
                handledAt = v.HandledAt,
                remark = v.Remark
            });
        }
        }
    }


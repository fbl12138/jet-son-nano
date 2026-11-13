using Microsoft.EntityFrameworkCore;
using ObjectDetection.Dtos;
using ObjectDetection.Models;
using ObjectDetection.Data;

namespace ObjectDetection.Services
{
    public class ViolationService : IViolationService
    {
        private readonly AppDbContext _db;
        public ViolationService(AppDbContext db)
        {
            _db = db;
        }
        //分页查询
        public async Task<PagedResult<ViolationDto>> QueryAsync(ViolationStatus? status, int? deviceId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var q = _db.Violations.AsNoTracking();
            if (status is ViolationStatus s)
                q = q.Where(v => v.Status == s);
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
                }).ToListAsync(cancellationToken);
            return new PagedResult<ViolationDto>
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }
        //通过ID查违规记录
        public async Task<ViolationDetailDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
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
                }).FirstOrDefaultAsync(cancellationToken);
            return dto;
        }
        //处理违规记录
        public async Task<(HandleOutcome outcome, Violation? violation)> HandleAsync(long id, int userId, HandleViolationRequestDto dto, CancellationToken cancellationToken = default)
        {
            var v = await _db.Violations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (v is null) return (HandleOutcome.NotFound, v);
            if (v.Status != ViolationStatus.Open) return (HandleOutcome.AlreadyHandled, v);
            v.Status = ViolationStatus.Handled;
            v.HandledBy = userId;
            v.HandledAt = DateTime.UtcNow;   // 存 UTC，和 CreatedAt 一致（DetectedAt 是设备时间，Day 13 统一）
            v.Remark = dto.Remark;
            await _db.SaveChangesAsync(cancellationToken);
            return (HandleOutcome.Success, v);
        }
    }
}

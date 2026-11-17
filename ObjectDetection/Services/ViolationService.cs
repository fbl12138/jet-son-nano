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
        //用「条件更新」而不是「先查后改」：并发下两个管理员同时点处理，
        //只有一条 UPDATE 能命中（WHERE Status = Open），另一条受影响行数为 0 → 409。
        public async Task<(HandleOutcome outcome, Violation? violation)> HandleAsync(long id, int userId, HandleViolationRequestDto dto, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var affected = await _db.Violations
                .Where(v => v.Id == id && v.Status == ViolationStatus.Open)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(v => v.Status, ViolationStatus.Handled)
                    .SetProperty(v => v.HandledBy, userId)
                    .SetProperty(v => v.HandledAt, now)
                    .SetProperty(v => v.Remark, dto.Remark), cancellationToken);

            // ExecuteUpdate 不经过变更跟踪，重新读一次拿到最终状态（也用于区分「不存在」和「已处理」）
            var current = await _db.Violations.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

            if (affected == 0)
                return current is null
                    ? (HandleOutcome.NotFound, null)
                    : (HandleOutcome.AlreadyHandled, current);

            return (HandleOutcome.Success, current);
        }
    }
}

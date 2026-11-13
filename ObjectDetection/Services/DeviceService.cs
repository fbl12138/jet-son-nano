using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ObjectDetection.Data;
using ObjectDetection.Dtos;
using ObjectDetection.Models;
using ObjectDetection.Option;

namespace ObjectDetection.Services;

public sealed class DeviceService : IDeviceService
{
    private readonly AppDbContext _db;
    private readonly MonitoringOptions _options;

    public DeviceService(AppDbContext db, IOptions<MonitoringOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<HeartbeatResponseDto?> HeartbeatAsync(
        string deviceCode, string? apiKey, CancellationToken ct = default)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceCode == deviceCode, ct);
        // 设备不存在和 ApiKey 不匹配返回同一个结果，避免暴露"哪个设备编码存在"
        if (device is null || device.ApiKey != apiKey)
            return null;

        var now = DateTime.UtcNow;
        device.LastHeartbeatAt = now;
        device.Status = DeviceStatus.Online;
        await _db.SaveChangesAsync(ct);

        return new HeartbeatResponseDto
        {
            DeviceCode = device.DeviceCode,
            Status = device.Status,
            ServerTime = now
        };
    }

    public async Task<List<DeviceDto>> ListAsync(CancellationToken ct = default)
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
        }).ToListAsync(ct);

        // 离线时长要在内存里算：EF 无法把两个时间列相减翻译成 SQL
        return rows.Select(d =>
        {
            int? offlineSeconds = d.LastHeartbeatAt is null
                ? null
                : (int)(now - d.LastHeartbeatAt.Value).TotalSeconds;

            var online = offlineSeconds is not null
                         && offlineSeconds < _options.HeartbeatTimeoutSeconds;

            return new DeviceDto
            {
                Id = d.Id,
                DeviceCode = d.DeviceCode,
                Name = d.Name,
                Location = d.Location,
                // 以实时判定为准，覆盖库里可能滞后的 Status
                Status = online ? DeviceStatus.Online : DeviceStatus.Offline,
                LastHeartbeatAt = d.LastHeartbeatAt,
                OfflineSeconds = online ? null : offlineSeconds
            };
        }).ToList();
    }

    public async Task<(bool DeviceExists, ComplianceRuleDto? Rule)> GetRuleAsync(
        int deviceId, CancellationToken ct = default)
    {
        // 先区分"设备不存在"和"设备存在但没配规则"
        if (!await _db.Devices.AnyAsync(d => d.Id == deviceId, ct))
            return (false, null);

        var rule = await _db.ComplianceRules.AsNoTracking()
            .FirstOrDefaultAsync(r => r.DeviceId == deviceId, ct);

        return (true, rule is null ? null : ToDto(rule));
    }

    public async Task<ComplianceRuleDto?> UpsertRuleAsync(
        int deviceId, UpdateRuleRequestDto dto, CancellationToken ct = default)
    {
        if (!await _db.Devices.AnyAsync(d => d.Id == deviceId, ct))
            return null;

        var rule = await _db.ComplianceRules.FirstOrDefaultAsync(r => r.DeviceId == deviceId, ct);
        if (rule is null)
        {
            rule = new ComplianceRule { DeviceId = deviceId };
            _db.ComplianceRules.Add(rule);
        }

        rule.RequireMask = dto.RequireMask;
        rule.RequireHelmet = dto.RequireHelmet;
        rule.MinConfidence = dto.MinConfidence;
        rule.ConfirmFrames = dto.ConfirmFrames;
        rule.Enabled = dto.Enabled;
        rule.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return ToDto(rule);
    }

    private static ComplianceRuleDto ToDto(ComplianceRule r) => new()
    {
        DeviceId = r.DeviceId,
        RequireMask = r.RequireMask,
        RequireHelmet = r.RequireHelmet,
        MinConfidence = r.MinConfidence,
        ConfirmFrames = r.ConfirmFrames,
        Enabled = r.Enabled,
        UpdatedAt = r.UpdatedAt
    };
}

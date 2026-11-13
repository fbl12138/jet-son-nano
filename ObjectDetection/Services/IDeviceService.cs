using ObjectDetection.Dtos;

namespace ObjectDetection.Services;

/// <summary>设备相关业务：心跳、列表、合规规则。</summary>
public interface IDeviceService
{
    /// <summary>
    /// 处理设备心跳。设备不存在或 ApiKey 不匹配时返回 null（由控制器转成 401）。
    /// </summary>
    Task<HeartbeatResponseDto?> HeartbeatAsync(
        string deviceCode, string? apiKey, CancellationToken ct = default);

    /// <summary>设备列表，Status 按最近心跳实时判定，不直接信任库里的值。</summary>
    Task<List<DeviceDto>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// 读取某设备的合规规则。
    /// DeviceExists=false 表示设备不存在；DeviceExists=true 且 Rule=null 表示设备存在但未配置规则。
    /// </summary>
    Task<(bool DeviceExists, ComplianceRuleDto? Rule)> GetRuleAsync(
        int deviceId, CancellationToken ct = default);

    /// <summary>新增或更新合规规则。返回 null 表示设备不存在。</summary>
    Task<ComplianceRuleDto?> UpsertRuleAsync(
        int deviceId, UpdateRuleRequestDto dto, CancellationToken ct = default);
}

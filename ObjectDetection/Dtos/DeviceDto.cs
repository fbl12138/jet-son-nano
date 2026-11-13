using ObjectDetection.Models;

namespace ObjectDetection.Dtos
{
    public class DeviceDto
    {
        public int Id { get; set; }
        public string DeviceCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Location { get; set; }
        public DeviceStatus Status { get; set; } = DeviceStatus.Offline;
        public DateTime? LastHeartbeatAt { get; set; }
        // 距离上次心跳多少秒；从没心跳过就是 null
        public int? OfflineSeconds { get; set; }
    }
}

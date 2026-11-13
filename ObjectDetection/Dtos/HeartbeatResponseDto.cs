using ObjectDetection.Models;

namespace ObjectDetection.Dtos
{
    /// <summary>心跳成功后的响应体</summary>
    public class HeartbeatResponseDto
    {
        public string DeviceCode { get; set; } = string.Empty;
        public DeviceStatus Status { get; set; }
        /// <summary>服务端当前 UTC 时间，供设备校准时钟</summary>
        public DateTime ServerTime { get; set; }
    }
}

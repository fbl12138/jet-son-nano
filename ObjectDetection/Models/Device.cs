namespace ObjectDetection.Models
{
    public class Device
    {
        public int Id { get; set; }
        public string DeviceCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Location { get; set; }
        public string ApiKey { get; set; } = string.Empty;
        public DeviceStatus Status { get; set; } = DeviceStatus.Offline;
        public DateTime? LastHeartbeatAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Detection> Detections { get; set; } = new List<Detection>();
        public ICollection<Violation> Violations { get; set; } = new List<Violation>();
    }
}

namespace ObjectDetection.Models
{
    public class Violation
    {
        public long Id { get; set; }
        public int DeviceId { get; set; }
        public string? TrackId { get; set; }
        public string ViolationTypes { get; set; } = string.Empty;
        public int PersonCount { get; set; } = 1;
        public string? ImagePath { get; set; }
        public decimal? Confidence { get; set; }
        public DateTime DetectedAt { get; set; }
        public ViolationStatus Status { get; set; } = ViolationStatus.Open;
        public int? HandledBy { get; set; }
        public DateTime? HandledAt { get; set; }
        public string? Remark { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Device? Device { get; set; }
    }
}

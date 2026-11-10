namespace ObjectDetection.Dtos
{
    public class ViolationDetailDto
    {
        public long Id { get; set; }
        public int DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string? TrackId { get; set; }
        public string ViolationTypes { get; set; } = string.Empty;
        public int PersonCount { get; set; }
        public decimal? Confidence { get; set; }
        public string? ImagePath { get; set; }
        public DateTime DetectedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? HandledBy { get; set; }
        public DateTime? HandledAt { get; set; }
        public string? Remark { get; set; }
    }
}

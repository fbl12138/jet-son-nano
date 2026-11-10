namespace ObjectDetection.Dtos
{
    public class ViolationDto
    {
        public long Id { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string? TrackId { get; set; }
        public string ViolationTypes { get; set; } = string.Empty;
        public decimal? Confidence { get; set; }
        public DateTime DetectedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
    }
}
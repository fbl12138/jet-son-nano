namespace ObjectDetection.Models
{
    public class Detection
    {
        public long Id { get; set; }
        public int DeviceId { get; set; }
        public string? TrackId { get; set; }
        public string LabelCode { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
        public int BBoxX { get; set; }
        public int BBoxY { get; set; }
        public int BBoxW { get; set; }
        public int BBoxH { get; set; }
        public string? ImagePath { get; set; }
        public DateTime DetectedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

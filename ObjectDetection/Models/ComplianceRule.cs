namespace ObjectDetection.Models
{
    public class ComplianceRule
    {
        public int Id { get; set; }
        public int DeviceId { get; set; }
        public bool RequireMask { get; set; } = true;
        public bool RequireHelmet { get; set; }
        public decimal MinConfidence { get; set; } = 0.5m;
        public int ConfirmFrames { get; set; } = 3;
        public bool Enabled { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Device? Device { get; set; }
    }
}

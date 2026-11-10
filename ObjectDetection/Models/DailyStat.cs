namespace ObjectDetection.Models
{
    public class DailyStat
    {
        public int Id { get; set; }
        public DateOnly StatDate { get; set; }
        public int DeviceId { get; set; }
        public int Persons {get;set; }
        public int Violations { get; set; }
        public int Compliant { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    }
}

using Microsoft.AspNetCore.Components.Web;

namespace ObjectDetection.Dtos
{
    public class ComplianceRuleDto
    {
        public int DeviceId { get; set; }
        public bool RequireMask { get; set; }
        public bool RequireHelmet { get; set; }
        public decimal MinConfidence { get; set; }
        public int ConfirmFrames { get; set; }
        public bool Enabled { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace ObjectDetection.Dtos
{
    public class UpdateRuleRequestDto
    {
        public bool RequireMask { get; set; }
        public bool RequireHelmet { get; set; }
        [Range(typeof(decimal), "0", "1", ErrorMessage = "MinConfidence必须在0~1之间")]
        public decimal MinConfidence { get; set; } = 0.5m;
        public bool Enabled { get; set; } = true;
    }
}

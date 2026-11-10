using System.ComponentModel.DataAnnotations;

namespace ObjectDetection.Dtos
{
    public class UpdateRuleRequestDto
    {
        public bool RequireMask { get; set; }
        public bool RequireHelmet { get; set; }
        [Range(typeof(decimal), "0", "1", ErrorMessage = "MinConfidence必须在0~1之间")]
        public decimal MinConfidence { get; set; } = 0.5m;
        [Range(1, 10, ErrorMessage = "ConfirmFrames必须在1~10之间")]
        public int ConfirmFrames { get; set; } = 3;
        public bool Enabled { get; set; } = true;
    }
}

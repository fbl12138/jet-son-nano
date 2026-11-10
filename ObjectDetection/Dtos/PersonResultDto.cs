using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using System.Text.Json.Serialization;

namespace ObjectDetection.Dtos
{
    //一个人+口罩判定结果
    public class PersonResultDto
    {
        public string? TrackId { get; set; }
        [JsonPropertyName("bbox")]
        public int[] BBox { get; set; } = Array.Empty<int>();
        public Dictionary<string, double?> Attributes { get; set; } = new(); //属性表{"mask",0.92},null
        public bool Compliant { get; set; } //是否合规
        public List<string> Violations { get; set; } = new(); //违规类型列表
        public double? Confidence { get; set; } //可信度
        public string? ImageUrl { get; set; }   //路径
    }
}

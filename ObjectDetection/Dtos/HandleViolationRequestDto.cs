using System.ComponentModel.DataAnnotations;

namespace ObjectDetection.Dtos
{
    public class HandleViolationRequestDto
    {
        // 和库里 HasMaxLength(300) 对齐：在入口挡住，比让 SQL 抛截断异常好
        [MaxLength(300, ErrorMessage = "备注不能超过 300 字")]
        public string? Remark { get; set; }
    }
}

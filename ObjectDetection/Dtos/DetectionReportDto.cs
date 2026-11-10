namespace ObjectDetection.Dtos
{
    //Jetson 一次上报的完整请求体
    public class DetectionReportDto
    {
        public string DeviceCode { get; set; } = string.Empty; //那台机器上报的
        public DateTime Timestamp { get; set; }     //检测发生时间
        public List<PersonResultDto> Persons { get; set; } = new(); //这批有多少人
    }
}

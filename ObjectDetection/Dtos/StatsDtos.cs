namespace ObjectDetection.Dtos
{
    public class StatsDtos
    {
        public string Date { get; set; } = "";          // 本地日期 yyyy-MM-dd
        public int Devices { get; set; }                 // 当天有数据的设备数
        public int PersonDetections { get; set; }        // 检测人次
        public int Violations { get; set; }              // 违规条数
        public int Compliant { get; set; }               // 合规人次
        public double ComplianceRate { get; set; }       // 0~1，保留 4 位

    }
    public class DailyStatDto
    {
        public string Date { get; set; } = "";
        public int PersonDetections { get; set; }
        public int Violations { get; set; }
        public double ComplianceRate { get; set; }
    }
}

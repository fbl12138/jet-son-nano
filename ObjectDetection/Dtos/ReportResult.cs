namespace ObjectDetection.Dtos
{   
    //上报接口返回 告诉设备 存了几条 产生多少调违规
    public class ReportResult
    {
        public int Saved { get; set; }
        public int ViolationsCreated { get; set; }
    }
}

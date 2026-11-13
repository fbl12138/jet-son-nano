namespace ObjectDetection.Option
{
    public class MonitoringOptions
    {
        public const string SectionName = "Monitoring";
        //超过秒数问上报心跳判为离线
        public int HeartbeatTimeoutSeconds { get; set; } = 60;
        //心跳扫描间隔
        public int HeartbeatScanIntervalSeconds { get; set; } = 15;
        //检测明细保留天数
        public int DetectionRetentionDays { get; set; } = 30;
        //违规记录保留天数
        public int ViolationRetentionDays { get; set; } = 180;
        //统计回填天数
        public int StatsBackfillDays { get; set; } = 31;
    }
}

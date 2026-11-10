using ObjectDetection.Dtos;

namespace ObjectDetection.Services
{
    public interface IDetectionService
    {
        Task<ReportResult> SaveAsync(DetectionReportDto dto, string apiKey);
    }
}

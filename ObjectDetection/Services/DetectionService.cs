using ObjectDetection.Data;
using ObjectDetection.Dtos;
using Microsoft.EntityFrameworkCore;
using ObjectDetection.Models;
using ObjectDetection.Domain;


namespace ObjectDetection.Services
{
    public class DetectionService : IDetectionService
    {
        private readonly AppDbContext _db;
        public DetectionService(AppDbContext db) 
        {
            _db = db;
        }
        private static Detection BuildDetection(int deviceId, PersonResultDto p, string label, double conf, DateTime at)
            => new()
            {
                DeviceId = deviceId,
                TrackId = p.TrackId,
                LabelCode = label,
                Confidence = (decimal)conf,
                BBoxX = p.BBox.Length > 0 ? p.BBox[0] : 0,
                BBoxY = p.BBox.Length > 1 ? p.BBox[1] : 0,
                BBoxW = p.BBox.Length > 2 ? p.BBox[2] : 0,
                BBoxH = p.BBox.Length > 3 ? p.BBox[3] : 0,
                ImagePath = p.ImageUrl,
                DetectedAt = at

            };
        public async Task<ReportResult> SaveAsync(DetectionReportDto dto,string apiKey)
        {
            var device = await _db.Devices.FirstOrDefaultAsync(c => c.DeviceCode == dto.DeviceCode);
            if (device is null || !ApiKeyHasher.Verify(apiKey, device.ApiKeyHash))
                throw new UnauthorizedAccessException("设备未授权");
            var rule = await _db.ComplianceRules.FirstOrDefaultAsync(r => r.DeviceId == device.Id && r.Enabled);
            int violationCount = 0;
            var detectedAt = dto.Timestamp.Kind switch
            {
                DateTimeKind.Utc => dto.Timestamp,
                DateTimeKind.Local => dto.Timestamp.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dto.Timestamp, DateTimeKind.Utc)
            };
            foreach(var person in dto.Persons)
            {
                _db.Detections.Add(BuildDetection(device.Id, person, "person", 1.0, detectedAt));
                if (person.Attributes.TryGetValue("mask", out var maskConf) && maskConf is double mc)
                    _db.Detections.Add(BuildDetection(device.Id, person, "mask", mc, detectedAt));
                var types = ComplianceEvaluator.Evaluate(person.Attributes, rule);
                if (types.Count > 0)
                {
                    _db.Violations.Add(new Violation
                    {
                        DeviceId = device.Id,
                        TrackId = person.TrackId,
                        ViolationTypes = string.Join(",", types),
                        PersonCount = 1,
                        ImagePath = person.ImageUrl,
                        Confidence = person.Confidence is double pc ? (decimal)pc : null,
                        DetectedAt = detectedAt,
                        Status = ViolationStatus.Open
                    });
                    violationCount++;
                }
            }
            await _db.SaveChangesAsync();
            return new ReportResult
            {
                Saved = dto.Persons.Count,
                ViolationsCreated = violationCount
            };
        }
    
    }
}

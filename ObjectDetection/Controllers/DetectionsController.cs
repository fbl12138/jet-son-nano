using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ObjectDetection.Dtos;
using ObjectDetection.Services;
using System.Linq.Expressions;

namespace ObjectDetection.Controllers
{
    [ApiController]
    [Route("api/detections")]
    public class DetectionsController : ControllerBase
    {
        private readonly IDetectionService _dete;
        public DetectionsController(IDetectionService dete)
        {
            _dete = dete;
        }
        [HttpPost]
        [EnableRateLimiting("device")]
        public async Task<IActionResult> Report([FromBody] DetectionReportDto dto,[FromHeader(Name = "X-Api-Key")] string? apikey)
        {
            if (string.IsNullOrWhiteSpace(apikey))
                return Unauthorized(new { message = "缺少ApiKey" });

            var result = await _dete.SaveAsync(dto, apikey);
            return Created("/api/detections", result);
        }

    }
}

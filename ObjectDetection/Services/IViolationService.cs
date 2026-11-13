using ObjectDetection.Dtos;
using ObjectDetection.Models;

namespace ObjectDetection.Services
{
    //处理违规状态
    public enum HandleOutcome
    {
        Success, //处理成功
        NotFound, //记录不存在
        AlreadyHandled //已经处理过 不能重复处理
    }
    public interface IViolationService
    {
        Task<PagedResult<ViolationDto>> QueryAsync(ViolationStatus? status, int? deviceId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default);
        Task<ViolationDetailDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
        Task<(HandleOutcome outcome, Violation? violation)> HandleAsync(long id, int userId, HandleViolationRequestDto dto, CancellationToken cancellationToken = default);
     }
}

using ObjectDetection.Dtos;

namespace ObjectDetection.Services;

/// <summary>合规统计</summary>
public interface IStatsService
{
    /// <summary>指定日期（本地日）的合规概览；date 为 null 时取上海时区的今天。</summary>
    Task<StatsDtos> GetComplianceAsync(DateTime? date, CancellationToken cancellationToken = default);

    /// <summary>最近 N 天的逐日统计，days 会被钳制到 1~31。</summary>
    Task<List<DailyStatDto>> GetDailyAsync(int days, CancellationToken cancellationToken = default);
}

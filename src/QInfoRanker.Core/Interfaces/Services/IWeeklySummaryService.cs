using QInfoRanker.Core.Entities;

namespace QInfoRanker.Core.Interfaces.Services;

public interface IWeeklySummaryService
{
    Task<WeeklySummary?> GetCurrentWeekSummaryAsync(int keywordId, CancellationToken cancellationToken = default);
    Task<IEnumerable<WeeklySummary>> GetSummariesAsync(int? keywordId = null, int take = 10, CancellationToken cancellationToken = default);
    Task<WeeklySummary?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<WeeklySummary> GenerateSummaryAsync(int keywordId, CancellationToken cancellationToken = default);
    Task<WeeklySummary?> GenerateSummaryIfNeededAsync(int keywordId, CancellationToken cancellationToken = default);
}

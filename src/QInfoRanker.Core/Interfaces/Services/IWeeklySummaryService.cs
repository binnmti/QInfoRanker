using QInfoRanker.Core.Entities;

namespace QInfoRanker.Core.Interfaces.Services;

/// <summary>
/// 要約生成に関する診断情報
/// </summary>
public record SummaryDiagnostics(
    bool IsAzureOpenAIConfigured,
    string? DeploymentName,
    int NewsArticleCount,
    int TechArticleCount,
    int AcademicArticleCount,
    int TotalArticleCount,
    int MinRequiredArticles,
    bool CanGenerateSummary,
    string? BlockingReason
);

public interface IWeeklySummaryService
{
    Task<WeeklySummary?> GetCurrentWeekSummaryAsync(int keywordId, CancellationToken cancellationToken = default);
    Task<IEnumerable<WeeklySummary>> GetSummariesAsync(int? keywordId = null, int take = 10, CancellationToken cancellationToken = default);
    Task<WeeklySummary?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<WeeklySummary> GenerateSummaryAsync(int keywordId, CancellationToken cancellationToken = default);
    Task<WeeklySummary?> GenerateSummaryIfNeededAsync(int keywordId, CancellationToken cancellationToken = default);
    Task<int> DeleteByKeywordAsync(int keywordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 要約生成に関する診断情報を取得
    /// </summary>
    Task<SummaryDiagnostics> GetDiagnosticsAsync(int keywordId, CancellationToken cancellationToken = default);
}

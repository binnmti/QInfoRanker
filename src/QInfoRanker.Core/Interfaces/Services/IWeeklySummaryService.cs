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

    /// <summary>
    /// 指定キーワードの要約履歴を生成日時の降順で取得
    /// </summary>
    /// <param name="keywordId">キーワードID</param>
    /// <param name="take">取得件数</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>要約履歴（新しい順）</returns>
    Task<IReadOnlyList<WeeklySummary>> GetHistoryAsync(int keywordId, int take = 30, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定キーワードの次の要約を取得（現在より古い）
    /// </summary>
    /// <param name="keywordId">キーワードID</param>
    /// <param name="currentGeneratedAt">現在の要約の生成日時</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>次の要約（古い方向）、なければnull</returns>
    Task<WeeklySummary?> GetPreviousSummaryAsync(int keywordId, DateTime currentGeneratedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定キーワードの前の要約を取得（現在より新しい）
    /// </summary>
    /// <param name="keywordId">キーワードID</param>
    /// <param name="currentGeneratedAt">現在の要約の生成日時</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>前の要約（新しい方向）、なければnull</returns>
    Task<WeeklySummary?> GetNextSummaryAsync(int keywordId, DateTime currentGeneratedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定キーワードの最新の要約を取得
    /// </summary>
    /// <param name="keywordId">キーワードID</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>最新の要約、なければnull</returns>
    Task<WeeklySummary?> GetLatestSummaryAsync(int keywordId, CancellationToken cancellationToken = default);
}

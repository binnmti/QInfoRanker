using QInfoRanker.Core.Exceptions;

namespace QInfoRanker.Core.Events;

/// <summary>収集処理のフェーズ</summary>
public enum CollectionPhase
{
    /// <summary>キューで待機中</summary>
    Queued,
    /// <summary>ソースから収集中</summary>
    CollectingSource,
    /// <summary>ソースをスコアリング中</summary>
    ScoringSource,
    /// <summary>完了</summary>
    Completed,
    /// <summary>失敗</summary>
    Failed
}

/// <summary>
/// 収集進捗更新イベント
/// </summary>
public record CollectionProgressEvent(
    int KeywordId,
    string KeywordTerm,
    CollectionPhase Phase,
    string? CurrentSource,
    int SourceIndex,
    int TotalSources,
    int ArticlesCollected,
    int ArticlesScored,
    string Message
);

/// <summary>
/// ソース収集完了イベント
/// </summary>
public record SourceCompletedEvent(
    int KeywordId,
    string SourceName,
    int ArticleCount,
    int ScoredCount,
    bool HasError,
    string? ErrorMessage
);

/// <summary>
/// 記事スコアリング完了イベント
/// </summary>
public record ArticlesScoredEvent(
    int KeywordId,
    string SourceName,
    int ScoredCount,
    double AverageScore
);

/// <summary>
/// 収集完了イベント
/// </summary>
public record CollectionCompletedEvent(
    int KeywordId,
    string KeywordTerm,
    int TotalArticles,
    int ScoredArticles,
    double DurationSeconds,
    List<SourceResultSummary> SourceResults
);

/// <summary>
/// エラー発生イベント
/// </summary>
public record CollectionErrorEvent(
    int KeywordId,
    ErrorSeverity Severity,
    string Source,
    string Message,
    bool IsFatal
);

/// <summary>
/// ソース結果サマリ
/// </summary>
public record SourceResultSummary(
    string SourceName,
    int ArticleCount,
    int ScoredCount,
    bool Success,
    string? ErrorMessage
);

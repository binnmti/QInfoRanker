using QInfoRanker.Core.Exceptions;

namespace QInfoRanker.Core.Interfaces.Services;

public interface ICollectionQueue
{
    ValueTask EnqueueAsync(CollectionJob job, CancellationToken cancellationToken = default);
    ValueTask<CollectionJob> DequeueAsync(CancellationToken cancellationToken);
    CollectionStatus? GetStatus(int keywordId);
    IEnumerable<CollectionStatus> GetAllStatuses();
    void UpdateStatus(int keywordId, CollectionStatus status);
    void ClearStatus(int keywordId);
}

public class CollectionJob
{
    public int KeywordId { get; set; }
    public string KeywordTerm { get; set; } = string.Empty;
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>デバッグモード（各ソースから少数の記事のみ収集）</summary>
    public bool DebugMode { get; set; } = false;

    /// <summary>デバッグモード時の各ソースからの収集上限</summary>
    public int DebugArticleLimit { get; set; } = 3;
}

public class CollectionStatus
{
    public int KeywordId { get; set; }
    public string KeywordTerm { get; set; } = string.Empty;
    public CollectionState State { get; set; }

    /// <summary>現在処理中のソース名</summary>
    public string? CurrentSource { get; set; }

    /// <summary>現在のソースインデックス（1始まり）</summary>
    public int SourceIndex { get; set; }

    /// <summary>総ソース数</summary>
    public int TotalSources { get; set; }

    /// <summary>収集済み記事数</summary>
    public int ArticlesCollected { get; set; }

    /// <summary>スコアリング済み記事数</summary>
    public int ArticlesScored { get; set; }

    /// <summary>ユーザー向けメッセージ</summary>
    public string? Message { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>ソースごとのエラー一覧</summary>
    public List<SourceError> SourceErrors { get; set; } = new();

    /// <summary>致命的エラーが発生したか</summary>
    public bool HasFatalError { get; set; }

    /// <summary>致命的エラーのメッセージ</summary>
    public string? FatalErrorMessage { get; set; }

    /// <summary>ソースごとの収集結果</summary>
    public List<SourceCollectionResult> SourceResults { get; set; } = new();

    /// <summary>取得中の記事プレビュー（スクレイピング段階で表示用）</summary>
    public List<FetchedArticlePreview> FetchedPreviews { get; set; } = new();

    /// <summary>フィルタ通過した記事（採点待ち）</summary>
    public List<PendingScoringPreview> PendingScoringPreviews { get; set; } = new();

    /// <summary>スコアリング完了した記事（即時表示用）</summary>
    public List<ScoredArticlePreview> ScoredArticlePreviews { get; set; } = new();

    /// <summary>API入力トークン数（累計）</summary>
    public int TotalInputTokens { get; set; }

    /// <summary>API出力トークン数（累計）</summary>
    public int TotalOutputTokens { get; set; }

    /// <summary>推定コスト（USD）- gpt-4o-mini料金</summary>
    public decimal EstimatedCostUsd =>
        (TotalInputTokens * 0.00000015m) + (TotalOutputTokens * 0.0000006m);
}

/// <summary>フィルタ通過した記事プレビュー（採点待ち）</summary>
public class PendingScoringPreview
{
    public int ArticleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public int? NativeScore { get; set; }
    public double RelevanceScore { get; set; }
    public DateTime PassedFilterAt { get; set; } = DateTime.UtcNow;
}

/// <summary>取得中の記事プレビュー（スクレイピング段階で表示）</summary>
public class FetchedArticlePreview
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public int? NativeScore { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>スコアリング完了した記事プレビュー（即時表示用）</summary>
public class ScoredArticlePreview
{
    public int ArticleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public int? NativeScore { get; set; }
    public double RelevanceScore { get; set; }
    public double LlmScore { get; set; }
    public double FinalScore { get; set; }
    public string? SummaryJa { get; set; }
    public DateTime ScoredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>ソースごとのエラー情報</summary>
public class SourceError
{
    public string SourceName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public ErrorSeverity Severity { get; set; } = ErrorSeverity.Error;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>ソースごとの収集結果</summary>
public class SourceCollectionResult
{
    public string SourceName { get; set; } = string.Empty;
    public int ArticleCount { get; set; }
    public int ScoredCount { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

public enum CollectionState
{
    Queued,
    Collecting,
    Scoring,
    Completed,
    Failed
}

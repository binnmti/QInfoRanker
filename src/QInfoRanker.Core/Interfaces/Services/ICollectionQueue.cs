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

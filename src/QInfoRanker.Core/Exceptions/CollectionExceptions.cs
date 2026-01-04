namespace QInfoRanker.Core.Exceptions;

/// <summary>
/// エラーの重大度
/// </summary>
public enum ErrorSeverity
{
    /// <summary>特定記事のエラー（スキップして続行）</summary>
    Warning,
    /// <summary>特定ソースのエラー（そのソースをスキップして続行）</summary>
    Error,
    /// <summary>AI使用不可など致命的エラー（処理終了）</summary>
    Critical
}

/// <summary>
/// 収集関連の基底例外
/// </summary>
public abstract class CollectionException : Exception
{
    public ErrorSeverity Severity { get; }
    public string SourceName { get; }

    protected CollectionException(string message, ErrorSeverity severity, string sourceName, Exception? innerException = null)
        : base(message, innerException)
    {
        Severity = severity;
        SourceName = sourceName;
    }
}

/// <summary>
/// AI/スコアリングサービス使用不可（致命的エラー）
/// 処理を終了し、ユーザーに通知する必要がある
/// </summary>
public class ScoringServiceUnavailableException : CollectionException
{
    public ScoringServiceUnavailableException(string message, Exception? innerException = null)
        : base(message, ErrorSeverity.Critical, "ScoringService", innerException)
    {
    }
}

/// <summary>
/// 特定ソースの収集エラー
/// そのソースをスキップして他のソースの収集を続行可能
/// </summary>
public class SourceCollectionException : CollectionException
{
    public SourceCollectionException(string sourceName, string message, Exception? innerException = null)
        : base(message, ErrorSeverity.Error, sourceName, innerException)
    {
    }
}

/// <summary>
/// 特定記事の処理エラー
/// その記事をスキップして他の記事の処理を続行可能
/// </summary>
public class ArticleProcessingException : CollectionException
{
    public string? ArticleUrl { get; }
    public string? ArticleTitle { get; }

    public ArticleProcessingException(string sourceName, string message, string? articleUrl = null, string? articleTitle = null, Exception? innerException = null)
        : base(message, ErrorSeverity.Warning, sourceName, innerException)
    {
        ArticleUrl = articleUrl;
        ArticleTitle = articleTitle;
    }
}

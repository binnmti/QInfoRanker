using QInfoRanker.Core.Events;

namespace QInfoRanker.Core.Interfaces.Services;

/// <summary>
/// 収集進捗通知サービスのインターフェース
/// </summary>
public interface ICollectionProgressNotifier
{
    /// <summary>進捗更新を通知</summary>
    Task NotifyProgressAsync(CollectionProgressEvent progress);

    /// <summary>ソース収集完了を通知</summary>
    Task NotifySourceCompletedAsync(SourceCompletedEvent evt);

    /// <summary>記事スコアリング完了を通知</summary>
    Task NotifyArticlesScoredAsync(ArticlesScoredEvent evt);

    /// <summary>記事取得を通知（スクレイピング中）</summary>
    Task NotifyArticlesFetchedAsync(ArticlesFetchedEvent evt);

    /// <summary>フィルタ通過記事を通知（採点待ち）</summary>
    Task NotifyArticlesPassedFilterAsync(ArticlesPassedFilterEvent evt);

    /// <summary>記事の品質評価完了を通知（採点待ちから削除、スコア済みリストに追加）</summary>
    Task NotifyArticlesQualityScoredAsync(int keywordId, IEnumerable<ScoredArticlePreview> scoredArticles);

    /// <summary>トークン使用量を更新（累計）</summary>
    Task NotifyTokenUsageAsync(int keywordId, int inputTokens, int outputTokens);

    /// <summary>収集完了を通知</summary>
    Task NotifyCompletedAsync(CollectionCompletedEvent evt);

    /// <summary>エラーを通知</summary>
    Task NotifyErrorAsync(CollectionErrorEvent error);
}

/// <summary>
/// SignalRクライアントへの通知インターフェース
/// </summary>
public interface ICollectionProgressClient
{
    /// <summary>進捗更新</summary>
    Task OnProgressUpdate(CollectionProgressEvent progress);

    /// <summary>ソース収集完了</summary>
    Task OnSourceCompleted(SourceCompletedEvent evt);

    /// <summary>記事スコアリング完了</summary>
    Task OnArticlesScored(ArticlesScoredEvent evt);

    /// <summary>記事取得（スクレイピング中）</summary>
    Task OnArticlesFetched(ArticlesFetchedEvent evt);

    /// <summary>フィルタ通過（採点待ち）</summary>
    Task OnArticlesPassedFilter(ArticlesPassedFilterEvent evt);

    /// <summary>収集完了</summary>
    Task OnCollectionCompleted(CollectionCompletedEvent evt);

    /// <summary>エラー発生</summary>
    Task OnError(CollectionErrorEvent error);
}

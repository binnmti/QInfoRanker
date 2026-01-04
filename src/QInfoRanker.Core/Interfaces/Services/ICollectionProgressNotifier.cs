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

    /// <summary>収集完了</summary>
    Task OnCollectionCompleted(CollectionCompletedEvent evt);

    /// <summary>エラー発生</summary>
    Task OnError(CollectionErrorEvent error);
}

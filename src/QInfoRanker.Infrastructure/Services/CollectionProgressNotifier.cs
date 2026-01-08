using Microsoft.Extensions.Logging;
using QInfoRanker.Core.Events;
using QInfoRanker.Core.Exceptions;
using QInfoRanker.Core.Interfaces.Services;

namespace QInfoRanker.Infrastructure.Services;

/// <summary>
/// 収集進捗通知サービス（ポーリングベース）
/// CollectionStatusを更新し、UIがポーリングで取得する方式
/// </summary>
public class CollectionProgressNotifier : ICollectionProgressNotifier
{
    private readonly ICollectionQueue _queue;
    private readonly ILogger<CollectionProgressNotifier> _logger;

    public CollectionProgressNotifier(
        ICollectionQueue queue,
        ILogger<CollectionProgressNotifier> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    public Task NotifyProgressAsync(CollectionProgressEvent progress)
    {
        // CollectionStatusを更新
        var status = _queue.GetStatus(progress.KeywordId);
        if (status != null)
        {
            status.CurrentSource = progress.CurrentSource;
            status.SourceIndex = progress.SourceIndex;
            status.TotalSources = progress.TotalSources;
            status.ArticlesCollected = progress.ArticlesCollected;
            status.ArticlesScored = progress.ArticlesScored;
            status.Message = progress.Message;
            status.State = progress.Phase switch
            {
                CollectionPhase.CollectingSource => CollectionState.Collecting,
                CollectionPhase.ScoringSource => CollectionState.Scoring,
                CollectionPhase.Completed => CollectionState.Completed,
                CollectionPhase.Failed => CollectionState.Failed,
                _ => status.State
            };
            _queue.UpdateStatus(progress.KeywordId, status);
        }

        _logger.LogDebug("Progress: {Keyword} - {Phase} - {Source} ({SourceIndex}/{TotalSources})",
            progress.KeywordTerm, progress.Phase, progress.CurrentSource, progress.SourceIndex, progress.TotalSources);

        return Task.CompletedTask;
    }

    public Task NotifySourceCompletedAsync(SourceCompletedEvent evt)
    {
        // ソース結果をステータスに追加
        var status = _queue.GetStatus(evt.KeywordId);
        if (status != null)
        {
            status.SourceResults.Add(new SourceCollectionResult
            {
                SourceName = evt.SourceName,
                ArticleCount = evt.ArticleCount,
                ScoredCount = evt.ScoredCount,
                Success = !evt.HasError,
                ErrorMessage = evt.ErrorMessage
            });
            _queue.UpdateStatus(evt.KeywordId, status);
        }

        _logger.LogDebug("Source completed: {Source} - {Count} articles, {Scored} scored, HasError={HasError}",
            evt.SourceName, evt.ArticleCount, evt.ScoredCount, evt.HasError);

        return Task.CompletedTask;
    }

    public Task NotifyArticlesScoredAsync(ArticlesScoredEvent evt)
    {
        var status = _queue.GetStatus(evt.KeywordId);
        if (status != null)
        {
            // スコア済み件数を累積更新
            status.ArticlesScored += evt.ScoredCount;

            // このソースの採点待ちプレビューをクリア（スコアリング完了）
            status.PendingScoringPreviews.RemoveAll(p => p.SourceName == evt.SourceName);

            _queue.UpdateStatus(evt.KeywordId, status);
        }

        _logger.LogDebug("Articles scored: {Source} - {Count} articles, avg={AvgScore:F1}",
            evt.SourceName, evt.ScoredCount, evt.AverageScore);

        return Task.CompletedTask;
    }

    public Task NotifyArticlesFetchedAsync(ArticlesFetchedEvent evt)
    {
        var status = _queue.GetStatus(evt.KeywordId);
        if (status != null)
        {
            // 取得した記事のプレビューを追加
            foreach (var article in evt.Articles)
            {
                status.FetchedPreviews.Add(new FetchedArticlePreview
                {
                    Title = article.Title,
                    Url = article.Url,
                    SourceName = evt.SourceName,
                    PublishedAt = article.PublishedAt,
                    NativeScore = article.NativeScore,
                    FetchedAt = DateTime.UtcNow
                });
            }
            _queue.UpdateStatus(evt.KeywordId, status);
        }

        _logger.LogDebug("Articles fetched: {Source} - {Count} articles",
            evt.SourceName, evt.Articles.Count);

        return Task.CompletedTask;
    }

    public Task NotifyArticlesPassedFilterAsync(ArticlesPassedFilterEvent evt)
    {
        var status = _queue.GetStatus(evt.KeywordId);
        if (status != null)
        {
            // フィルタリング完了したので、このソースの取得中プレビューをクリア
            status.FetchedPreviews.RemoveAll(p => p.SourceName == evt.SourceName);

            // フィルタ通過記事をプレビューに追加
            foreach (var article in evt.Articles)
            {
                status.PendingScoringPreviews.Add(new PendingScoringPreview
                {
                    ArticleId = article.ArticleId,
                    Title = article.Title,
                    Url = article.Url,
                    SourceName = evt.SourceName,
                    PublishedAt = article.PublishedAt,
                    NativeScore = article.NativeScore,
                    RelevanceScore = article.RelevanceScore,
                    PassedFilterAt = DateTime.UtcNow
                });
            }
            _queue.UpdateStatus(evt.KeywordId, status);
        }

        _logger.LogDebug("Articles passed filter: {Source} - {Count} articles",
            evt.SourceName, evt.Articles.Count);

        return Task.CompletedTask;
    }

    public Task NotifyArticlesQualityScoredAsync(int keywordId, IEnumerable<ScoredArticlePreview> scoredArticles)
    {
        var status = _queue.GetStatus(keywordId);
        if (status != null)
        {
            var scoredList = scoredArticles.ToList();
            var idsToRemove = new HashSet<int>(scoredList.Select(a => a.ArticleId));

            // 採点待ちリストから削除
            var removedCount = status.PendingScoringPreviews.RemoveAll(p => idsToRemove.Contains(p.ArticleId));

            // スコア済みリストに追加（即時表示用）
            status.ScoredArticlePreviews.AddRange(scoredList);

            if (removedCount > 0 || scoredList.Any())
            {
                _queue.UpdateStatus(keywordId, status);
                _logger.LogDebug("Moved {Count} articles from pending to scored list", scoredList.Count);
            }
        }

        return Task.CompletedTask;
    }

    public Task NotifyTokenUsageAsync(int keywordId, int inputTokens, int outputTokens)
    {
        var status = _queue.GetStatus(keywordId);
        if (status != null)
        {
            status.TotalInputTokens += inputTokens;
            status.TotalOutputTokens += outputTokens;
            _queue.UpdateStatus(keywordId, status);
        }

        return Task.CompletedTask;
    }

    public Task NotifyCompletedAsync(CollectionCompletedEvent evt)
    {
        var status = _queue.GetStatus(evt.KeywordId);
        if (status != null)
        {
            status.State = CollectionState.Completed;
            status.CompletedAt = DateTime.UtcNow;
            status.ArticlesCollected = evt.TotalArticles;
            status.ArticlesScored = evt.ScoredArticles;
            status.Message = $"完了: {evt.TotalArticles}件収集, {evt.ScoredArticles}件スコアリング";
            // 完了時にプレビューをクリア
            status.FetchedPreviews.Clear();
            status.PendingScoringPreviews.Clear();
            status.ScoredArticlePreviews.Clear();
            _queue.UpdateStatus(evt.KeywordId, status);
        }

        _logger.LogInformation("Collection completed: {Keyword} - {Total} articles, {Scored} scored, {Duration:F1}s",
            evt.KeywordTerm, evt.TotalArticles, evt.ScoredArticles, evt.DurationSeconds);

        return Task.CompletedTask;
    }

    public Task NotifyErrorAsync(CollectionErrorEvent error)
    {
        var status = _queue.GetStatus(error.KeywordId);
        if (status != null)
        {
            if (error.IsFatal)
            {
                status.HasFatalError = true;
                status.FatalErrorMessage = error.Message;
                status.State = CollectionState.Failed;
            }
            else
            {
                status.SourceErrors.Add(new SourceError
                {
                    SourceName = error.Source,
                    ErrorMessage = error.Message,
                    Severity = error.Severity,
                    OccurredAt = DateTime.UtcNow
                });
            }
            _queue.UpdateStatus(error.KeywordId, status);
        }

        if (error.IsFatal)
        {
            _logger.LogError("Fatal error: {Source} - {Message}", error.Source, error.Message);
        }
        else
        {
            _logger.LogWarning("Error: {Source} - {Message}", error.Source, error.Message);
        }

        return Task.CompletedTask;
    }
}

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
        _logger.LogDebug("Articles scored: {Source} - {Count} articles, avg={AvgScore:F1}",
            evt.SourceName, evt.ScoredCount, evt.AverageScore);

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

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using QInfoRanker.Core.Entities;
using QInfoRanker.Core.Events;
using QInfoRanker.Core.Exceptions;
using QInfoRanker.Core.Interfaces.Collectors;
using QInfoRanker.Core.Interfaces.Services;

namespace QInfoRanker.Infrastructure.Services;

public class CollectionService : ICollectionService
{
    private readonly IKeywordService _keywordService;
    private readonly ISourceService _sourceService;
    private readonly IArticleService _articleService;
    private readonly IScoringService _scoringService;
    private readonly IEnumerable<ICollector> _collectors;
    private readonly ICollectionProgressNotifier _progressNotifier;
    private readonly ILogger<CollectionService> _logger;

    public CollectionService(
        IKeywordService keywordService,
        ISourceService sourceService,
        IArticleService articleService,
        IScoringService scoringService,
        IEnumerable<ICollector> collectors,
        ICollectionProgressNotifier progressNotifier,
        ILogger<CollectionService> logger)
    {
        _keywordService = keywordService;
        _sourceService = sourceService;
        _articleService = articleService;
        _scoringService = scoringService;
        _collectors = collectors;
        _progressNotifier = progressNotifier;
        _logger = logger;
    }

    public async Task CollectAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting collection for all active keywords");

        var keywords = await _keywordService.GetActiveAsync(cancellationToken);

        foreach (var keyword in keywords)
        {
            await CollectForKeywordAsync(keyword.Id, cancellationToken);
        }

        _logger.LogInformation("Completed collection for all keywords");
    }

    public Task CollectForKeywordAsync(int keywordId, CancellationToken cancellationToken = default)
    {
        return CollectForKeywordAsync(keywordId, debugMode: false, debugArticleLimit: 3, cancellationToken);
    }

    public async Task CollectForKeywordAsync(int keywordId, bool debugMode, int debugArticleLimit = 3, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var keyword = await _keywordService.GetByIdAsync(keywordId, cancellationToken);
        if (keyword == null)
        {
            _logger.LogWarning("Keyword {KeywordId} not found", keywordId);
            return;
        }

        _logger.LogInformation("Starting collection for keyword: {Keyword} (DebugMode={DebugMode})", keyword.Term, debugMode);

        var sources = await _sourceService.GetByKeywordIdAsync(keywordId, cancellationToken);
        var activeSources = sources.Where(s => s.IsActive).ToList();
        var sourceDict = activeSources.ToDictionary(s => s.Id);
        var searchTerms = keyword.GetAllSearchTerms().ToList();
        var since = DateTime.UtcNow.AddMonths(-1);

        // AIヘルスチェック（最初に1回）
        try
        {
            await _scoringService.HealthCheckAsync(cancellationToken);
            _logger.LogInformation("AI service health check passed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI service is unavailable");
            await _progressNotifier.NotifyErrorAsync(new CollectionErrorEvent(
                keywordId, ErrorSeverity.Critical, "AI Service",
                $"AIサービスに接続できません: {ex.Message}", IsFatal: true));
            throw new ScoringServiceUnavailableException(ex.Message, ex);
        }

        var allSavedArticles = new List<Article>();
        var sourceResults = new List<SourceResultSummary>();

        _logger.LogInformation("Searching with terms: {Terms}", string.Join(", ", searchTerms));

        // ソースごとに収集→スコアリング
        for (int i = 0; i < activeSources.Count; i++)
        {
            var source = activeSources[i];
            var sourceIndex = i + 1;
            var hasSourceError = false;
            string? sourceErrorMsg = null;
            var sourceArticles = new List<Article>();
            var scoredCount = 0;

            // 進捗通知: 収集開始
            await _progressNotifier.NotifyProgressAsync(new CollectionProgressEvent(
                keywordId, keyword.Term, CollectionPhase.CollectingSource,
                source.Name, sourceIndex, activeSources.Count,
                allSavedArticles.Count, allSavedArticles.Count(a => a.LlmScore.HasValue),
                $"{source.Name} から収集中..."
            ));

            // ソースから記事を収集
            try
            {
                foreach (var searchTerm in searchTerms)
                {
                    try
                    {
                        var articles = await CollectFromSourceSafeAsync(source, searchTerm, since, debugMode, debugArticleLimit, cancellationToken);
                        sourceArticles.AddRange(articles);

                        // デバッグモードで上限に達したら終了
                        if (debugMode && sourceArticles.Count >= debugArticleLimit)
                        {
                            sourceArticles = sourceArticles.Take(debugArticleLimit).ToList();
                            break;
                        }
                    }
                    catch (ArticleProcessingException ex)
                    {
                        _logger.LogWarning(ex, "記事処理エラー (続行): {Source}, {Term}", source.Name, searchTerm);
                    }
                }
            }
            catch (SourceCollectionException ex)
            {
                hasSourceError = true;
                sourceErrorMsg = ex.Message;
                _logger.LogWarning(ex, "ソース収集エラー (スキップ): {Source}", source.Name);
                await _progressNotifier.NotifyErrorAsync(new CollectionErrorEvent(
                    keywordId, ErrorSeverity.Error, source.Name, ex.Message, IsFatal: false));
            }

            // 記事が収集できた場合、DB保存とスコアリング
            if (sourceArticles.Any())
            {
                // DB保存
                var savedArticles = (await _articleService.CreateBatchAsync(sourceArticles, cancellationToken)).ToList();
                _logger.LogInformation("Saved {Count} new articles from {Source}", savedArticles.Count, source.Name);

                if (savedArticles.Any())
                {
                    // Sourceをセット
                    foreach (var article in savedArticles)
                    {
                        article.Source = source;
                    }

                    // 進捗通知: スコアリング開始
                    await _progressNotifier.NotifyProgressAsync(new CollectionProgressEvent(
                        keywordId, keyword.Term, CollectionPhase.ScoringSource,
                        source.Name, sourceIndex, activeSources.Count,
                        allSavedArticles.Count + savedArticles.Count,
                        allSavedArticles.Count(a => a.LlmScore.HasValue),
                        $"{source.Name} をスコアリング中..."
                    ));

                    // ソース単位でスコアリング
                    try
                    {
                        await ScoreArticlesForSourceAsync(savedArticles, source, searchTerms, cancellationToken);
                        scoredCount = savedArticles.Count(a => a.LlmScore.HasValue);

                        var avgScore = savedArticles.Where(a => a.FinalScore > 0).Select(a => a.FinalScore).DefaultIfEmpty(0).Average();
                        await _progressNotifier.NotifyArticlesScoredAsync(new ArticlesScoredEvent(
                            keywordId, source.Name, scoredCount, avgScore));
                    }
                    catch (ScoringServiceUnavailableException)
                    {
                        throw; // 致命的エラーは再スロー
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "スコアリングエラー (フォールバック): {Source}", source.Name);
                        await ApplyFallbackScoresAsync(savedArticles, source, cancellationToken);
                        scoredCount = savedArticles.Count;
                    }

                    allSavedArticles.AddRange(savedArticles);
                }
            }

            // ソース完了通知
            await _progressNotifier.NotifySourceCompletedAsync(new SourceCompletedEvent(
                keywordId, source.Name, sourceArticles.Count, scoredCount, hasSourceError, sourceErrorMsg));

            sourceResults.Add(new SourceResultSummary(
                source.Name, sourceArticles.Count, scoredCount, !hasSourceError, sourceErrorMsg));
        }

        stopwatch.Stop();

        // 完了通知
        await _progressNotifier.NotifyCompletedAsync(new CollectionCompletedEvent(
            keywordId, keyword.Term,
            allSavedArticles.Count,
            allSavedArticles.Count(a => a.LlmScore.HasValue),
            stopwatch.Elapsed.TotalSeconds,
            sourceResults));

        _logger.LogInformation(
            "Collection completed for '{Keyword}': {Total} articles, {Scored} scored, {Duration:F1}s",
            keyword.Term, allSavedArticles.Count, allSavedArticles.Count(a => a.LlmScore.HasValue), stopwatch.Elapsed.TotalSeconds);
    }

    private async Task<IEnumerable<Article>> CollectFromSourceSafeAsync(
        Source source,
        string searchTerm,
        DateTime since,
        bool debugMode,
        int debugArticleLimit,
        CancellationToken cancellationToken)
    {
        var collector = _collectors.FirstOrDefault(c => c.CanHandle(source));
        if (collector == null)
        {
            throw new SourceCollectionException(source.Name, $"No collector found for source: {source.Name}");
        }

        try
        {
            var articles = await collector.CollectAsync(source, searchTerm, since, cancellationToken);
            var articleList = articles.ToList();

            if (debugMode && articleList.Count > debugArticleLimit)
            {
                articleList = articleList.Take(debugArticleLimit).ToList();
            }

            return articleList;
        }
        catch (HttpRequestException ex)
        {
            throw new SourceCollectionException(source.Name, $"HTTP error: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new SourceCollectionException(source.Name, "Request timeout", ex);
        }
        catch (OperationCanceledException)
        {
            throw; // キャンセルは伝播
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error collecting from {Source}", source.Name);
            throw new SourceCollectionException(source.Name, $"Collection error: {ex.Message}", ex);
        }
    }

    private async Task ScoreArticlesForSourceAsync(
        List<Article> articles,
        Source source,
        List<string> searchTerms,
        CancellationToken cancellationToken)
    {
        if (!articles.Any()) return;

        _logger.LogInformation("Scoring {Count} articles from {Source}", articles.Count, source.Name);

        // 2段階スコアリング実行
        var result = await _scoringService.EvaluateTwoStageAsync(
            articles, searchTerms, source.HasServerSideFiltering, cancellationToken);

        _logger.LogInformation(
            "Scoring completed for {Source}: {Relevant}/{Total} relevant, API calls: {ApiCalls}",
            source.Name, result.RelevanceResult.RelevantCount, articles.Count, result.TotalApiCalls);

        // 最終スコア計算と保存
        foreach (var article in articles)
        {
            if (article.LlmScore != null)
            {
                article.FinalScore = _scoringService.CalculateFinalScore(article, source);
            }
            else
            {
                // LLMスコアがない場合のフォールバック計算
                var relevanceScore = article.RelevanceScore ?? 5;
                var normalizedNative = source.HasNativeScore && article.NativeScore.HasValue
                    ? _scoringService.NormalizeNativeScore(article.NativeScore, source.Name)
                    : 0;
                article.FinalScore = (relevanceScore * 5) + (normalizedNative * 0.3) + (source.AuthorityWeight * 10);
            }

            await _articleService.UpdateAsync(article, cancellationToken);
        }
    }

    private async Task ApplyFallbackScoresAsync(
        List<Article> articles,
        Source source,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("Applying fallback scores for {Count} articles from {Source}", articles.Count, source.Name);

        foreach (var article in articles)
        {
            // 関連性スコアとネイティブスコアを使ったフォールバック
            var relevanceScore = article.RelevanceScore ?? 5;
            var normalizedNative = source.HasNativeScore && article.NativeScore.HasValue
                ? _scoringService.NormalizeNativeScore(article.NativeScore, source.Name)
                : 0;

            article.FinalScore = (relevanceScore * 5) + (normalizedNative * 0.3) + (source.AuthorityWeight * 10);
            article.IsRelevant = true;

            await _articleService.UpdateAsync(article, cancellationToken);
        }
    }

    public async Task<IEnumerable<Article>> CollectFromSourceAsync(
        Source source,
        string keyword,
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        var collector = _collectors.FirstOrDefault(c => c.CanHandle(source));

        if (collector == null)
        {
            _logger.LogWarning("No collector found for source: {Source}", source.Name);
            return Enumerable.Empty<Article>();
        }

        _logger.LogInformation("Using {Collector} for source {Source}",
            collector.GetType().Name, source.Name);

        return await collector.CollectAsync(source, keyword, since, cancellationToken);
    }
}

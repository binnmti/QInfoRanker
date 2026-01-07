using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QInfoRanker.Core.Entities;
using QInfoRanker.Core.Events;
using QInfoRanker.Core.Exceptions;
using QInfoRanker.Core.Interfaces.Collectors;
using QInfoRanker.Core.Interfaces.Services;
using QInfoRanker.Infrastructure.Scoring;

namespace QInfoRanker.Infrastructure.Services;

public class CollectionService : ICollectionService
{
    private readonly IKeywordService _keywordService;
    private readonly ISourceService _sourceService;
    private readonly IArticleService _articleService;
    private readonly IScoringService _scoringService;
    private readonly IWeeklySummaryService _weeklySummaryService;
    private readonly IEnumerable<ICollector> _collectors;
    private readonly ICollectionProgressNotifier _progressNotifier;
    private readonly EnsembleScoringOptions _ensembleOptions;
    private readonly ILogger<CollectionService> _logger;

    public CollectionService(
        IKeywordService keywordService,
        ISourceService sourceService,
        IArticleService articleService,
        IScoringService scoringService,
        IWeeklySummaryService weeklySummaryService,
        IEnumerable<ICollector> collectors,
        ICollectionProgressNotifier progressNotifier,
        IOptions<EnsembleScoringOptions> ensembleOptions,
        ILogger<CollectionService> logger)
    {
        _keywordService = keywordService;
        _sourceService = sourceService;
        _articleService = articleService;
        _scoringService = scoringService;
        _weeklySummaryService = weeklySummaryService;
        _collectors = collectors;
        _progressNotifier = progressNotifier;
        _ensembleOptions = ensembleOptions.Value;
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
                        var articleList = articles.ToList();
                        sourceArticles.AddRange(articleList);

                        // 取得した記事を即座に通知（画面に動きを出す）
                        if (articleList.Any())
                        {
                            var fetchedInfos = articleList.Select(a => new FetchedArticleInfo(
                                a.Title,
                                a.Url,
                                a.PublishedAt,
                                a.NativeScore
                            )).ToList();

                            await _progressNotifier.NotifyArticlesFetchedAsync(new ArticlesFetchedEvent(
                                keywordId, source.Name, fetchedInfos));
                        }

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
                        await ScoreArticlesForSourceAsync(
                            savedArticles, source, searchTerms,
                            keywordId, keyword.Term, sourceIndex, activeSources.Count,
                            allSavedArticles.Count, cancellationToken);
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

        // 週次まとめを生成（既存の場合はスキップ）
        try
        {
            var summary = await _weeklySummaryService.GenerateSummaryIfNeededAsync(keywordId, cancellationToken);
            if (summary != null)
            {
                _logger.LogInformation("Generated weekly summary for '{Keyword}': {Title}", keyword.Term, summary.Title);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate weekly summary for '{Keyword}'", keyword.Term);
        }
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
        int keywordId,
        string keywordTerm,
        int sourceIndex,
        int totalSources,
        int totalArticlesSoFar,
        CancellationToken cancellationToken)
    {
        if (!articles.Any()) return;

        _logger.LogInformation("Scoring {Count} articles from {Source}", articles.Count, source.Name);

        // 進捗コールバック - スコアリング中のバッチ進捗を通知
        var scoredSoFar = 0;
        var progress = new Progress<ScoringProgress>(async p =>
        {
            string message;
            if (p.Stage == ScoringStage.RelevanceEvaluation)
            {
                // フィルタリング段階: 関連ありと判定された記事数を表示
                message = $"{source.Name}: フィルタ {p.CurrentBatch}/{p.TotalBatches} (通過: {p.RelevantCount}件)";
            }
            else
            {
                // 品質評価段階: スコアリング進捗を表示
                scoredSoFar = p.ProcessedArticles;
                message = $"{source.Name}: スコアリング {p.CurrentBatch}/{p.TotalBatches} ({p.ProcessedArticles}/{p.TotalArticles}件)";
            }

            await _progressNotifier.NotifyProgressAsync(new CollectionProgressEvent(
                keywordId, keywordTerm,
                p.Stage == ScoringStage.RelevanceEvaluation ? CollectionPhase.CollectingSource : CollectionPhase.ScoringSource,
                source.Name, sourceIndex, totalSources,
                totalArticlesSoFar + articles.Count,
                totalArticlesSoFar + scoredSoFar,  // 品質評価完了分のみカウント
                message
            ));
        });

        // フィルタリング完了コールバック（Stage 1完了時に即座に採点待ちリストに追加）
        Action<BatchRelevanceResult, IEnumerable<Article>> onRelevanceComplete = async (relevanceResult, relevantArticles) =>
        {
            var passedFilterInfos = relevanceResult.Evaluations
                .Where(e => e.IsRelevant)
                .Select(e =>
                {
                    var article = relevantArticles.FirstOrDefault(a => a.Id == e.ArticleId);
                    if (article == null) return null;
                    return new PassedFilterArticleInfo(
                        article.Id,
                        article.Title,
                        article.Url,
                        article.PublishedAt,
                        article.NativeScore,
                        e.RelevanceScore
                    );
                })
                .Where(p => p != null)
                .Cast<PassedFilterArticleInfo>()
                .ToList();

            if (passedFilterInfos.Any())
            {
                await _progressNotifier.NotifyArticlesPassedFilterAsync(new ArticlesPassedFilterEvent(
                    keywordId, source.Name, passedFilterInfos));
            }
        };

        // 品質評価完了コールバック（各バッチ完了時に即座にDB保存＆採点待ちリストから削除）
        Action<IEnumerable<Article>, IEnumerable<ArticleQuality>> onQualityBatchComplete = async (scoredArticles, qualityResults) =>
        {
            var scoredPreviews = new List<ScoredArticlePreview>();

            foreach (var article in scoredArticles)
            {
                // 最終スコアを即座に計算
                if (article.LlmScore.HasValue)
                {
                    article.FinalScore = _scoringService.CalculateFinalScore(article, source);
                }
                else
                {
                    // フォールバック計算（シンプル100点満点: 品質80点 + 関連20点）
                    var relevanceScore = (article.RelevanceScore ?? 5) * 2;
                    var qualityScore = 40; // LLMスコアなしの場合は中央値
                    article.FinalScore = qualityScore + relevanceScore;
                }

                // 即座にDB保存（メイン一覧にすぐ反映されるように）
                await _articleService.UpdateAsync(article, cancellationToken);

                scoredPreviews.Add(new ScoredArticlePreview
                {
                    ArticleId = article.Id,
                    Title = article.Title,
                    Url = article.Url,
                    SourceName = source.Name,
                    PublishedAt = article.PublishedAt,
                    NativeScore = article.NativeScore,
                    RelevanceScore = article.RelevanceScore ?? 0,
                    LlmScore = article.LlmScore ?? 0,
                    FinalScore = article.FinalScore,
                    SummaryJa = article.SummaryJa,
                    ScoredAt = DateTime.UtcNow
                });
            }

            await _progressNotifier.NotifyArticlesQualityScoredAsync(keywordId, scoredPreviews);
        };

        // スコアリング実行（アンサンブル有効/無効で分岐）
        int totalApiCalls;
        int relevantCount;
        int totalInputTokens;
        int totalOutputTokens;

        if (_ensembleOptions.EnableEnsemble)
        {
            // 3段階アンサンブル評価
            _logger.LogInformation("Using ensemble evaluation with {JudgeCount} judges",
                _ensembleOptions.Judges.Count(j => j.IsEnabled));

            var threeStageResult = await _scoringService.EvaluateThreeStageAsync(
                articles, searchTerms, source.HasServerSideFiltering, progress, cancellationToken);

            totalApiCalls = threeStageResult.TotalApiCalls;
            relevantCount = threeStageResult.RelevanceResult.RelevantCount;
            totalInputTokens = threeStageResult.TotalInputTokens;
            totalOutputTokens = threeStageResult.TotalOutputTokens;

            // Stage 1完了通知（関連性フィルタを通過した記事）
            var passedFilterInfos = threeStageResult.RelevanceResult.Evaluations
                .Where(e => e.IsRelevant)
                .Select(e =>
                {
                    var article = articles.FirstOrDefault(a => a.Id == e.ArticleId);
                    if (article == null) return null;
                    return new PassedFilterArticleInfo(
                        article.Id,
                        article.Title,
                        article.Url,
                        article.PublishedAt,
                        article.NativeScore,
                        e.RelevanceScore
                    );
                })
                .Where(p => p != null)
                .Cast<PassedFilterArticleInfo>()
                .ToList();

            if (passedFilterInfos.Any())
            {
                await _progressNotifier.NotifyArticlesPassedFilterAsync(new ArticlesPassedFilterEvent(
                    keywordId, source.Name, passedFilterInfos));
            }

            // アンサンブル結果をArticleに反映（各記事完了時に通知）
            var scoredPreviews = new List<ScoredArticlePreview>();
            foreach (var ensembleResult in threeStageResult.EnsembleResults)
            {
                var article = articles.FirstOrDefault(a => a.Id == ensembleResult.ArticleId);
                if (article != null)
                {
                    article.TechnicalScore = ensembleResult.FinalTechnical;
                    article.NoveltyScore = ensembleResult.FinalNovelty;
                    article.ImpactScore = ensembleResult.FinalImpact;
                    article.QualityScore = ensembleResult.FinalQuality;
                    article.LlmScore = ensembleResult.FinalTotal;
                    article.SummaryJa = ensembleResult.FinalSummaryJa;
                    article.FinalScore = _scoringService.CalculateFinalScore(article, source);
                    await _articleService.UpdateAsync(article, cancellationToken);

                    scoredPreviews.Add(new ScoredArticlePreview
                    {
                        ArticleId = article.Id,
                        Title = article.Title,
                        Url = article.Url,
                        SourceName = source.Name,
                        PublishedAt = article.PublishedAt,
                        NativeScore = article.NativeScore,
                        RelevanceScore = article.RelevanceScore ?? 0,
                        LlmScore = article.LlmScore ?? 0,
                        FinalScore = article.FinalScore,
                        SummaryJa = article.SummaryJa,
                        ScoredAt = DateTime.UtcNow
                    });
                }
            }

            // Stage 2+3完了通知
            if (scoredPreviews.Any())
            {
                await _progressNotifier.NotifyArticlesQualityScoredAsync(keywordId, scoredPreviews);
            }

            _logger.LogInformation(
                "Ensemble scoring completed for {Source}: {Relevant}/{Total} relevant, API calls: {ApiCalls}, MetaJudge skipped: {Skipped}",
                source.Name, relevantCount, articles.Count, totalApiCalls, threeStageResult.MetaJudgeSkippedCount);
        }
        else
        {
            // 従来の2段階評価（コールバック付き）
            var result = await _scoringService.EvaluateTwoStageAsync(
                articles, searchTerms, source.HasServerSideFiltering, progress,
                onRelevanceComplete, onQualityBatchComplete, cancellationToken);

            totalApiCalls = result.TotalApiCalls;
            relevantCount = result.RelevanceResult.RelevantCount;
            totalInputTokens = result.TotalInputTokens;
            totalOutputTokens = result.TotalOutputTokens;

            _logger.LogInformation(
                "Scoring completed for {Source}: {Relevant}/{Total} relevant, API calls: {ApiCalls}",
                source.Name, relevantCount, articles.Count, totalApiCalls);
        }

        // トークン使用量を通知
        await _progressNotifier.NotifyTokenUsageAsync(keywordId, totalInputTokens, totalOutputTokens);

        // FinalScoreが未計算の記事を処理
        // - LlmScoreがない記事（フィルタで除外された記事）: フォールバック計算
        // - LlmScoreがあるがFinalScoreが0の記事（LLMレスポンス漏れでコールバック未処理）: 正式計算
        foreach (var article in articles.Where(a => a.LlmScore == null || a.FinalScore == 0))
        {
            if (article.LlmScore.HasValue)
            {
                // LlmScoreがある場合は正式な計算（コールバックで漏れた記事）
                article.FinalScore = _scoringService.CalculateFinalScore(article, source);
            }
            else
            {
                // LlmScoreがない場合はフォールバック計算（シンプル100点満点: 品質80点 + 関連20点）
                var relevanceScore = (article.RelevanceScore ?? 5) * 2;
                var qualityScore = 40; // LLMスコアなしの場合は中央値
                article.FinalScore = qualityScore + relevanceScore;
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

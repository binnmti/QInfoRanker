using Microsoft.Extensions.Logging;
using QInfoRanker.Core.Entities;
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
    private readonly ILogger<CollectionService> _logger;

    public CollectionService(
        IKeywordService keywordService,
        ISourceService sourceService,
        IArticleService articleService,
        IScoringService scoringService,
        IEnumerable<ICollector> collectors,
        ILogger<CollectionService> logger)
    {
        _keywordService = keywordService;
        _sourceService = sourceService;
        _articleService = articleService;
        _scoringService = scoringService;
        _collectors = collectors;
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

    public async Task CollectForKeywordAsync(int keywordId, CancellationToken cancellationToken = default)
    {
        var keyword = await _keywordService.GetByIdAsync(keywordId, cancellationToken);
        if (keyword == null)
        {
            _logger.LogWarning("Keyword {KeywordId} not found", keywordId);
            return;
        }

        _logger.LogInformation("Starting collection for keyword: {Keyword}", keyword.Term);

        var sources = await _sourceService.GetByKeywordIdAsync(keywordId, cancellationToken);
        var activeSources = sources.Where(s => s.IsActive).ToList();
        var sourceDict = activeSources.ToDictionary(s => s.Id);

        // Calculate since date (1 month ago for initial, or last collection date)
        var since = DateTime.UtcNow.AddMonths(-1);

        var allArticles = new List<Article>();
        var searchTerms = keyword.GetAllSearchTerms().ToList();

        _logger.LogInformation("Searching with terms: {Terms}", string.Join(", ", searchTerms));

        foreach (var source in activeSources)
        {
            foreach (var searchTerm in searchTerms)
            {
                try
                {
                    var articles = await CollectFromSourceAsync(source, searchTerm, since, cancellationToken);
                    allArticles.AddRange(articles);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error collecting from source {Source} for term {Term}",
                        source.Name, searchTerm);
                }
            }
        }

        // Save all collected articles (duplicates will be filtered)
        if (allArticles.Any())
        {
            var savedArticles = (await _articleService.CreateBatchAsync(allArticles, cancellationToken)).ToList();
            _logger.LogInformation("Saved {Count} new articles for keyword: {Keyword}",
                savedArticles.Count, keyword.Term);

            if (savedArticles.Count == 0)
            {
                _logger.LogInformation("No new articles to score for keyword: {Keyword}", keyword.Term);
                return;
            }

            // 保存後のArticleにSourceをセット（バッチスコアリングで使用）
            foreach (var article in savedArticles)
            {
                if (sourceDict.TryGetValue(article.SourceId, out var source))
                {
                    article.Source = source;
                }
            }

            // 2段階バッチスコアリング
            try
            {
                // ソースがサーバー側でキーワードフィルタリングしている場合はStage 1をスキップ
                // 全てのソースがサーバー側フィルタリングを持つ場合のみスキップ
                _logger.LogInformation("=== スコアリング開始 ===");
                foreach (var src in activeSources)
                {
                    _logger.LogInformation("  Source: {Name}, HasServerSideFiltering={HasFilter}",
                        src.Name, src.HasServerSideFiltering);
                }

                var allSourcesHaveServerFiltering = activeSources.All(s => s.HasServerSideFiltering);
                _logger.LogInformation("allSourcesHaveServerFiltering = {Value}", allSourcesHaveServerFiltering);

                if (allSourcesHaveServerFiltering)
                {
                    _logger.LogInformation("全ソースがサーバー側フィルタリングを持つため、Stage 1をスキップします。");
                }

                var twoStageResult = await _scoringService.EvaluateTwoStageAsync(
                    savedArticles,
                    searchTerms,
                    skipRelevanceFilter: allSourcesHaveServerFiltering,
                    cancellationToken);

                _logger.LogInformation(
                    "バッチスコアリング完了: {Relevant}/{Total}件が関連あり, API呼び出し{ApiCalls}回",
                    twoStageResult.RelevanceResult.RelevantCount,
                    savedArticles.Count,
                    twoStageResult.TotalApiCalls);

                // スコアリング後の状態をログ出力
                var articlesWithLlmScore = savedArticles.Count(a => a.LlmScore != null);
                var articlesWithRelevance = savedArticles.Count(a => a.RelevanceScore != null);
                _logger.LogInformation("スコアリング後状態: LlmScoreあり={WithLlm}/{Total}, RelevanceScoreあり={WithRel}/{Total}",
                    articlesWithLlmScore, savedArticles.Count, articlesWithRelevance, savedArticles.Count);

                // 最終スコア計算と更新
                // すべての記事にスコアを付ける（0点にはしない）
                foreach (var article in savedArticles)
                {
                    if (sourceDict.TryGetValue(article.SourceId, out var source))
                    {
                        // LLMスコアがある場合は通常計算
                        if (article.LlmScore != null)
                        {
                            article.FinalScore = _scoringService.CalculateFinalScore(article, source);
                        }
                        // LLMスコアがない場合でも、関連性スコア＋ネイティブスコアでFinalScoreを計算
                        else
                        {
                            var relevanceScore = article.RelevanceScore ?? 5; // デフォルト5
                            var normalizedNative = source.HasNativeScore && article.NativeScore.HasValue
                                ? _scoringService.NormalizeNativeScore(article.NativeScore, source.Name)
                                : 0;

                            // FinalScore = 関連性スコア(0-10)*5 + 正規化ネイティブスコア*0.3 + 権威ボーナス
                            article.FinalScore = (relevanceScore * 5) +
                                                (normalizedNative * 0.3) +
                                                (source.AuthorityWeight * 10);

                            _logger.LogInformation(
                                "記事 '{Title}' のLLMスコアなし。関連性+ネイティブで計算: FinalScore={Score:F1} (Relevance={Relevance}, Native={Native})",
                                article.Title?.Substring(0, Math.Min(20, article.Title?.Length ?? 0)),
                                article.FinalScore, relevanceScore, article.NativeScore ?? 0);
                        }

                        await _articleService.UpdateAsync(article, cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning("記事 '{Title}' のSourceId={SourceId} がsourceDictに見つかりません！",
                            article.Title, article.SourceId);
                    }
                }

                var avgScore = savedArticles.Average(a => a.FinalScore);
                _logger.LogInformation("スコア計算完了: {Count}件, 平均スコア={Avg:F1}",
                    savedArticles.Count, avgScore);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "バッチスコアリングに失敗。フォールバック処理を実行。");
                await FallbackToIndividualScoringAsync(savedArticles, sourceDict, cancellationToken);
            }
        }
    }

    private async Task FallbackToIndividualScoringAsync(
        List<Article> articles,
        Dictionary<int, Source> sourceDict,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("個別スコアリングにフォールバック中... ({Count}件)", articles.Count);

        foreach (var article in articles)
        {
            try
            {
                if (sourceDict.TryGetValue(article.SourceId, out var source))
                {
                    var includeContent = !source.HasNativeScore;
                    await _scoringService.CalculateLlmScoreAsync(article, includeContent, cancellationToken);
                    article.FinalScore = _scoringService.CalculateFinalScore(article, source);
                    article.IsRelevant = true; // フォールバック時は全て関連ありとみなす
                    await _articleService.UpdateAsync(article, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "記事スコアリングエラー: {Title}", article.Title);
            }
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

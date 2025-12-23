using Microsoft.EntityFrameworkCore;
using QInfoRanker.Core.Entities;
using QInfoRanker.Core.Interfaces;
using QInfoRanker.Infrastructure.Collectors;
using QInfoRanker.Infrastructure.Data;

namespace QInfoRanker.Infrastructure.Services;

/// <summary>
/// Service for collecting articles from sources
/// </summary>
public class ArticleCollectionService
{
    private readonly QInfoRankerDbContext _dbContext;
    private readonly CollectorFactory _collectorFactory;
    private readonly IScoringService _scoringService;

    public ArticleCollectionService(
        QInfoRankerDbContext dbContext,
        CollectorFactory collectorFactory,
        IScoringService scoringService)
    {
        _dbContext = dbContext;
        _collectorFactory = collectorFactory;
        _scoringService = scoringService;
    }

    /// <summary>
    /// Collect articles for all active keywords from all active sources
    /// </summary>
    public async Task<int> CollectArticlesAsync()
    {
        var keywords = await _dbContext.Keywords
            .Include(k => k.Sources)
            .Where(k => k.IsActive)
            .ToListAsync();

        int totalCollected = 0;

        foreach (var keyword in keywords)
        {
            var collected = await CollectForKeywordAsync(keyword);
            totalCollected += collected;
        }

        return totalCollected;
    }

    /// <summary>
    /// Collect articles for a specific keyword
    /// </summary>
    public async Task<int> CollectForKeywordAsync(Keyword keyword)
    {
        // Get all active sources for this keyword
        var sources = await _dbContext.Sources
            .Where(s => s.IsActive && (s.KeywordId == keyword.Id || s.KeywordId == null))
            .ToListAsync();

        int totalCollected = 0;

        foreach (var source in sources)
        {
            var collector = _collectorFactory.GetCollector(source.Type);
            if (collector == null)
            {
                Console.WriteLine($"No collector found for source type: {source.Type}");
                continue;
            }

            try
            {
                // Get the last collection time for incremental updates
                var lastCollected = await _dbContext.Articles
                    .Where(a => a.SourceId == source.Id)
                    .OrderByDescending(a => a.CollectedAt)
                    .Select(a => a.CollectedAt)
                    .FirstOrDefaultAsync();

                var since = lastCollected != default ? lastCollected : DateTime.UtcNow.AddMonths(-1);

                // Collect articles
                var articles = await collector.CollectAsync(source, keyword.Term, since);

                foreach (var article in articles)
                {
                    // Check for duplicates by URL
                    var exists = await _dbContext.Articles
                        .AnyAsync(a => a.Url == article.Url);

                    if (exists)
                        continue;

                    // Score the article with LLM
                    article.LlmScore = await _scoringService.ScoreArticleAsync(article, !source.HasNativeScore);

                    // Calculate final score
                    article.Source = source; // Ensure source is loaded for scoring
                    article.FinalScore = _scoringService.CalculateFinalScore(article);

                    _dbContext.Articles.Add(article);
                    totalCollected++;
                }

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error collecting from {source.Name}: {ex.Message}");
            }
        }

        return totalCollected;
    }

    /// <summary>
    /// Recalculate scores for all articles
    /// </summary>
    public async Task RecalculateScoresAsync()
    {
        var articles = await _dbContext.Articles
            .Include(a => a.Source)
            .ToListAsync();

        foreach (var article in articles)
        {
            // Recalculate LLM score if needed
            if (!article.LlmScore.HasValue)
            {
                article.LlmScore = await _scoringService.ScoreArticleAsync(article, !article.Source.HasNativeScore);
            }

            // Recalculate final score
            article.FinalScore = _scoringService.CalculateFinalScore(article);
        }

        await _dbContext.SaveChangesAsync();
    }
}

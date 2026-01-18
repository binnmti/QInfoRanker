using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QInfoRanker.Core.Entities;
using QInfoRanker.Core.Enums;
using QInfoRanker.Core.Interfaces.Services;
using QInfoRanker.Infrastructure.Data;

namespace QInfoRanker.Infrastructure.Services;

public class ArticleService : IArticleService
{
    private readonly AppDbContext _context;

    public ArticleService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Article>> GetAllAsync(int? keywordId = null, int? sourceId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Articles
            .AsNoTracking() // 常にDBから最新データを取得
            .Include(a => a.Source)
            .Include(a => a.Keyword)
            .AsQueryable();

        if (keywordId.HasValue)
        {
            query = query.Where(a => a.KeywordId == keywordId.Value);
        }

        if (sourceId.HasValue)
        {
            query = query.Where(a => a.SourceId == sourceId.Value);
        }

        return await query
            .OrderByDescending(a => a.CollectedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Article>> GetRankedAsync(int? keywordId = null, DateTime? from = null, DateTime? to = null, int take = 50, CancellationToken cancellationToken = default)
    {
        var query = _context.Articles
            .AsNoTracking() // 常にDBから最新データを取得
            .Include(a => a.Source)
            .Include(a => a.Keyword)
            .AsQueryable();

        if (keywordId.HasValue)
        {
            query = query.Where(a => a.KeywordId == keywordId.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(a => a.CollectedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(a => a.CollectedAt <= to.Value);
        }

        return await query
            .OrderByDescending(a => a.FinalScore)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Article>> GetWeeklyByCategoryAsync(
        SourceCategory category,
        int? keywordId = null,
        int take = 10,
        CancellationToken cancellationToken = default)
    {
        var (weekStart, weekEnd) = GetCurrentWeekRange();

        var query = _context.Articles
            .AsNoTracking()
            .Include(a => a.Source)
            .Include(a => a.Keyword)
            .Where(a => a.Source.Category == category)
            .Where(a => a.IsRelevant == true)
            .Where(a => a.LlmScore.HasValue);

        if (keywordId.HasValue)
        {
            query = query.Where(a => a.KeywordId == keywordId.Value);
        }

        // Newsカテゴリは公開日基準、その他は収集日基準
        if (category == SourceCategory.News)
        {
            query = query.Where(a => a.PublishedAt.HasValue &&
                                     a.PublishedAt.Value >= weekStart &&
                                     a.PublishedAt.Value <= weekEnd);
        }
        else
        {
            query = query.Where(a => a.CollectedAt >= weekStart && a.CollectedAt <= weekEnd);
        }

        return await query
            .OrderByDescending(a => a.FinalScore)
            .ThenByDescending(a => a.CollectedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Article>> GetWeeklyRecommendedByCategoryAsync(
        SourceCategory category,
        int recommendThreshold,
        int? keywordId = null,
        int skip = 0,
        int take = 10,
        CancellationToken cancellationToken = default)
    {
        var query = BuildWeeklyRecommendedQuery(category, recommendThreshold, keywordId);

        return await query
            .OrderByDescending(a => a.RecommendScore)
            .ThenByDescending(a => a.FinalScore)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetWeeklyRecommendedCountByCategoryAsync(
        SourceCategory category,
        int recommendThreshold,
        int? keywordId = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildWeeklyRecommendedQuery(category, recommendThreshold, keywordId);
        return await query.CountAsync(cancellationToken);
    }

    private IQueryable<Article> BuildWeeklyRecommendedQuery(
        SourceCategory category,
        int recommendThreshold,
        int? keywordId)
    {
        var (weekStart, weekEnd) = GetCurrentWeekRange();

        var query = _context.Articles
            .AsNoTracking()
            .Include(a => a.Source)
            .Include(a => a.Keyword)
            .Where(a => a.Source.Category == category)
            .Where(a => a.IsRelevant == true)
            .Where(a => a.LlmScore.HasValue)
            .Where(a => a.RecommendScore.HasValue && a.RecommendScore.Value >= recommendThreshold);

        if (keywordId.HasValue)
        {
            query = query.Where(a => a.KeywordId == keywordId.Value);
        }

        // Newsカテゴリは公開日基準、その他は収集日基準
        if (category == SourceCategory.News)
        {
            query = query.Where(a => a.PublishedAt.HasValue &&
                                     a.PublishedAt.Value >= weekStart &&
                                     a.PublishedAt.Value <= weekEnd);
        }
        else
        {
            query = query.Where(a => a.CollectedAt >= weekStart && a.CollectedAt <= weekEnd);
        }

        return query;
    }

    private static (DateTime WeekStart, DateTime WeekEnd) GetCurrentWeekRange()
    {
        var today = DateTime.UtcNow.Date;
        var diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        var weekStart = today.AddDays(-diff);
        var weekEnd = weekStart.AddDays(6).AddHours(23).AddMinutes(59).AddSeconds(59);
        return (weekStart, weekEnd);
    }

    public async Task<Article?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Articles
            .Include(a => a.Source)
            .Include(a => a.Keyword)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<Article?> GetByUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        return await _context.Articles
            .Include(a => a.Source)
            .Include(a => a.Keyword)
            .FirstOrDefaultAsync(a => a.Url == url, cancellationToken);
    }

    public async Task<Article> CreateAsync(Article article, CancellationToken cancellationToken = default)
    {
        article.CollectedAt = DateTime.UtcNow;
        _context.Articles.Add(article);
        await _context.SaveChangesAsync(cancellationToken);
        return article;
    }

    public async Task<Article> UpdateAsync(Article article, CancellationToken cancellationToken = default)
    {
        _context.Articles.Update(article);
        await _context.SaveChangesAsync(cancellationToken);
        return article;
    }

    public async Task<IEnumerable<Article>> CreateBatchAsync(IEnumerable<Article> articles, CancellationToken cancellationToken = default)
    {
        var articleList = articles.ToList();

        // 既存のURLとタイトルを取得（同じキーワード内で重複チェック）
        var keywordIds = articleList.Select(a => a.KeywordId).Distinct().ToList();
        var existingArticles = await _context.Articles
            .Where(a => keywordIds.Contains(a.KeywordId))
            .Select(a => new { a.Url, a.Title })
            .ToListAsync(cancellationToken);

        var existingUrls = existingArticles.Select(a => a.Url).ToHashSet();
        var existingTitles = existingArticles.Select(a => a.Title.ToLowerInvariant()).ToHashSet();

        // URL重複と同じタイトルを除外
        var newArticles = articleList
            .Where(a => !existingUrls.Contains(a.Url) &&
                       !existingTitles.Contains(a.Title.ToLowerInvariant()))
            .ToList();

        // 新規記事内での重複も除外（最初に出現したものを優先）
        var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        newArticles = newArticles
            .Where(a => seenTitles.Add(a.Title))
            .ToList();

        foreach (var article in newArticles)
        {
            article.CollectedAt = DateTime.UtcNow;
        }

        _context.Articles.AddRange(newArticles);
        await _context.SaveChangesAsync(cancellationToken);
        return newArticles;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var article = await _context.Articles.FindAsync([id], cancellationToken);
        if (article != null)
        {
            _context.Articles.Remove(article);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> DeleteByKeywordAsync(int keywordId, CancellationToken cancellationToken = default)
    {
        var articles = await _context.Articles
            .Where(a => a.KeywordId == keywordId)
            .ToListAsync(cancellationToken);

        if (articles.Count == 0)
            return 0;

        _context.Articles.RemoveRange(articles);
        await _context.SaveChangesAsync(cancellationToken);
        return articles.Count;
    }
}

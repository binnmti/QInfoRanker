using Microsoft.EntityFrameworkCore;
using QInfoRanker.Core.Entities;
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
}

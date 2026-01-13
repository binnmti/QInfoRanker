using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QInfoRanker.Core.Entities;
using QInfoRanker.Core.Interfaces.Services;
using QInfoRanker.Infrastructure.Data;

namespace QInfoRanker.Infrastructure.Services;

public class KeywordService : IKeywordService
{
    private readonly AppDbContext _context;
    private readonly ISourceRecommendationService _recommendationService;
    private readonly ILogger<KeywordService> _logger;

    public KeywordService(
        AppDbContext context,
        ISourceRecommendationService recommendationService,
        ILogger<KeywordService> logger)
    {
        _context = context;
        _recommendationService = recommendationService;
        _logger = logger;
    }

    public async Task<IEnumerable<Keyword>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Keywords
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Keyword>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Keywords
            .Where(k => k.IsActive)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Keyword?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Keywords
            .FirstOrDefaultAsync(k => k.Id == id, cancellationToken);
    }

    public async Task<Keyword> CreateAsync(string term, CancellationToken cancellationToken = default)
    {
        var keyword = new Keyword
        {
            Term = term,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Keywords.Add(keyword);
        await _context.SaveChangesAsync(cancellationToken);

        // AIで英語エイリアスを生成
        _logger.LogInformation("Getting AI analysis for keyword '{Term}'", term);
        var recommendationResult = await _recommendationService.RecommendSourcesAsync(term, cancellationToken);

        if (recommendationResult.KeywordAnalysis != null)
        {
            _logger.LogInformation(
                "Keyword '{Term}' analysis: Language={Language}, Category={Category}, Aliases={Aliases}, Reasoning={Reasoning}",
                term,
                recommendationResult.DetectedLanguage,
                recommendationResult.DetectedCategory,
                recommendationResult.EnglishAliases,
                recommendationResult.KeywordAnalysis);
        }

        // 英語エイリアスを設定（AIが生成した場合）
        if (!string.IsNullOrWhiteSpace(recommendationResult.EnglishAliases))
        {
            keyword.Aliases = recommendationResult.EnglishAliases;
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Auto-generated English aliases for '{Term}': {Aliases}", term, keyword.Aliases);
        }

        _logger.LogInformation("Created keyword '{Term}'", term);

        return keyword;
    }

    public async Task<Keyword> UpdateAsync(Keyword keyword, CancellationToken cancellationToken = default)
    {
        _context.Keywords.Update(keyword);
        await _context.SaveChangesAsync(cancellationToken);
        return keyword;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var keyword = await _context.Keywords.FindAsync([id], cancellationToken);
        if (keyword != null)
        {
            _context.Keywords.Remove(keyword);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ToggleActiveAsync(int id, CancellationToken cancellationToken = default)
    {
        var keyword = await _context.Keywords.FindAsync([id], cancellationToken);
        if (keyword != null)
        {
            keyword.IsActive = !keyword.IsActive;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UpdateAliasesAsync(int id, string? aliases, CancellationToken cancellationToken = default)
    {
        var keyword = await _context.Keywords.FindAsync([id], cancellationToken);
        if (keyword != null)
        {
            keyword.Aliases = string.IsNullOrWhiteSpace(aliases) ? null : aliases.Trim();
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

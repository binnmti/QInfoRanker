using Microsoft.EntityFrameworkCore;
using QInfoRanker.Core.Entities;
using QInfoRanker.Core.Interfaces.Services;
using QInfoRanker.Infrastructure.Data;

namespace QInfoRanker.Infrastructure.Services;

public class SourceService : ISourceService
{
    private readonly AppDbContext _context;

    public SourceService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Source>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Sources
            .Include(s => s.Keyword)
            .Where(s => !s.IsTemplate)
            .OrderBy(s => s.KeywordId)
            .ThenBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Source>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Sources
            .Include(s => s.Keyword)
            .Where(s => s.IsActive && !s.IsTemplate)
            .OrderBy(s => s.KeywordId)
            .ThenBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Source>> GetByKeywordIdAsync(int keywordId, CancellationToken cancellationToken = default)
    {
        return await _context.Sources
            .Where(s => s.KeywordId == keywordId && !s.IsTemplate)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Source>> GetTemplateSourcesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Sources
            .Where(s => s.IsTemplate)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Source?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Sources
            .Include(s => s.Keyword)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Source> CreateAsync(Source source, CancellationToken cancellationToken = default)
    {
        // 重複チェック: 同じKeywordIdと名前の組み合わせが既に存在するかチェック
        var existingSource = await _context.Sources.FirstOrDefaultAsync(
            s => s.KeywordId == source.KeywordId &&
                 s.Name == source.Name &&
                 !s.IsTemplate,
            cancellationToken);

        if (existingSource != null)
        {
            // 既存のソースを返す（重複作成を防ぐ）
            return existingSource;
        }

        source.CreatedAt = DateTime.UtcNow;
        _context.Sources.Add(source);
        await _context.SaveChangesAsync(cancellationToken);
        return source;
    }

    public async Task<Source> UpdateAsync(Source source, CancellationToken cancellationToken = default)
    {
        _context.Sources.Update(source);
        await _context.SaveChangesAsync(cancellationToken);
        return source;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var source = await _context.Sources.FindAsync([id], cancellationToken);
        if (source != null)
        {
            _context.Sources.Remove(source);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ToggleActiveAsync(int id, CancellationToken cancellationToken = default)
    {
        var source = await _context.Sources.FindAsync([id], cancellationToken);
        if (source != null)
        {
            source.IsActive = !source.IsActive;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<Source>> CreateFromTemplatesAsync(int keywordId, CancellationToken cancellationToken = default)
    {
        var templates = await GetTemplateSourcesAsync(cancellationToken);

        // 既存のソース名を取得して重複チェック
        var existingNames = await _context.Sources
            .Where(s => s.KeywordId == keywordId && !s.IsTemplate)
            .Select(s => s.Name)
            .ToListAsync(cancellationToken);
        var existingNameSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        var newSources = new List<Source>();

        foreach (var template in templates)
        {
            // 重複スキップ
            if (existingNameSet.Contains(template.Name))
            {
                continue;
            }

            var source = new Source
            {
                KeywordId = keywordId,
                Name = template.Name,
                Url = template.Url,
                SearchUrlTemplate = template.SearchUrlTemplate,
                Type = template.Type,
                HasNativeScore = template.HasNativeScore,
                AuthorityWeight = template.AuthorityWeight,
                IsTemplate = false,
                IsActive = true,
                Language = template.Language,
                Category = template.Category,
                CreatedAt = DateTime.UtcNow
            };
            _context.Sources.Add(source);
            newSources.Add(source);
            existingNameSet.Add(template.Name);
        }

        if (newSources.Any())
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return newSources;
    }
}

using Microsoft.EntityFrameworkCore;
using QInfoRanker.Core.Entities;
using QInfoRanker.Core.Interfaces.Services;
using QInfoRanker.Infrastructure.Data;

namespace QInfoRanker.Infrastructure.Services;

/// <summary>
/// グローバルソース（全キーワード共通）の管理サービス
/// </summary>
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
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Source>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Sources
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Source?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Sources
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Source> CreateAsync(Source source, CancellationToken cancellationToken = default)
    {
        // 重複チェック: 同じ名前のソースが既に存在するかチェック
        var existingSource = await _context.Sources.FirstOrDefaultAsync(
            s => s.Name == source.Name,
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
}

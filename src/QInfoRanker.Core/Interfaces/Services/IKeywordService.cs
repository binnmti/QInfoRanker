using QInfoRanker.Core.Entities;

namespace QInfoRanker.Core.Interfaces.Services;

public interface IKeywordService
{
    Task<IEnumerable<Keyword>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Keyword>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<Keyword?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>
    /// Gets a keyword by slug or ID
    /// </summary>
    /// <param name="slugOrId">Slug string or numeric ID</param>
    Task<Keyword?> GetBySlugOrIdAsync(string slugOrId, CancellationToken cancellationToken = default);
    Task<Keyword> CreateAsync(string term, CancellationToken cancellationToken = default);
    Task<Keyword> UpdateAsync(Keyword keyword, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task ToggleActiveAsync(int id, CancellationToken cancellationToken = default);
    Task UpdateAliasesAsync(int id, string? aliases, CancellationToken cancellationToken = default);
    /// <summary>
    /// Generates and sets the slug for a keyword based on its aliases
    /// </summary>
    Task<string?> GenerateAndSetSlugAsync(int id, CancellationToken cancellationToken = default);
}

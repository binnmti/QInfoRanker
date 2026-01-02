using QInfoRanker.Core.Entities;

namespace QInfoRanker.Core.Interfaces.Services;

public interface IKeywordService
{
    Task<IEnumerable<Keyword>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Keyword>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<Keyword?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Keyword> CreateAsync(string term, CancellationToken cancellationToken = default);
    Task<Keyword> UpdateAsync(Keyword keyword, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task ToggleActiveAsync(int id, CancellationToken cancellationToken = default);
    Task UpdateAliasesAsync(int id, string? aliases, CancellationToken cancellationToken = default);
}

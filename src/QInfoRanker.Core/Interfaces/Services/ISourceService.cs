using QInfoRanker.Core.Entities;

namespace QInfoRanker.Core.Interfaces.Services;

/// <summary>
/// グローバルソース（全キーワード共通）の管理サービス
/// </summary>
public interface ISourceService
{
    Task<IEnumerable<Source>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Source>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<Source?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Source> CreateAsync(Source source, CancellationToken cancellationToken = default);
    Task<Source> UpdateAsync(Source source, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task ToggleActiveAsync(int id, CancellationToken cancellationToken = default);
}

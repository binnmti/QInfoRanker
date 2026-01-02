using QInfoRanker.Core.Entities;

namespace QInfoRanker.Core.Interfaces.Services;

public interface IArticleService
{
    Task<IEnumerable<Article>> GetAllAsync(int? keywordId = null, int? sourceId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<Article>> GetRankedAsync(int? keywordId = null, DateTime? from = null, DateTime? to = null, int take = 50, CancellationToken cancellationToken = default);
    Task<Article?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Article?> GetByUrlAsync(string url, CancellationToken cancellationToken = default);
    Task<Article> CreateAsync(Article article, CancellationToken cancellationToken = default);
    Task<Article> UpdateAsync(Article article, CancellationToken cancellationToken = default);
    Task<IEnumerable<Article>> CreateBatchAsync(IEnumerable<Article> articles, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

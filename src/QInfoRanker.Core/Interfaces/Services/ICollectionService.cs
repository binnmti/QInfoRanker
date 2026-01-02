using QInfoRanker.Core.Entities;

namespace QInfoRanker.Core.Interfaces.Services;

public interface ICollectionService
{
    Task CollectAllAsync(CancellationToken cancellationToken = default);
    Task CollectForKeywordAsync(int keywordId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Article>> CollectFromSourceAsync(Source source, string keyword, DateTime? since = null, CancellationToken cancellationToken = default);
}

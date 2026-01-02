using QInfoRanker.Core.Entities;

namespace QInfoRanker.Core.Interfaces.Collectors;

public interface ICollector
{
    string SourceName { get; }
    bool CanHandle(Source source);
    Task<IEnumerable<Article>> CollectAsync(Source source, string keyword, DateTime? since = null, CancellationToken cancellationToken = default);
}

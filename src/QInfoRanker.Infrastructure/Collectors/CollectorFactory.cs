using QInfoRanker.Core.Entities;
using QInfoRanker.Core.Interfaces;

namespace QInfoRanker.Infrastructure.Collectors;

/// <summary>
/// Factory for creating appropriate collector based on source type
/// </summary>
public class CollectorFactory
{
    private readonly IEnumerable<ICollector> _collectors;

    public CollectorFactory(IEnumerable<ICollector> collectors)
    {
        _collectors = collectors;
    }

    /// <summary>
    /// Get collector that can handle the given source type
    /// </summary>
    public ICollector? GetCollector(SourceType sourceType)
    {
        return _collectors.FirstOrDefault(c => c.CanHandle(sourceType));
    }
}

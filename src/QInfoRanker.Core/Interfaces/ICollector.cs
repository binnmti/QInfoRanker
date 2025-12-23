using QInfoRanker.Core.Entities;

namespace QInfoRanker.Core.Interfaces;

/// <summary>
/// Interface for collecting articles from various sources
/// </summary>
public interface ICollector
{
    /// <summary>
    /// Collect articles from a source for a given keyword
    /// </summary>
    /// <param name="source">The source to collect from</param>
    /// <param name="keyword">The keyword to search for</param>
    /// <param name="since">Optional date to collect articles since (for incremental updates)</param>
    /// <returns>Collection of articles</returns>
    Task<IEnumerable<Article>> CollectAsync(Source source, string keyword, DateTime? since = null);
    
    /// <summary>
    /// Checks if this collector can handle the given source type
    /// </summary>
    bool CanHandle(SourceType sourceType);
}

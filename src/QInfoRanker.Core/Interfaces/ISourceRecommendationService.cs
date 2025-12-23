using QInfoRanker.Core.Entities;

namespace QInfoRanker.Core.Interfaces;

/// <summary>
/// Interface for LLM-based source recommendation
/// </summary>
public interface ISourceRecommendationService
{
    /// <summary>
    /// Get recommended sources for a keyword using LLM
    /// </summary>
    /// <param name="keyword">The keyword to find sources for</param>
    /// <returns>Collection of recommended sources</returns>
    Task<IEnumerable<Source>> RecommendSourcesAsync(string keyword);
}

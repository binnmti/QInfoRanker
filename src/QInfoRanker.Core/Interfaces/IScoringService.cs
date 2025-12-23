using QInfoRanker.Core.Entities;

namespace QInfoRanker.Core.Interfaces;

/// <summary>
/// Interface for scoring articles using LLM and hybrid methods
/// </summary>
public interface IScoringService
{
    /// <summary>
    /// Score an article using LLM evaluation
    /// </summary>
    /// <param name="article">The article to score</param>
    /// <param name="includeContent">Whether to include full content in evaluation (more expensive)</param>
    /// <returns>LLM score (0-100)</returns>
    Task<double> ScoreArticleAsync(Article article, bool includeContent = false);
    
    /// <summary>
    /// Calculate the final hybrid score for an article
    /// </summary>
    /// <param name="article">The article to score</param>
    /// <returns>Final hybrid score</returns>
    double CalculateFinalScore(Article article);
}

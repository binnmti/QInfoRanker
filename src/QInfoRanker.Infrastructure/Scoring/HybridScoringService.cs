using QInfoRanker.Core.Entities;
using QInfoRanker.Core.Interfaces;

namespace QInfoRanker.Infrastructure.Scoring;

/// <summary>
/// Hybrid scoring service for articles
/// </summary>
public class HybridScoringService : IScoringService
{
    // Weights for hybrid scoring
    private const double NativeWeight = 0.7;
    private const double LlmWeight = 0.3;

    public Task<double> ScoreArticleAsync(Article article, bool includeContent = false)
    {
        // NOTE: This is a placeholder implementation until Azure OpenAI integration is complete.
        // Current scoring uses simple heuristics based on article metadata.
        // TODO: Replace with actual LLM-based scoring using Azure OpenAI GPT models
        // with structured prompts evaluating technical importance, novelty, impact, and quality.
        
        double llmScore = 50.0; // Default mid-range score
        
        // Simple heuristic scoring based on title length and presence of summary
        if (!string.IsNullOrEmpty(article.Title))
        {
            // Longer, more descriptive titles might indicate quality
            if (article.Title.Length > 50)
                llmScore += 10;
        }

        if (!string.IsNullOrEmpty(article.Summary))
        {
            llmScore += 15;
        }

        // Ensure score is within 0-100 range
        llmScore = Math.Clamp(llmScore, 0, 100);

        return Task.FromResult(llmScore);
    }

    public double CalculateFinalScore(Article article)
    {
        double finalScore = 0;

        // Get source authority weight (default to 0.5 if source not loaded)
        double authorityWeight = article.Source?.AuthorityWeight ?? 0.5;
        bool hasNativeScore = article.Source?.HasNativeScore ?? false;

        if (hasNativeScore && article.NativeScore.HasValue)
        {
            // Normalize native score to 0-100 scale
            double normalizedNative = NormalizeNativeScore(article.NativeScore.Value);
            
            // Use LLM score if available, otherwise use default
            double llmScore = article.LlmScore ?? 50.0;

            // Hybrid scoring: weighted combination
            finalScore = (normalizedNative * NativeWeight) + (llmScore * LlmWeight);
        }
        else
        {
            // No native score: rely entirely on LLM score + authority bonus
            double llmScore = article.LlmScore ?? 50.0;
            finalScore = llmScore;
        }

        // Add authority bonus (0-20 points based on source authority)
        double authorityBonus = authorityWeight * 20;
        finalScore += authorityBonus;

        // Ensure final score is within reasonable range
        return Math.Clamp(finalScore, 0, 120);
    }

    /// <summary>
    /// Normalize native scores to 0-100 scale using logarithmic scaling
    /// </summary>
    private double NormalizeNativeScore(int nativeScore)
    {
        if (nativeScore <= 0)
            return 0;

        // Use logarithmic scaling for native scores
        // This prevents extremely high scores from dominating
        // Common ranges: 1-10 bookmarks -> 0-50, 10-100 -> 50-75, 100+ -> 75-100
        double normalized = Math.Log10(nativeScore + 1) * 33.33;
        
        return Math.Clamp(normalized, 0, 100);
    }
}

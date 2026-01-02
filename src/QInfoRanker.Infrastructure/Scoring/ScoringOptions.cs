namespace QInfoRanker.Infrastructure.Scoring;

public class ScoringOptions
{
    public const string SectionName = "Scoring";

    // Weight for native score (e.g., likes, upvotes)
    public double NativeScoreWeight { get; set; } = 0.7;

    // Weight for LLM score
    public double LlmScoreWeight { get; set; } = 0.3;

    // Weight for LLM score when no native score exists
    public double LlmOnlyWeight { get; set; } = 1.0;

    // Authority bonus multiplier
    public double AuthorityBonusMultiplier { get; set; } = 10.0;

    // Max native scores for normalization (by source)
    public Dictionary<string, int> MaxNativeScores { get; set; } = new()
    {
        { "Hacker News", 500 },
        { "Reddit", 1000 },
        { "Qiita", 200 },
        { "Zenn", 200 },
        { "はてなブックマーク", 500 }
    };

    // Default max for unknown sources
    public int DefaultMaxNativeScore { get; set; } = 100;
}

namespace QInfoRanker.Infrastructure.Scoring;

public class ScoringOptions
{
    public const string SectionName = "Scoring";

    /// <summary>
    /// スコアリングプリセット（設定されている場合、NativeScoreWeight/LlmScoreWeightより優先）
    /// </summary>
    public ScoringPreset? Preset { get; set; } = ScoringPreset.QualityFocused;

    // Weight for native score (e.g., likes, upvotes)
    // Presetが設定されている場合は無視される
    public double NativeScoreWeight { get; set; } = 0.3;

    // Weight for LLM score
    // Presetが設定されている場合は無視される
    public double LlmScoreWeight { get; set; } = 0.7;

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

    /// <summary>
    /// アンサンブル評価での関連性再評価の閾値（0-20点中、この値未満は除外）
    /// Judgeによる詳細評価後、この閾値を下回る記事は最終結果から除外される
    /// </summary>
    public int EnsembleRelevanceThreshold { get; set; } = 6;

    /// <summary>
    /// 実効NativeScoreWeight（プリセットを考慮）
    /// </summary>
    public double EffectiveNativeScoreWeight => Preset switch
    {
        ScoringPreset.QualityFocused => 0.3,
        ScoringPreset.Balanced => 0.5,
        ScoringPreset.PopularityFocused => 0.7,
        _ => NativeScoreWeight
    };

    /// <summary>
    /// 実効LlmScoreWeight（プリセットを考慮）
    /// </summary>
    public double EffectiveLlmScoreWeight => Preset switch
    {
        ScoringPreset.QualityFocused => 0.7,
        ScoringPreset.Balanced => 0.5,
        ScoringPreset.PopularityFocused => 0.3,
        _ => LlmScoreWeight
    };
}

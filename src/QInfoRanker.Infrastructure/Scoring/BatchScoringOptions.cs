namespace QInfoRanker.Infrastructure.Scoring;

public class BatchScoringOptions
{
    public const string SectionName = "BatchScoring";

    /// <summary>
    /// フィルタリングプリセット（設定されている場合、RelevanceThresholdより優先）
    /// </summary>
    public FilteringPreset? FilteringPreset { get; set; } = Scoring.FilteringPreset.Normal;

    // Stage 1: 関連性評価設定
    public int RelevanceBatchSize { get; set; } = 15;
    // FilteringPresetが設定されている場合は無視される
    public double RelevanceThreshold { get; set; } = 3.0;
    public int RelevanceMaxTokens { get; set; } = 2000;

    // Stage 2: 品質評価設定
    public int QualityBatchSize { get; set; } = 3;  // 長いサマリー生成のため減らした
    public int QualityMaxTokens { get; set; } = 4000;

    // バッチ処理設定
    public bool EnableBatchProcessing { get; set; } = true;
    public bool FallbackToIndividual { get; set; } = true;
    public int MaxRetries { get; set; } = 2;
    public int DelayBetweenBatchesMs { get; set; } = 500;

    /// <summary>
    /// 実効RelevanceThreshold（プリセットを考慮）
    /// </summary>
    public double EffectiveRelevanceThreshold => FilteringPreset switch
    {
        Scoring.FilteringPreset.Loose => 2.0,
        Scoring.FilteringPreset.Normal => 3.0,
        Scoring.FilteringPreset.Strict => 6.0,
        _ => RelevanceThreshold
    };
}

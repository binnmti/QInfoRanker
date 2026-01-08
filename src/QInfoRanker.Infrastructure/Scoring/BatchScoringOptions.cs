namespace QInfoRanker.Infrastructure.Scoring;

public class BatchScoringOptions
{
    public const string SectionName = "BatchScoring";

    /// <summary>
    /// フィルタリングプリセット（設定されている場合、RelevanceThresholdより優先）
    /// </summary>
    public FilteringPreset? FilteringPreset { get; set; } = Scoring.FilteringPreset.Normal;

    /// <summary>
    /// Filtering: 関連性フィルタリング設定（高速・低コストモデル推奨）
    /// キーワードとの関連性を0-10で判定し、閾値未満の記事を除外
    /// </summary>
    public StageOptions Filtering { get; set; } = new()
    {
        BatchSize = 15,
        MaxTokens = 2000
    };

    /// <summary>
    /// QualityFallback: アンサンブル評価が失敗した場合のフォールバック品質評価設定
    /// 通常はEnsembleScoringが使用されるため、このセクションは緊急時のみ使用
    /// </summary>
    public StageOptions QualityFallback { get; set; } = new()
    {
        BatchSize = 3,
        MaxTokens = 4000
    };

    // 後方互換性のためのプロパティ
    [Obsolete("Use Filtering.BatchSize instead")]
    public int RelevanceBatchSize { get => Filtering.BatchSize; set => Filtering.BatchSize = value; }
    [Obsolete("Use Filtering.MaxTokens instead")]
    public int RelevanceMaxTokens { get => Filtering.MaxTokens; set => Filtering.MaxTokens = value; }
    [Obsolete("Use QualityFallback.BatchSize instead")]
    public int QualityBatchSize { get => QualityFallback.BatchSize; set => QualityFallback.BatchSize = value; }
    [Obsolete("Use QualityFallback.MaxTokens instead")]
    public int QualityMaxTokens { get => QualityFallback.MaxTokens; set => QualityFallback.MaxTokens = value; }

    // FilteringPresetが設定されている場合は無視される
    public double RelevanceThreshold { get; set; } = 3.0;

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

/// <summary>
/// ステージごとの設定
/// </summary>
public class StageOptions
{
    /// <summary>
    /// 使用するモデルのデプロイメント名（appsettings.jsonで設定必須）
    /// </summary>
    public string DeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// バッチサイズ（1回のAPI呼び出しで処理する件数）
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// 最大トークン数
    /// </summary>
    public int MaxTokens { get; set; } = 2000;
}

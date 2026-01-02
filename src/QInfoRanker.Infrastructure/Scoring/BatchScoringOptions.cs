namespace QInfoRanker.Infrastructure.Scoring;

public class BatchScoringOptions
{
    public const string SectionName = "BatchScoring";

    // Stage 1: 関連性評価設定
    public int RelevanceBatchSize { get; set; } = 15;
    public double RelevanceThreshold { get; set; } = 5.0;
    public int RelevanceMaxTokens { get; set; } = 2000;

    // Stage 2: 品質評価設定
    public int QualityBatchSize { get; set; } = 5;
    public int QualityMaxTokens { get; set; } = 3000;

    // バッチ処理設定
    public bool EnableBatchProcessing { get; set; } = true;
    public bool FallbackToIndividual { get; set; } = true;
    public int MaxRetries { get; set; } = 2;
    public int DelayBetweenBatchesMs { get; set; } = 500;
}

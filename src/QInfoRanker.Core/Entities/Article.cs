namespace QInfoRanker.Core.Entities;

public class Article
{
    public int Id { get; set; }
    public int SourceId { get; set; }
    public int KeywordId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Content { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;

    // スコアリング
    public int? NativeScore { get; set; }
    public double? LlmScore { get; set; }
    public double FinalScore { get; set; }

    // LLMスコア詳細
    public int? TechnicalScore { get; set; }
    public int? NoveltyScore { get; set; }
    public int? ImpactScore { get; set; }
    public int? QualityScore { get; set; }

    // スコアリング詳細
    public double? RelevanceScore { get; set; }        // 0-10 フィルタリング時の簡易関連性スコア
    public int? EnsembleRelevanceScore { get; set; }   // 0-20 アンサンブル評価での最終関連性スコア
    public bool? IsRelevant { get; set; }              // 関連性閾値判定結果
    public string? SummaryJa { get; set; }             // AI生成の日本語要約
    public int? RecommendScore { get; set; }           // 0-20 おすすめ度（閾値以上で炎表示）

    public Source Source { get; set; } = null!;
    public Keyword Keyword { get; set; } = null!;
}

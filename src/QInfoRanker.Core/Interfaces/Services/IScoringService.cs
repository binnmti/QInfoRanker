using QInfoRanker.Core.Entities;

namespace QInfoRanker.Core.Interfaces.Services;

public interface IScoringService
{
    // 既存メソッド（後方互換性のため維持）
    Task<Article> CalculateLlmScoreAsync(Article article, bool includeContent = false, CancellationToken cancellationToken = default);
    Task<IEnumerable<Article>> CalculateLlmScoresAsync(IEnumerable<Article> articles, CancellationToken cancellationToken = default);
    double CalculateFinalScore(Article article, Source source);
    double NormalizeNativeScore(int? nativeScore, string sourceName);

    // 2段階バッチ評価
    // skipRelevanceFilter: trueの場合はStage 1（関連性フィルタリング）をスキップしてStage 2のみ実行
    // サーバー側でキーワード検索済みのソースからの記事の場合はtrueを指定
    Task<TwoStageResult> EvaluateTwoStageAsync(
        IEnumerable<Article> articles,
        IEnumerable<string> keywords,
        bool skipRelevanceFilter = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// AIサービスの利用可能性をチェック
    /// </summary>
    /// <returns>利用可能な場合true、そうでない場合は例外をスロー</returns>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}

#region バッチスコアリング結果クラス

public class TwoStageResult
{
    public BatchRelevanceResult RelevanceResult { get; set; } = new();
    public BatchQualityResult QualityResult { get; set; } = new();
    public int TotalApiCalls { get; set; }
    public TimeSpan TotalDuration { get; set; }
}

public class BatchRelevanceResult
{
    public List<ArticleRelevance> Evaluations { get; set; } = new();
    public int TotalProcessed { get; set; }
    public int RelevantCount { get; set; }
    public int FilteredCount { get; set; }
    public int ApiCallCount { get; set; }
}

public class ArticleRelevance
{
    public int ArticleId { get; set; }
    public double RelevanceScore { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool IsRelevant { get; set; }
}

public class BatchQualityResult
{
    public List<ArticleQuality> Evaluations { get; set; } = new();
    public int TotalProcessed { get; set; }
    public int ApiCallCount { get; set; }
}

public class ArticleQuality
{
    public int ArticleId { get; set; }
    public int Technical { get; set; }
    public int Novelty { get; set; }
    public int Impact { get; set; }
    public int Quality { get; set; }
    public int Total { get; set; }
    public string SummaryJa { get; set; } = string.Empty;
}

#endregion

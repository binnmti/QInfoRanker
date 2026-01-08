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

    // 進捗コールバック付きの2段階バッチ評価
    Task<TwoStageResult> EvaluateTwoStageAsync(
        IEnumerable<Article> articles,
        IEnumerable<string> keywords,
        bool skipRelevanceFilter,
        IProgress<ScoringProgress>? progress,
        CancellationToken cancellationToken = default);

    // コールバック付きの2段階バッチ評価（フィルタ通過時・品質評価完了時に通知）
    Task<TwoStageResult> EvaluateTwoStageAsync(
        IEnumerable<Article> articles,
        IEnumerable<string> keywords,
        bool skipRelevanceFilter,
        IProgress<ScoringProgress>? progress,
        Action<BatchRelevanceResult, IEnumerable<Article>>? onRelevanceComplete,
        Action<IEnumerable<Article>, IEnumerable<ArticleQuality>>? onQualityBatchComplete,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// AIサービスの利用可能性をチェック
    /// </summary>
    /// <returns>利用可能な場合true、そうでない場合は例外をスロー</returns>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);

    #region アンサンブル評価

    /// <summary>
    /// 単一記事のアンサンブル評価を実行
    /// 複数のJudgeモデルで並列評価し、Meta-Judgeで統合
    /// </summary>
    Task<EnsembleEvaluationResult> EvaluateEnsembleAsync(
        Article article,
        IEnumerable<string> keywords,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 3段階評価（関連性→アンサンブル品質→Meta-Judge）を実行
    /// アンサンブルが無効の場合は従来の2段階評価にフォールバック
    /// </summary>
    Task<ThreeStageResult> EvaluateThreeStageAsync(
        IEnumerable<Article> articles,
        IEnumerable<string> keywords,
        bool skipRelevanceFilter,
        IProgress<ScoringProgress>? progress,
        CancellationToken cancellationToken = default);

    #endregion
}

/// <summary>
/// スコアリング進捗情報
/// </summary>
public record ScoringProgress(
    ScoringStage Stage,
    int CurrentBatch,
    int TotalBatches,
    int ProcessedArticles,
    int TotalArticles,
    int RelevantCount,  // フィルタリング段階で関連ありと判定された記事数
    string Message
);

public enum ScoringStage
{
    RelevanceEvaluation,
    QualityEvaluation
}

#region バッチスコアリング結果クラス

public class TwoStageResult
{
    public BatchRelevanceResult RelevanceResult { get; set; } = new();
    public BatchQualityResult QualityResult { get; set; } = new();
    public int TotalApiCalls { get; set; }
    public TimeSpan TotalDuration { get; set; }

    // トークン使用量
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public int TotalTokens => TotalInputTokens + TotalOutputTokens;

    // 推定コスト (USD) - gpt-4o-mini料金: Input $0.15/1M, Output $0.60/1M
    public decimal EstimatedCostUsd =>
        (TotalInputTokens * 0.00000015m) + (TotalOutputTokens * 0.0000006m);
}

public class BatchRelevanceResult
{
    public List<ArticleRelevance> Evaluations { get; set; } = new();
    public int TotalProcessed { get; set; }
    public int RelevantCount { get; set; }
    public int FilteredCount { get; set; }
    public int ApiCallCount { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
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
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}

public class ArticleQuality
{
    public int ArticleId { get; set; }
    public int Relevance { get; set; }  // Stage 2での最終関連性 (0-20)
    public int Technical { get; set; }
    public int Novelty { get; set; }
    public int Impact { get; set; }
    public int Quality { get; set; }
    public int Total { get; set; }
    public string SummaryJa { get; set; } = string.Empty;
}

#endregion

#region アンサンブル評価結果クラス

/// <summary>
/// 個別Judgeの評価結果
/// </summary>
public class JudgeEvaluation
{
    public string JudgeId { get; set; } = string.Empty;
    public string JudgeDisplayName { get; set; } = string.Empty;
    public int Relevance { get; set; }  // キーワードとの関連性 (0-20)
    public int Technical { get; set; }
    public int Novelty { get; set; }
    public int Impact { get; set; }
    public int Quality { get; set; }
    public int Total { get; set; }
    public string RelevanceReason { get; set; } = string.Empty;
    public string TechnicalReason { get; set; } = string.Empty;
    public string NoveltyReason { get; set; } = string.Empty;
    public string ImpactReason { get; set; } = string.Empty;
    public string QualityReason { get; set; } = string.Empty;
    public string SummaryJa { get; set; } = string.Empty;
    public double Weight { get; set; } = 1.0;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Meta-Judge（統合評価）の結果
/// </summary>
public class MetaJudgeResult
{
    public int FinalRelevance { get; set; }  // 最終関連性スコア (0-20)
    public int FinalTechnical { get; set; }
    public int FinalNovelty { get; set; }
    public int FinalImpact { get; set; }
    public int FinalQuality { get; set; }
    public int FinalTotal { get; set; }
    public double Confidence { get; set; }
    public string Rationale { get; set; } = string.Empty;
    public bool HasContradiction { get; set; }
    public List<ContradictionDetail> Contradictions { get; set; } = new();
    public string ConsolidatedSummary { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Judge間の矛盾詳細
/// </summary>
public class ContradictionDetail
{
    public string Dimension { get; set; } = string.Empty;
    public string JudgeA { get; set; } = string.Empty;
    public int ScoreA { get; set; }
    public string JudgeB { get; set; } = string.Empty;
    public int ScoreB { get; set; }
    public int Difference { get; set; }
    public string Resolution { get; set; } = string.Empty;
}

/// <summary>
/// 単一記事のアンサンブル評価結果
/// </summary>
public class EnsembleEvaluationResult
{
    public int ArticleId { get; set; }
    public List<JudgeEvaluation> JudgeEvaluations { get; set; } = new();
    public MetaJudgeResult? MetaJudgeResult { get; set; }
    public int FinalRelevance { get; set; }  // キーワードとの関連性 (0-20)
    public int FinalTechnical { get; set; }
    public int FinalNovelty { get; set; }
    public int FinalImpact { get; set; }
    public int FinalQuality { get; set; }
    public int FinalTotal { get; set; }
    public double Confidence { get; set; }
    public string FinalSummaryJa { get; set; } = string.Empty;
    public bool SkippedMetaJudge { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
}

/// <summary>
/// 3段階評価（関連性→アンサンブル品質→Meta-Judge）の結果
/// </summary>
public class ThreeStageResult
{
    public BatchRelevanceResult RelevanceResult { get; set; } = new();
    public List<EnsembleEvaluationResult> EnsembleResults { get; set; } = new();
    public int TotalApiCalls { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public int TotalTokens => TotalInputTokens + TotalOutputTokens;

    /// <summary>
    /// Meta-Judgeがスキップされた記事数
    /// </summary>
    public int MetaJudgeSkippedCount { get; set; }

    /// <summary>
    /// 推定コスト (USD)
    /// gpt-5/o3等の料金は変動するため概算
    /// </summary>
    public decimal EstimatedCostUsd { get; set; }
}

#endregion

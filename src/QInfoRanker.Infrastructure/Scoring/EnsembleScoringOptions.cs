namespace QInfoRanker.Infrastructure.Scoring;

/// <summary>
/// 本評価（Ensemble Scoring）の設定
/// フィルタリング後の記事に対して5軸評価を実行
///
/// アーキテクチャ:
/// 1. Filtering (BatchScoring.Filtering) - 関連性フィルタリング（高速・低コスト）
/// 2. Ensemble (この設定) - 5軸評価（高品質モデル）
/// </summary>
public class EnsembleScoringOptions
{
    public const string SectionName = "EnsembleScoring";

    /// <summary>
    /// 評価に使用するモデルのデプロイメント名
    /// 推論モデル（o3-mini, gpt-5等）を推奨
    /// </summary>
    public string DeploymentName { get; set; } = "o3-mini";

    /// <summary>
    /// 1回のAPI呼び出しで評価する記事数
    /// 推奨: 3-5件（精度とコストのバランス）
    /// </summary>
    public int BatchSize { get; set; } = 5;

    /// <summary>
    /// 評価のタイムアウト（ミリ秒）
    /// </summary>
    public int TimeoutMs { get; set; } = 120000;

    /// <summary>
    /// 生成時の温度パラメータ（通常モデル専用）
    /// 推論モデル(o3, gpt-5等)では自動的に無視される
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// 最大トークン数（通常モデル専用）
    /// 推論モデルでは自動的に無視される
    /// </summary>
    public int MaxTokens { get; set; } = 4000;

    #region 後方互換性（非推奨）

    /// <summary>
    /// 旧: Judgeモデル構成リスト
    /// 新アーキテクチャでは使用しない。DeploymentNameを使用すること。
    /// </summary>
    [Obsolete("Use DeploymentName instead. Multi-judge evaluation has been replaced with unified evaluation.")]
    public List<JudgeModelConfiguration> Judges { get; set; } = new();

    /// <summary>
    /// 旧: Meta-Judge構成
    /// 新アーキテクチャでは使用しない。DeploymentNameを使用すること。
    /// </summary>
    [Obsolete("Use DeploymentName instead. Meta-judge has been removed in favor of unified evaluation.")]
    public MetaJudgeConfiguration MetaJudge { get; set; } = new();

    /// <summary>
    /// 旧: 同時実行Judge数
    /// </summary>
    [Obsolete("Multi-judge evaluation has been removed.")]
    public int MaxParallelJudges { get; set; } = 2;

    /// <summary>
    /// 旧: Judgeタイムアウト
    /// </summary>
    [Obsolete("Use TimeoutMs instead.")]
    public int JudgeTimeoutMs { get => TimeoutMs; set => TimeoutMs = value; }

    #endregion
}

/// <summary>
/// 旧: Judgeモデル構成（後方互換性のため残存）
/// </summary>
[Obsolete("Multi-judge evaluation has been removed. Use EnsembleScoringOptions.DeploymentName instead.")]
public class JudgeModelConfiguration
{
    public string JudgeId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string EffectiveDisplayName => string.IsNullOrEmpty(DisplayName) ? JudgeId : DisplayName;
    public string DeploymentName { get; set; } = string.Empty;
    public double Weight { get; set; } = 1.0;
    public string? Specialty { get; set; }
    public int MaxTokens { get; set; } = 0;
    public float? Temperature { get; set; }
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// 旧: Meta-Judge構成（後方互換性のため残存）
/// </summary>
[Obsolete("Meta-judge has been removed. Use EnsembleScoringOptions.DeploymentName instead.")]
public class MetaJudgeConfiguration
{
    public bool IsEnabled { get; set; } = true;
    public string DeploymentName { get; set; } = "o3-mini";
    public int MaxTokens { get; set; } = 0;
    public float? Temperature { get; set; }
    public double ContradictionThreshold { get; set; } = 15.0;
}

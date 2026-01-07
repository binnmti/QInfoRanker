namespace QInfoRanker.Infrastructure.Scoring;

/// <summary>
/// アンサンブル評価システムの設定
/// 複数のJudgeモデルによる並列評価とMeta-Judgeによる統合評価を構成
/// </summary>
public class EnsembleScoringOptions
{
    public const string SectionName = "EnsembleScoring";

    /// <summary>
    /// アンサンブル評価を有効化するか
    /// false の場合は従来の単一モデル評価を使用
    /// </summary>
    public bool EnableEnsemble { get; set; } = false;

    /// <summary>
    /// Judge モデルの構成リスト
    /// </summary>
    public List<JudgeModelConfiguration> Judges { get; set; } = new();

    /// <summary>
    /// Meta-Judge（最終統合評価）の構成
    /// </summary>
    public MetaJudgeConfiguration MetaJudge { get; set; } = new();

    /// <summary>
    /// コンセンサス判定の閾値（各軸のスコア差がこの値以下なら合意とみなす）
    /// </summary>
    public double ConsensusThreshold { get; set; } = 5.0;

    /// <summary>
    /// コンセンサスがある場合にMeta-Judgeをスキップするか
    /// trueの場合、全Judgeが合意していればコスト削減のためMeta-Judgeを省略
    /// </summary>
    public bool SkipMetaJudgeOnConsensus { get; set; } = true;

    /// <summary>
    /// 同時に実行するJudgeの最大数
    /// </summary>
    public int MaxParallelJudges { get; set; } = 3;

    /// <summary>
    /// 各Judgeのタイムアウト（ミリ秒）
    /// </summary>
    public int JudgeTimeoutMs { get; set; } = 60000;
}

/// <summary>
/// 個別のJudgeモデルの構成
/// </summary>
public class JudgeModelConfiguration
{
    /// <summary>
    /// Judge の一意識別子（例: "JudgeA", "JudgeB"）
    /// </summary>
    public string JudgeId { get; set; } = string.Empty;

    /// <summary>
    /// 表示名（例: "gpt-5 (汎用評価)"）
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Azure OpenAI デプロイメント名
    /// これによりモデルの機能（推論モデルかどうか）が自動判定される
    /// </summary>
    public string DeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// このJudgeの重み（最終スコア計算時に使用）
    /// </summary>
    public double Weight { get; set; } = 1.0;

    /// <summary>
    /// 専門分野（null: 汎用, "technical": 技術評価, "reasoning": 推論評価）
    /// プロンプトのカスタマイズに使用
    /// </summary>
    public string? Specialty { get; set; }

    /// <summary>
    /// 最大トークン数のオーバーライド（通常モデル専用）
    /// 推論モデル(gpt-5, o3等)では無視される（max_tokensをサポートしないため）
    /// 0以下の場合はModelCapabilities.DefaultMaxTokensが使用される
    /// </summary>
    public int MaxTokens { get; set; } = 0;

    /// <summary>
    /// 生成時の温度パラメータのオーバーライド（通常モデル専用）
    /// 推論モデル(gpt-5, o3等)では無視される（temperatureをサポートしないため）
    /// nullまたは未指定の場合はModelCapabilities.DefaultTemperatureが使用される
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// このJudgeを有効にするか
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Meta-Judge（統合評価モデル）の構成
/// </summary>
public class MetaJudgeConfiguration
{
    /// <summary>
    /// Meta-Judgeを有効にするか
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Azure OpenAI デプロイメント名
    /// 推論モデル(gpt-5, o3等)推奨。モデル機能は自動判定される
    /// </summary>
    public string DeploymentName { get; set; } = "o3-pro";

    /// <summary>
    /// 最大トークン数のオーバーライド（通常モデル専用）
    /// 推論モデル(gpt-5, o3等)では無視される
    /// </summary>
    public int MaxTokens { get; set; } = 0;

    /// <summary>
    /// 生成時の温度パラメータのオーバーライド（通常モデル専用）
    /// 推論モデル(gpt-5, o3等)では無視される
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// 矛盾検出の閾値（各軸のスコア差がこの値を超えたら矛盾とみなす）
    /// </summary>
    public double ContradictionThreshold { get; set; } = 15.0;
}

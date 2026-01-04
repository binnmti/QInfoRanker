namespace QInfoRanker.Infrastructure.Scoring;

/// <summary>
/// スコアリングのプリセット設定
/// </summary>
public enum ScoringPreset
{
    /// <summary>
    /// 質重視: AI評価を重視し、人気がなくても良質な記事が上位に
    /// </summary>
    QualityFocused,

    /// <summary>
    /// バランス型: 人気度とAI評価を均等に評価
    /// </summary>
    Balanced,

    /// <summary>
    /// 人気重視: いいね数やupvote等の人気度を重視
    /// </summary>
    PopularityFocused
}

/// <summary>
/// フィルタリングのプリセット設定
/// </summary>
public enum FilteringPreset
{
    /// <summary>
    /// 緩め: 幅広く記事を表示（閾値 2.0）
    /// </summary>
    Loose,

    /// <summary>
    /// 通常: 明らかに無関係な記事だけ除外（閾値 3.0）
    /// </summary>
    Normal,

    /// <summary>
    /// 厳格: 関連性の高い記事のみ表示（閾値 6.0）
    /// </summary>
    Strict
}

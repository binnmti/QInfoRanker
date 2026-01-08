namespace QInfoRanker.Infrastructure.Scoring;

public class WeeklySummaryOptions
{
    public const string SectionName = "WeeklySummary";

    /// <summary>
    /// 週次サマリー生成に使用するモデル名
    /// </summary>
    public string DeploymentName { get; set; } = "o3-mini";
}

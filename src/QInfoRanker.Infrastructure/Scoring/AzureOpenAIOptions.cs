namespace QInfoRanker.Infrastructure.Scoring;

public class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = "gpt-4o-mini";
    public int MaxTokens { get; set; } = 500;
    public float Temperature { get; set; } = 0.3f;
}

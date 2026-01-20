namespace QInfoRanker.Infrastructure.Scoring;

/// <summary>
/// 画像生成（DALL-E 3）の設定オプション
/// </summary>
public class SummaryImageOptions
{
    /// <summary>
    /// Azure OpenAI エンドポイント（画像生成用）
    /// 未設定の場合は AzureOpenAI:Endpoint を使用
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Azure OpenAI APIキー（画像生成用）
    /// 未設定の場合は AzureOpenAI:ApiKey を使用
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// DALL-E 3のデプロイメント名
    /// </summary>
    public string DeploymentName { get; set; } = "dall-e-3";

    /// <summary>
    /// 画像サイズ（1024x1024, 1792x1024, 1024x1792）
    /// </summary>
    public string Size { get; set; } = "1024x1024";

    /// <summary>
    /// 画像品質（standard, hd）
    /// </summary>
    public string Quality { get; set; } = "standard";

    /// <summary>
    /// 画像スタイル（natural, vivid）
    /// </summary>
    public string Style { get; set; } = "vivid";

    /// <summary>
    /// 画像生成を有効にするかどうか
    /// </summary>
    public bool Enabled { get; set; } = true;
}

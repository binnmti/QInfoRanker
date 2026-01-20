namespace QInfoRanker.Infrastructure.Scoring;

/// <summary>
/// Azure Blob Storage の設定オプション
/// </summary>
public class BlobStorageOptions
{
    /// <summary>
    /// Azure Blob Storage の接続文字列
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// 画像を保存するコンテナ名
    /// </summary>
    public string ContainerName { get; set; } = "weekly-summary-images";

    /// <summary>
    /// Blob Storage が設定されているかどうか
    /// </summary>
    public bool IsConfigured => !string.IsNullOrEmpty(ConnectionString);
}

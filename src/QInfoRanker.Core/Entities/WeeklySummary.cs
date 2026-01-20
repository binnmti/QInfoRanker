namespace QInfoRanker.Core.Entities;

public class WeeklySummary
{
    public int Id { get; set; }
    public int KeywordId { get; set; }
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int ArticleCount { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// DALL-E 3で生成された画像のURL（Azure Blob Storageに保存）
    /// </summary>
    public string? ImageUrl { get; set; }

    public Keyword Keyword { get; set; } = null!;
}

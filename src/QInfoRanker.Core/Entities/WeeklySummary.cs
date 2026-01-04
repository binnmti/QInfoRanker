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

    public Keyword Keyword { get; set; } = null!;
}

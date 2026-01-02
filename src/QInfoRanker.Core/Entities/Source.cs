using QInfoRanker.Core.Enums;

namespace QInfoRanker.Core.Entities;

public class Source
{
    public int Id { get; set; }
    public int? KeywordId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? SearchUrlTemplate { get; set; }
    public SourceType Type { get; set; }
    public bool HasNativeScore { get; set; }
    public bool HasServerSideFiltering { get; set; } = true; // サーバー側でキーワード検索している場合はtrue
    public double AuthorityWeight { get; set; } = 0.5;
    public bool IsActive { get; set; } = true;
    public bool IsAutoDiscovered { get; set; }
    public bool IsTemplate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Language and Category
    public Language Language { get; set; } = Language.Both;
    public SourceCategory Category { get; set; } = SourceCategory.Technology;

    // AI推薦の理由（キーワード作成時にAIが選んだ理由）
    public string? RecommendationReason { get; set; }

    public Keyword? Keyword { get; set; }
    public ICollection<Article> Articles { get; set; } = new List<Article>();
}

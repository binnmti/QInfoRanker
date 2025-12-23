namespace QInfoRanker.Core.Entities;

/// <summary>
/// Represents an information source for collecting articles
/// </summary>
public class Source
{
    public int Id { get; set; }
    
    /// <summary>
    /// Associated keyword ID (null for generic/template sources)
    /// </summary>
    public int? KeywordId { get; set; }
    
    /// <summary>
    /// Source name (e.g., "はてなブックマーク", "Qiita", "Google Quantum AI")
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Base URL of the source
    /// </summary>
    public string Url { get; set; } = string.Empty;
    
    /// <summary>
    /// URL template for keyword search ({keyword} is placeholder)
    /// </summary>
    public string? SearchUrlTemplate { get; set; }
    
    /// <summary>
    /// Type of collection method
    /// </summary>
    public SourceType Type { get; set; }
    
    /// <summary>
    /// Whether this source has native scoring (likes, bookmarks, etc.)
    /// </summary>
    public bool HasNativeScore { get; set; }
    
    /// <summary>
    /// Authority weight of this source (0.0-1.0)
    /// </summary>
    public double AuthorityWeight { get; set; } = 0.5;
    
    /// <summary>
    /// Whether this source is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Whether this source was auto-discovered by LLM
    /// </summary>
    public bool IsAutoDiscovered { get; set; }
    
    /// <summary>
    /// Navigation property to associated keyword
    /// </summary>
    public Keyword? Keyword { get; set; }
    
    /// <summary>
    /// Articles collected from this source
    /// </summary>
    public ICollection<Article> Articles { get; set; } = new List<Article>();
}

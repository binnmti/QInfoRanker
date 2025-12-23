namespace QInfoRanker.Core.Entities;

/// <summary>
/// Represents a collected article/post
/// </summary>
public class Article
{
    public int Id { get; set; }
    
    /// <summary>
    /// Source from which this article was collected
    /// </summary>
    public int SourceId { get; set; }
    
    /// <summary>
    /// Article title
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Article URL
    /// </summary>
    public string Url { get; set; } = string.Empty;
    
    /// <summary>
    /// Article summary/excerpt
    /// </summary>
    public string? Summary { get; set; }
    
    /// <summary>
    /// When the article was published
    /// </summary>
    public DateTime PublishedAt { get; set; }
    
    /// <summary>
    /// When this article was collected
    /// </summary>
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
    
    // Scoring properties
    
    /// <summary>
    /// Native score from source (bookmarks, likes, etc.)
    /// </summary>
    public int? NativeScore { get; set; }
    
    /// <summary>
    /// LLM-based quality score (0-100)
    /// </summary>
    public double? LlmScore { get; set; }
    
    /// <summary>
    /// Final hybrid score for ranking
    /// </summary>
    public double FinalScore { get; set; }
    
    /// <summary>
    /// Navigation property to source
    /// </summary>
    public Source Source { get; set; } = null!;
}

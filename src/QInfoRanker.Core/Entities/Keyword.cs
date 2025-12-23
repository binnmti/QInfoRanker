namespace QInfoRanker.Core.Entities;

/// <summary>
/// Represents a search keyword for information aggregation
/// </summary>
public class Keyword
{
    public int Id { get; set; }
    
    /// <summary>
    /// The search term (e.g., "量子コンピュータ", "quantum computing")
    /// </summary>
    public string Term { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this keyword is currently active for collection
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// When this keyword was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Sources associated with this keyword
    /// </summary>
    public ICollection<Source> Sources { get; set; } = new List<Source>();
}

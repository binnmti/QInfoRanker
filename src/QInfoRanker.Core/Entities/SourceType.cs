namespace QInfoRanker.Core.Entities;

/// <summary>
/// Type of information source collection method
/// </summary>
public enum SourceType
{
    /// <summary>
    /// REST API endpoint
    /// </summary>
    API,
    
    /// <summary>
    /// RSS/Atom feed
    /// </summary>
    RSS,
    
    /// <summary>
    /// Web scraping required
    /// </summary>
    Scraping
}

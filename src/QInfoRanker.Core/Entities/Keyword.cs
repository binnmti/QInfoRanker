namespace QInfoRanker.Core.Entities;

public class Keyword
{
    public int Id { get; set; }
    public string Term { get; set; } = string.Empty;
    public string? Aliases { get; set; } // Comma-separated alternative search terms (e.g., English translations)
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Source> Sources { get; set; } = new List<Source>();
    public ICollection<Article> Articles { get; set; } = new List<Article>();

    /// <summary>
    /// Gets all search terms including the main term and aliases
    /// </summary>
    public IEnumerable<string> GetAllSearchTerms()
    {
        yield return Term;

        if (!string.IsNullOrEmpty(Aliases))
        {
            foreach (var alias in Aliases.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return alias;
            }
        }
    }
}

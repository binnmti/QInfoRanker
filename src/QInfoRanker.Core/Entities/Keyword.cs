using System.Text.RegularExpressions;

namespace QInfoRanker.Core.Entities;

public class Keyword
{
    public int Id { get; set; }
    public string Term { get; set; } = string.Empty;
    public string? Aliases { get; set; } // Comma-separated alternative search terms (e.g., English translations)
    public string? Slug { get; set; } // URL-friendly identifier (e.g., "quantum-computing")
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

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

    /// <summary>
    /// Gets the URL identifier for this keyword (Slug if available, otherwise Id)
    /// </summary>
    public string GetUrlIdentifier() => Slug ?? Id.ToString();

    /// <summary>
    /// Generates a slug from the given text
    /// </summary>
    public static string? GenerateSlug(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Lowercase
        var slug = text.ToLowerInvariant();

        // Replace spaces and underscores with hyphens
        slug = slug.Replace(' ', '-').Replace('_', '-');

        // Remove non-alphanumeric characters except hyphens
        slug = Regex.Replace(slug, @"[^a-z0-9\-]", "");

        // Replace multiple hyphens with single hyphen
        slug = Regex.Replace(slug, @"-+", "-");

        // Trim hyphens from start and end
        slug = slug.Trim('-');

        return string.IsNullOrEmpty(slug) ? null : slug;
    }

    /// <summary>
    /// Generates a slug from this keyword's aliases (uses first English alias)
    /// </summary>
    public string? GenerateSlugFromAliases()
    {
        if (string.IsNullOrEmpty(Aliases))
            return null;

        // Get the first alias that looks like English text
        var aliases = Aliases.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var englishAlias = aliases.FirstOrDefault(a => Regex.IsMatch(a, @"^[a-zA-Z\s\-]+$"));

        return GenerateSlug(englishAlias);
    }
}

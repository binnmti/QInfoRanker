using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using QInfoRanker.Core.Entities;

namespace QInfoRanker.Infrastructure.Collectors;

public class BBCNewsCollector : BaseCollector
{
    // BBC RSS feeds by section
    private static readonly Dictionary<string, string> SectionFeeds = new()
    {
        ["top"] = "https://feeds.bbci.co.uk/news/rss.xml",
        ["world"] = "https://feeds.bbci.co.uk/news/world/rss.xml",
        ["business"] = "https://feeds.bbci.co.uk/news/business/rss.xml",
        ["technology"] = "https://feeds.bbci.co.uk/news/technology/rss.xml",
        ["science"] = "https://feeds.bbci.co.uk/news/science_and_environment/rss.xml",
        ["entertainment"] = "https://feeds.bbci.co.uk/news/entertainment_and_arts/rss.xml",
        ["health"] = "https://feeds.bbci.co.uk/news/health/rss.xml"
    };

    public BBCNewsCollector(HttpClient httpClient, ILogger<BBCNewsCollector> logger)
        : base(httpClient, logger)
    {
    }

    public override string SourceName => "BBC News";

    public override bool CanHandle(Source source)
    {
        return source.Name.Contains("BBC", StringComparison.OrdinalIgnoreCase) ||
               source.Url.Contains("bbc.co.uk", StringComparison.OrdinalIgnoreCase) ||
               source.Url.Contains("bbci.co.uk", StringComparison.OrdinalIgnoreCase);
    }

    public override async Task<IEnumerable<Article>> CollectAsync(
        Source source,
        string keyword,
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        var articles = new List<Article>();

        try
        {
            Logger.LogInformation("Collecting from BBC News for keyword: {Keyword}", keyword);

            // Determine which feeds to check based on the source URL
            var feedUrls = GetRelevantFeeds(source);

            foreach (var feedUrl in feedUrls)
            {
                var feedArticles = await CollectFromFeedAsync(source, feedUrl, keyword, since, cancellationToken);
                articles.AddRange(feedArticles);
            }

            // Remove duplicates based on URL
            articles = articles
                .GroupBy(a => a.Url)
                .Select(g => g.First())
                .ToList();

            Logger.LogInformation("Collected {Count} articles from BBC News", articles.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error collecting from BBC News");
        }

        return articles;
    }

    private List<string> GetRelevantFeeds(Source source)
    {
        var url = source.SearchUrlTemplate ?? source.Url;

        // If a specific section is in the URL, use only that feed
        foreach (var (section, feedUrl) in SectionFeeds)
        {
            if (url.Contains(section, StringComparison.OrdinalIgnoreCase))
            {
                return [feedUrl];
            }
        }

        // Default: use top stories, world, business, technology
        return
        [
            SectionFeeds["top"],
            SectionFeeds["world"],
            SectionFeeds["business"],
            SectionFeeds["technology"]
        ];
    }

    private async Task<List<Article>> CollectFromFeedAsync(
        Source source,
        string feedUrl,
        string keyword,
        DateTime? since,
        CancellationToken cancellationToken)
    {
        var articles = new List<Article>();

        var rssContent = await GetStringAsync(feedUrl, cancellationToken);

        if (string.IsNullOrEmpty(rssContent))
            return articles;

        try
        {
            var doc = XDocument.Parse(rssContent);
            var items = doc.Descendants("item");

            // Split keyword into terms for matching
            var keywordTerms = keyword.ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            foreach (var item in items)
            {
                var title = item.Element("title")?.Value?.Trim();
                var link = item.Element("link")?.Value?.Trim();
                var description = item.Element("description")?.Value?.Trim();
                var pubDateStr = item.Element("pubDate")?.Value;

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(link))
                    continue;

                // Client-side keyword filtering
                var combinedText = $"{title} {description}".ToLowerInvariant();
                if (!keywordTerms.Any(term => combinedText.Contains(term)))
                    continue;

                // Parse date
                DateTime? publishedDate = null;
                if (!string.IsNullOrEmpty(pubDateStr) && DateTime.TryParse(pubDateStr, out var parsedDate))
                {
                    publishedDate = parsedDate;
                }

                // Filter by date if since is provided
                if (since.HasValue && publishedDate.HasValue && publishedDate < since.Value)
                    continue;

                var article = new Article
                {
                    SourceId = source.Id,
                    Title = title,
                    Url = link,
                    Summary = description?.Length > 500 ? description[..500] + "..." : description,
                    NativeScore = null, // BBC doesn't provide a score
                    PublishedAt = publishedDate,
                    CollectedAt = DateTime.UtcNow
                };

                articles.Add(article);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to parse BBC News RSS from {Url}", feedUrl);
        }

        return articles;
    }
}

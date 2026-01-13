using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using QInfoRanker.Core.Entities;

namespace QInfoRanker.Infrastructure.Collectors;

public class YahooNewsJapanCollector : BaseCollector
{
    // Yahoo! News Japan RSS feeds by category (updated 2025)
    // Note: it-science.xml was split into it.xml and science.xml
    private static readonly Dictionary<string, string> CategoryFeeds = new()
    {
        ["top"] = "https://news.yahoo.co.jp/rss/topics/top-picks.xml",
        ["domestic"] = "https://news.yahoo.co.jp/rss/topics/domestic.xml",
        ["world"] = "https://news.yahoo.co.jp/rss/topics/world.xml",
        ["business"] = "https://news.yahoo.co.jp/rss/topics/business.xml",
        ["entertainment"] = "https://news.yahoo.co.jp/rss/topics/entertainment.xml",
        ["sports"] = "https://news.yahoo.co.jp/rss/topics/sports.xml",
        ["it"] = "https://news.yahoo.co.jp/rss/topics/it.xml",
        ["science"] = "https://news.yahoo.co.jp/rss/topics/science.xml",
        ["local"] = "https://news.yahoo.co.jp/rss/topics/local.xml"
    };

    public YahooNewsJapanCollector(HttpClient httpClient, ILogger<YahooNewsJapanCollector> logger)
        : base(httpClient, logger)
    {
    }

    public override string SourceName => "Yahoo! News Japan";

    public override bool CanHandle(Source source)
    {
        return source.Name.Contains("Yahoo", StringComparison.OrdinalIgnoreCase) &&
               (source.Name.Contains("News", StringComparison.OrdinalIgnoreCase) ||
                source.Name.Contains("ニュース", StringComparison.OrdinalIgnoreCase)) ||
               source.Url.Contains("news.yahoo.co.jp", StringComparison.OrdinalIgnoreCase);
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
            Logger.LogInformation("Collecting from Yahoo! News Japan for keyword: {Keyword}", keyword);

            // Get relevant feeds based on source configuration
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

            Logger.LogInformation("Collected {Count} articles from Yahoo! News Japan", articles.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error collecting from Yahoo! News Japan");
        }

        return articles;
    }

    private List<string> GetRelevantFeeds(Source source)
    {
        var url = source.SearchUrlTemplate ?? source.Url;

        // If a specific category is in the URL, use only that feed
        foreach (var (category, feedUrl) in CategoryFeeds)
        {
            if (url.Contains(category, StringComparison.OrdinalIgnoreCase))
            {
                return [feedUrl];
            }
        }

        // Default: use top, domestic, business, it, science
        return
        [
            CategoryFeeds["top"],
            CategoryFeeds["domestic"],
            CategoryFeeds["business"],
            CategoryFeeds["it"],
            CategoryFeeds["science"]
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

            // Split keyword into terms for matching (handles both Japanese and English)
            var keywordLower = keyword.ToLowerInvariant();
            var keywordTerms = keyword
                .Split(' ', '　', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.ToLowerInvariant())
                .ToList();

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

                // Check if any keyword term matches (partial match for Japanese)
                var matches = keywordTerms.Any(term => combinedText.Contains(term)) ||
                              combinedText.Contains(keywordLower);

                if (!matches)
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
                    NativeScore = null, // Yahoo! News doesn't provide a score
                    PublishedAt = publishedDate,
                    CollectedAt = DateTime.UtcNow
                };

                articles.Add(article);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to parse Yahoo! News RSS from {Url}", feedUrl);
        }

        return articles;
    }
}

using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using QInfoRanker.Core.Entities;

namespace QInfoRanker.Infrastructure.Collectors;

public class GoogleNewsCollector : BaseCollector
{
    public GoogleNewsCollector(HttpClient httpClient, ILogger<GoogleNewsCollector> logger)
        : base(httpClient, logger)
    {
    }

    public override string SourceName => "Google News";

    public override bool CanHandle(Source source)
    {
        return source.Name.Contains("Google News", StringComparison.OrdinalIgnoreCase) ||
               source.Url.Contains("news.google.com", StringComparison.OrdinalIgnoreCase);
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
            var searchUrl = BuildSearchUrl(source, keyword);
            Logger.LogInformation("Collecting from Google News: {Url}", searchUrl);

            var rssContent = await GetStringAsync(searchUrl, cancellationToken);

            if (string.IsNullOrEmpty(rssContent))
            {
                Logger.LogWarning("No results from Google News for keyword: {Keyword}", keyword);
                return articles;
            }

            var doc = XDocument.Parse(rssContent);
            var items = doc.Descendants("item");

            foreach (var item in items)
            {
                var title = item.Element("title")?.Value?.Trim();
                var link = item.Element("link")?.Value?.Trim();
                var description = item.Element("description")?.Value?.Trim();
                var pubDateStr = item.Element("pubDate")?.Value;
                var sourceElement = item.Element("source")?.Value?.Trim();

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(link))
                    continue;

                // Parse RFC 822 date format (e.g., "Thu, 02 Jan 2025 12:00:00 GMT")
                DateTime? publishedDate = null;
                if (!string.IsNullOrEmpty(pubDateStr) &&
                    DateTime.TryParse(pubDateStr, out var parsedDate))
                {
                    publishedDate = parsedDate;
                }

                // Filter by date if since is provided
                if (since.HasValue && publishedDate.HasValue && publishedDate < since.Value)
                    continue;

                // Append source name to description if available
                var summary = description;
                if (!string.IsNullOrEmpty(sourceElement))
                {
                    summary = $"[{sourceElement}] {description}";
                }

                var article = new Article
                {
                    SourceId = source.Id,
                    Title = CleanHtmlEntities(title),
                    Url = link,
                    Summary = summary?.Length > 500 ? summary[..500] + "..." : summary,
                    NativeScore = null, // Google News doesn't provide a score
                    PublishedAt = publishedDate,
                    CollectedAt = DateTime.UtcNow
                };

                articles.Add(article);
            }

            Logger.LogInformation("Collected {Count} articles from Google News", articles.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error collecting from Google News");
        }

        return articles;
    }

    private static string CleanHtmlEntities(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return text
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("&#39;", "'");
    }
}

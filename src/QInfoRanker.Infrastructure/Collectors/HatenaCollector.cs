using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using QInfoRanker.Core.Entities;

namespace QInfoRanker.Infrastructure.Collectors;

public class HatenaCollector : BaseCollector
{
    private static readonly XNamespace RssNs = "http://purl.org/rss/1.0/";
    private static readonly XNamespace HatenaNs = "http://www.hatena.ne.jp/info/xmlns#";
    private static readonly XNamespace DcNs = "http://purl.org/dc/elements/1.1/";

    public HatenaCollector(HttpClient httpClient, ILogger<HatenaCollector> logger)
        : base(httpClient, logger)
    {
    }

    public override string SourceName => "Hatena Bookmark";

    public override bool CanHandle(Source source)
    {
        return source.Name.Contains("Hatena", StringComparison.OrdinalIgnoreCase) ||
               source.Name.Contains("はてな", StringComparison.OrdinalIgnoreCase) ||
               source.Url.Contains("hatena.ne.jp", StringComparison.OrdinalIgnoreCase) ||
               source.Url.Contains("b.hatena", StringComparison.OrdinalIgnoreCase);
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
            Logger.LogInformation("Collecting from Hatena Bookmark: {Url}", searchUrl);

            var rssContent = await GetStringAsync(searchUrl, cancellationToken);

            if (string.IsNullOrEmpty(rssContent))
            {
                Logger.LogWarning("No results from Hatena Bookmark for keyword: {Keyword}", keyword);
                return articles;
            }

            var doc = XDocument.Parse(rssContent);
            var items = doc.Descendants(RssNs + "item");

            foreach (var item in items)
            {
                var title = item.Element(RssNs + "title")?.Value?.Trim();
                var link = item.Element(RssNs + "link")?.Value?.Trim();
                var description = item.Element(RssNs + "description")?.Value?.Trim();
                var dateStr = item.Element(DcNs + "date")?.Value;
                var bookmarkCount = item.Element(HatenaNs + "bookmarkcount")?.Value;

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(link))
                    continue;

                // Filter by date if since is provided
                if (since.HasValue && DateTime.TryParse(dateStr, out var publishedDate))
                {
                    if (publishedDate < since.Value)
                        continue;
                }

                var article = new Article
                {
                    SourceId = source.Id,
                    KeywordId = source.KeywordId ?? 0,
                    Title = title,
                    Url = link,
                    Summary = description?.Length > 500 ? description[..500] + "..." : description,
                    NativeScore = int.TryParse(bookmarkCount, out var count) ? count : null,
                    PublishedAt = DateTime.TryParse(dateStr, out var date) ? date : null,
                    CollectedAt = DateTime.UtcNow
                };

                articles.Add(article);
            }

            Logger.LogInformation("Collected {Count} articles from Hatena Bookmark", articles.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error collecting from Hatena Bookmark");
        }

        return articles;
    }
}

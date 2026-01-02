using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using QInfoRanker.Core.Entities;

namespace QInfoRanker.Infrastructure.Collectors;

public class ArXivCollector : BaseCollector
{
    private static readonly XNamespace AtomNs = "http://www.w3.org/2005/Atom";

    public ArXivCollector(HttpClient httpClient, ILogger<ArXivCollector> logger)
        : base(httpClient, logger)
    {
    }

    public override string SourceName => "arXiv";

    public override bool CanHandle(Source source)
    {
        return source.Name.Contains("arXiv", StringComparison.OrdinalIgnoreCase) ||
               source.Url.Contains("arxiv.org", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// arXiv用の検索URLを構築。複数語のキーワードはAND検索に変換。
    /// </summary>
    private string BuildArXivSearchUrl(Source source, string keyword)
    {
        if (string.IsNullOrEmpty(source.SearchUrlTemplate))
        {
            return source.Url;
        }

        // arXivではスペースがORとして解釈されるため、複数語をANDで結合
        // "quantum computer" → "quantum+AND+computer"
        var words = keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string searchQuery;

        if (words.Length > 1)
        {
            // 各単語をURIエンコードしてANDで結合
            searchQuery = string.Join("+AND+", words.Select(w => Uri.EscapeDataString(w)));
        }
        else
        {
            searchQuery = Uri.EscapeDataString(keyword);
        }

        return source.SearchUrlTemplate.Replace("{keyword}", searchQuery);
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
            var searchUrl = BuildArXivSearchUrl(source, keyword);
            Logger.LogInformation("Collecting from arXiv: {Url}", searchUrl);

            var xmlContent = await GetStringAsync(searchUrl, cancellationToken);

            if (string.IsNullOrEmpty(xmlContent))
            {
                Logger.LogWarning("No results from arXiv for keyword: {Keyword}", keyword);
                return articles;
            }

            var doc = XDocument.Parse(xmlContent);
            var entries = doc.Descendants(AtomNs + "entry");

            foreach (var entry in entries)
            {
                var title = entry.Element(AtomNs + "title")?.Value?.Trim();
                var summary = entry.Element(AtomNs + "summary")?.Value?.Trim();
                var published = entry.Element(AtomNs + "published")?.Value;

                // arXivのリンクは rel="alternate" で取得（type="text/html"は存在しない）
                var link = entry.Elements(AtomNs + "link")
                    .FirstOrDefault(l => l.Attribute("rel")?.Value == "alternate")
                    ?.Attribute("href")?.Value;

                // alternateがなければ最初のリンクを使用
                if (string.IsNullOrEmpty(link))
                {
                    link = entry.Element(AtomNs + "id")?.Value; // arXivではidがURLになっている
                }

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(link))
                    continue;

                // Clean up title (remove newlines)
                title = title.Replace("\n", " ").Replace("  ", " ").Trim();

                // Filter by date if since is provided
                if (since.HasValue && DateTime.TryParse(published, out var publishedDate))
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
                    Summary = summary?.Length > 1000
                        ? summary[..1000] + "..."
                        : summary,
                    NativeScore = null, // arXiv doesn't have native scores
                    PublishedAt = DateTime.TryParse(published, out var date) ? date : null,
                    CollectedAt = DateTime.UtcNow
                };

                articles.Add(article);
            }

            Logger.LogInformation("Collected {Count} articles from arXiv", articles.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error collecting from arXiv");
        }

        return articles;
    }
}

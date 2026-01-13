using System.Text.Json;
using Microsoft.Extensions.Logging;
using QInfoRanker.Core.Entities;

namespace QInfoRanker.Infrastructure.Collectors;

public class SemanticScholarCollector : BaseCollector
{
    private const string ApiBaseUrl = "https://api.semanticscholar.org/graph/v1/paper/search";

    public SemanticScholarCollector(HttpClient httpClient, ILogger<SemanticScholarCollector> logger)
        : base(httpClient, logger)
    {
    }

    public override string SourceName => "Semantic Scholar";

    public override bool CanHandle(Source source)
    {
        return source.Name.Contains("Semantic Scholar", StringComparison.OrdinalIgnoreCase) ||
               source.Url.Contains("semanticscholar.org", StringComparison.OrdinalIgnoreCase);
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
            Logger.LogInformation("Collecting from Semantic Scholar for keyword: {Keyword}", keyword);

            var searchUrl = BuildApiUrl(keyword, since);
            var jsonContent = await GetStringAsync(searchUrl, cancellationToken);

            if (string.IsNullOrEmpty(jsonContent))
            {
                Logger.LogWarning("No results from Semantic Scholar for keyword: {Keyword}", keyword);
                return articles;
            }

            articles = ParseSearchResults(source, jsonContent);

            Logger.LogInformation("Collected {Count} articles from Semantic Scholar", articles.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error collecting from Semantic Scholar");
        }

        return articles;
    }

    private static string BuildApiUrl(string keyword, DateTime? since)
    {
        var fields = "paperId,title,abstract,citationCount,year,url,publicationDate";
        var url = $"{ApiBaseUrl}?query={Uri.EscapeDataString(keyword)}&limit=50&fields={fields}";

        // Add year filter if since is provided
        if (since.HasValue)
        {
            url += $"&year={since.Value.Year}-";
        }

        return url;
    }

    private List<Article> ParseSearchResults(Source source, string jsonContent)
    {
        var articles = new List<Article>();

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var data))
            {
                return articles;
            }

            foreach (var paper in data.EnumerateArray())
            {
                var paperId = paper.TryGetProperty("paperId", out var idProp) ? idProp.GetString() : null;
                var title = paper.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;
                var abstractText = paper.TryGetProperty("abstract", out var absProp) ? absProp.GetString() : null;
                var citationCount = paper.TryGetProperty("citationCount", out var citeProp) ? citeProp.GetInt32() : 0;
                var year = paper.TryGetProperty("year", out var yearProp) && yearProp.ValueKind != JsonValueKind.Null
                    ? yearProp.GetInt32()
                    : (int?)null;
                var url = paper.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
                var pubDateStr = paper.TryGetProperty("publicationDate", out var pubProp)
                    ? pubProp.GetString()
                    : null;

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(paperId))
                    continue;

                // Build URL if not provided
                if (string.IsNullOrEmpty(url))
                {
                    url = $"https://www.semanticscholar.org/paper/{paperId}";
                }

                // Parse publication date
                DateTime? publishedDate = null;
                if (!string.IsNullOrEmpty(pubDateStr) && DateTime.TryParse(pubDateStr, out var parsedDate))
                {
                    publishedDate = parsedDate;
                }
                else if (year.HasValue)
                {
                    publishedDate = new DateTime(year.Value, 1, 1);
                }

                var article = new Article
                {
                    SourceId = source.Id,
                    Title = title,
                    Url = url,
                    Summary = abstractText?.Length > 500 ? abstractText[..500] + "..." : abstractText,
                    NativeScore = citationCount, // Use citation count as score
                    PublishedAt = publishedDate,
                    CollectedAt = DateTime.UtcNow
                };

                articles.Add(article);
            }
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Failed to parse Semantic Scholar response");
        }

        return articles;
    }
}

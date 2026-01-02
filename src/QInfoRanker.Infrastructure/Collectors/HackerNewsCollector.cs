using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using QInfoRanker.Core.Entities;

namespace QInfoRanker.Infrastructure.Collectors;

public class HackerNewsCollector : BaseCollector
{
    public HackerNewsCollector(HttpClient httpClient, ILogger<HackerNewsCollector> logger)
        : base(httpClient, logger)
    {
    }

    public override string SourceName => "Hacker News";

    public override bool CanHandle(Source source)
    {
        return source.Name.Contains("Hacker News", StringComparison.OrdinalIgnoreCase) ||
               source.Url.Contains("news.ycombinator.com", StringComparison.OrdinalIgnoreCase) ||
               source.Url.Contains("hn.algolia.com", StringComparison.OrdinalIgnoreCase);
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

            // Add time filter if since is provided
            if (since.HasValue)
            {
                var timestamp = new DateTimeOffset(since.Value).ToUnixTimeSeconds();
                searchUrl += $"&numericFilters=created_at_i>{timestamp}";
            }

            Logger.LogInformation("Collecting from Hacker News: {Url}", searchUrl);

            var response = await GetJsonAsync<HackerNewsResponse>(searchUrl, cancellationToken);

            if (response?.Hits == null)
            {
                Logger.LogWarning("No results from Hacker News for keyword: {Keyword}", keyword);
                return articles;
            }

            foreach (var hit in response.Hits)
            {
                if (string.IsNullOrEmpty(hit.Url) || string.IsNullOrEmpty(hit.Title))
                    continue;

                var article = new Article
                {
                    SourceId = source.Id,
                    KeywordId = source.KeywordId ?? 0,
                    Title = hit.Title,
                    Url = hit.Url,
                    Summary = hit.StoryText?.Length > 500
                        ? hit.StoryText[..500] + "..."
                        : hit.StoryText,
                    NativeScore = hit.Points,
                    PublishedAt = hit.CreatedAtUtc,
                    CollectedAt = DateTime.UtcNow
                };

                articles.Add(article);
            }

            Logger.LogInformation("Collected {Count} articles from Hacker News", articles.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error collecting from Hacker News");
        }

        return articles;
    }

    private class HackerNewsResponse
    {
        [JsonPropertyName("hits")]
        public List<HackerNewsHit>? Hits { get; set; }

        [JsonPropertyName("nbHits")]
        public int TotalHits { get; set; }
    }

    private class HackerNewsHit
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("story_text")]
        public string? StoryText { get; set; }

        [JsonPropertyName("points")]
        public int Points { get; set; }

        [JsonPropertyName("created_at_i")]
        public long CreatedAtTimestamp { get; set; }

        public DateTime CreatedAtUtc => DateTimeOffset.FromUnixTimeSeconds(CreatedAtTimestamp).UtcDateTime;

        [JsonPropertyName("objectID")]
        public string? ObjectId { get; set; }
    }
}

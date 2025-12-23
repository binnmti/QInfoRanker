using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using QInfoRanker.Core.Entities;

namespace QInfoRanker.Infrastructure.Collectors;

/// <summary>
/// Collector for Hacker News using Algolia API
/// </summary>
public class HackerNewsCollector : BaseCollector
{
    private readonly ILogger<HackerNewsCollector> _logger;

    public HackerNewsCollector(HttpClient httpClient, ILogger<HackerNewsCollector> logger) 
        : base(httpClient)
    {
        _logger = logger;
    }

    public override bool CanHandle(SourceType sourceType)
    {
        // This is a specialized collector for Hacker News API
        return sourceType == SourceType.API;
    }

    public override async Task<IEnumerable<Article>> CollectAsync(Source source, string keyword, DateTime? since = null)
    {
        // Only handle Hacker News source
        if (!source.SearchUrlTemplate?.Contains("hn.algolia.com") ?? true)
            return Enumerable.Empty<Article>();

        var articles = new List<Article>();
        var searchUrl = BuildSearchUrl(source, keyword);

        try
        {
            var response = await _httpClient.GetAsync(searchUrl);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<HackerNewsResponse>(content);

            if (result?.Hits == null)
                return articles;

            foreach (var hit in result.Hits)
            {
                // Skip if no URL or title
                if (string.IsNullOrEmpty(hit.Url) || string.IsNullOrEmpty(hit.Title))
                    continue;

                // Skip if older than 'since' date
                if (since.HasValue && hit.CreatedAt < since.Value)
                    continue;

                var article = new Article
                {
                    SourceId = source.Id,
                    Title = hit.Title,
                    Url = hit.Url,
                    PublishedAt = hit.CreatedAt,
                    CollectedAt = DateTime.UtcNow,
                    NativeScore = hit.Points,
                    FinalScore = 0 // Will be calculated by scoring service
                };

                articles.Add(article);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting articles from Hacker News for keyword: {Keyword}", keyword);
        }

        return articles;
    }

    // DTOs for Hacker News API response
    private class HackerNewsResponse
    {
        [JsonPropertyName("hits")]
        public List<HackerNewsHit> Hits { get; set; } = new();
    }

    private class HackerNewsHit
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("points")]
        public int Points { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;
    }
}

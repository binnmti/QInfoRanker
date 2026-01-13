using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using QInfoRanker.Core.Entities;

namespace QInfoRanker.Infrastructure.Collectors;

public class RedditCollector : BaseCollector
{
    public RedditCollector(HttpClient httpClient, ILogger<RedditCollector> logger)
        : base(httpClient, logger)
    {
        // Reddit requires a User-Agent header
        if (!httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            httpClient.DefaultRequestHeaders.Add("User-Agent", "QInfoRanker/1.0");
        }
    }

    public override string SourceName => "Reddit";

    public override bool CanHandle(Source source)
    {
        return source.Name.Contains("Reddit", StringComparison.OrdinalIgnoreCase) ||
               source.Url.Contains("reddit.com", StringComparison.OrdinalIgnoreCase);
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
            Logger.LogInformation("Collecting from Reddit: {Url}", searchUrl);

            var response = await GetJsonAsync<RedditResponse>(searchUrl, cancellationToken);

            if (response?.Data?.Children == null)
            {
                Logger.LogWarning("No results from Reddit for keyword: {Keyword}", keyword);
                return articles;
            }

            foreach (var child in response.Data.Children)
            {
                var post = child.Data;
                if (post == null || string.IsNullOrEmpty(post.Title))
                    continue;

                // Filter by date if since is provided
                if (since.HasValue)
                {
                    var postDate = DateTimeOffset.FromUnixTimeSeconds((long)post.CreatedUtc).UtcDateTime;
                    if (postDate < since.Value)
                        continue;
                }

                // Use the Reddit URL if no external URL is provided
                var url = string.IsNullOrEmpty(post.Url) || post.Url.Contains("reddit.com")
                    ? $"https://www.reddit.com{post.Permalink}"
                    : post.Url;

                var article = new Article
                {
                    SourceId = source.Id,
                    Title = post.Title,
                    Url = url,
                    Summary = post.Selftext?.Length > 500
                        ? post.Selftext[..500] + "..."
                        : post.Selftext,
                    NativeScore = post.Score,
                    PublishedAt = DateTimeOffset.FromUnixTimeSeconds((long)post.CreatedUtc).UtcDateTime,
                    CollectedAt = DateTime.UtcNow
                };

                articles.Add(article);
            }

            Logger.LogInformation("Collected {Count} articles from Reddit", articles.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error collecting from Reddit");
        }

        return articles;
    }

    private class RedditResponse
    {
        [JsonPropertyName("data")]
        public RedditData? Data { get; set; }
    }

    private class RedditData
    {
        [JsonPropertyName("children")]
        public List<RedditChild>? Children { get; set; }
    }

    private class RedditChild
    {
        [JsonPropertyName("data")]
        public RedditPost? Data { get; set; }
    }

    private class RedditPost
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("permalink")]
        public string? Permalink { get; set; }

        [JsonPropertyName("selftext")]
        public string? Selftext { get; set; }

        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("created_utc")]
        public double CreatedUtc { get; set; }

        [JsonPropertyName("subreddit")]
        public string? Subreddit { get; set; }
    }
}

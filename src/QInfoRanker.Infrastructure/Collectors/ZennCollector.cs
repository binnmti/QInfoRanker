using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using QInfoRanker.Core.Entities;

namespace QInfoRanker.Infrastructure.Collectors;

public class ZennCollector : BaseCollector
{
    public ZennCollector(HttpClient httpClient, ILogger<ZennCollector> logger)
        : base(httpClient, logger)
    {
    }

    public override string SourceName => "Zenn";

    public override bool CanHandle(Source source)
    {
        return source.Name.Contains("Zenn", StringComparison.OrdinalIgnoreCase) ||
               source.Url.Contains("zenn.dev", StringComparison.OrdinalIgnoreCase);
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
            Logger.LogInformation("Collecting from Zenn: {Url}", searchUrl);

            // Zennの検索APIはトピックベースの場合もある
            var response = await GetJsonAsync<ZennResponse>(searchUrl, cancellationToken);

            // レスポンスが空の場合、トピック検索を試行
            if (response?.Articles == null || response.Articles.Count == 0)
            {
                Logger.LogInformation("Trying Zenn topic search for keyword: {Keyword}", keyword);
                var topicUrl = $"https://zenn.dev/api/articles?topicname={Uri.EscapeDataString(keyword)}&order=latest";
                response = await GetJsonAsync<ZennResponse>(topicUrl, cancellationToken);
            }

            if (response?.Articles == null || response.Articles.Count == 0)
            {
                Logger.LogWarning("No results from Zenn for keyword: {Keyword}", keyword);
                return articles;
            }

            foreach (var item in response.Articles)
            {
                if (string.IsNullOrEmpty(item.Title) || string.IsNullOrEmpty(item.Slug))
                    continue;

                // Filter by date if since is provided
                if (since.HasValue && item.PublishedAt.HasValue)
                {
                    if (item.PublishedAt.Value < since.Value)
                        continue;
                }

                var articleUrl = $"https://zenn.dev/{item.User?.Username}/articles/{item.Slug}";

                // Zenn APIのリストエンドポイントは本文を返さないため、タイトルのみ使用
                // body_letters_countは本文の文字数（整数）であり、本文そのものではない
                var article = new Article
                {
                    SourceId = source.Id,
                    Title = item.Title,
                    Url = articleUrl,
                    Summary = $"{item.Emoji} {item.ArticleType ?? "tech"} - {item.BodyLettersCount?.ToString("N0") ?? "?"} 文字",
                    NativeScore = item.LikedCount,
                    PublishedAt = item.PublishedAt,
                    CollectedAt = DateTime.UtcNow
                };

                articles.Add(article);
            }

            Logger.LogInformation("Collected {Count} articles from Zenn", articles.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error collecting from Zenn");
        }

        return articles;
    }

    private class ZennResponse
    {
        [JsonPropertyName("articles")]
        public List<ZennArticle>? Articles { get; set; }
    }

    private class ZennArticle
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("slug")]
        public string? Slug { get; set; }

        [JsonPropertyName("body_letters_count")]
        public int? BodyLettersCount { get; set; }

        [JsonPropertyName("emoji")]
        public string? Emoji { get; set; }

        [JsonPropertyName("article_type")]
        public string? ArticleType { get; set; }

        [JsonPropertyName("liked_count")]
        public int LikedCount { get; set; }

        [JsonPropertyName("published_at")]
        public DateTime? PublishedAt { get; set; }

        [JsonPropertyName("user")]
        public ZennUser? User { get; set; }
    }

    private class ZennUser
    {
        [JsonPropertyName("username")]
        public string? Username { get; set; }
    }
}

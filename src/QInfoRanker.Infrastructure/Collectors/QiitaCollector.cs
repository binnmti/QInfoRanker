using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using QInfoRanker.Core.Entities;

namespace QInfoRanker.Infrastructure.Collectors;

public class QiitaCollector : BaseCollector
{
    public QiitaCollector(HttpClient httpClient, ILogger<QiitaCollector> logger)
        : base(httpClient, logger)
    {
    }

    public override string SourceName => "Qiita";

    public override bool CanHandle(Source source)
    {
        return source.Name.Contains("Qiita", StringComparison.OrdinalIgnoreCase) ||
               source.Url.Contains("qiita.com", StringComparison.OrdinalIgnoreCase);
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
            Logger.LogInformation("Collecting from Qiita: {Url}", searchUrl);

            var response = await GetJsonAsync<List<QiitaItem>>(searchUrl, cancellationToken);

            if (response == null || response.Count == 0)
            {
                Logger.LogWarning("No results from Qiita for keyword: {Keyword}", keyword);
                return articles;
            }

            foreach (var item in response)
            {
                if (string.IsNullOrEmpty(item.Title) || string.IsNullOrEmpty(item.Url))
                    continue;

                // Filter by date if since is provided
                if (since.HasValue && item.CreatedAt.HasValue)
                {
                    if (item.CreatedAt.Value < since.Value)
                        continue;
                }

                var article = new Article
                {
                    SourceId = source.Id,
                    KeywordId = source.KeywordId ?? 0,
                    Title = item.Title,
                    Url = item.Url,
                    Summary = item.Body?.Length > 500 ? item.Body[..500] + "..." : item.Body,
                    Content = item.Body, // 全文を保存（要約生成用）
                    NativeScore = item.LikesCount,
                    PublishedAt = item.CreatedAt,
                    CollectedAt = DateTime.UtcNow
                };

                articles.Add(article);
            }

            Logger.LogInformation("Collected {Count} articles from Qiita", articles.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error collecting from Qiita");
        }

        return articles;
    }

    private class QiitaItem
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("likes_count")]
        public int LikesCount { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}

using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using QInfoRanker.Core.Entities;

namespace QInfoRanker.Infrastructure.Collectors;

public class NoteCollector : BaseCollector
{
    public NoteCollector(HttpClient httpClient, ILogger<NoteCollector> logger)
        : base(httpClient, logger)
    {
    }

    public override string SourceName => "Note.com";

    public override bool CanHandle(Source source)
    {
        return source.Name.Contains("Note", StringComparison.OrdinalIgnoreCase) ||
               source.Url.Contains("note.com", StringComparison.OrdinalIgnoreCase);
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
            Logger.LogInformation("Collecting from Note.com for keyword: {Keyword}", keyword);

            // Try API first, fallback to RSS
            articles = await CollectFromApiAsync(source, keyword, since, cancellationToken);

            if (articles.Count == 0)
            {
                Logger.LogInformation("API returned no results, trying RSS for Note.com");
                articles = await CollectFromRssAsync(source, keyword, since, cancellationToken);
            }

            Logger.LogInformation("Collected {Count} articles from Note.com", articles.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error collecting from Note.com");
        }

        return articles;
    }

    private async Task<List<Article>> CollectFromApiAsync(
        Source source,
        string keyword,
        DateTime? since,
        CancellationToken cancellationToken)
    {
        var articles = new List<Article>();

        // v3 API endpoint (v2 was deprecated)
        var apiUrl = $"https://note.com/api/v3/searches?context=note&q={Uri.EscapeDataString(keyword)}&size=50&sort=new";
        var jsonContent = await GetStringAsync(apiUrl, cancellationToken);

        if (string.IsNullOrEmpty(jsonContent))
            return articles;

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            // v3 API structure: data.notes.contents[]
            if (!root.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("notes", out var notes) ||
                !notes.TryGetProperty("contents", out var contents))
            {
                return articles;
            }

            foreach (var note in contents.EnumerateArray())
            {
                var id = note.TryGetProperty("id", out var idProp) ? idProp.GetInt64().ToString() : null;
                var key = note.TryGetProperty("key", out var keyProp) ? keyProp.GetString() : null;
                var name = note.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                var body = note.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : null;
                // v3 API uses snake_case
                var likeCount = note.TryGetProperty("like_count", out var likeProp) ? likeProp.GetInt32() : 0;
                var publishAt = note.TryGetProperty("publish_at", out var pubProp) ? pubProp.GetString() : null;

                // Get user info for URL
                var userUrlname = "";
                if (note.TryGetProperty("user", out var user) &&
                    user.TryGetProperty("urlname", out var urlnameProp))
                {
                    userUrlname = urlnameProp.GetString() ?? "";
                }

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(key))
                    continue;

                DateTime? publishedDate = null;
                if (!string.IsNullOrEmpty(publishAt) && DateTime.TryParse(publishAt, out var parsedDate))
                {
                    publishedDate = parsedDate;
                }

                // Filter by date if since is provided
                if (since.HasValue && publishedDate.HasValue && publishedDate < since.Value)
                    continue;

                var url = !string.IsNullOrEmpty(userUrlname)
                    ? $"https://note.com/{userUrlname}/n/{key}"
                    : $"https://note.com/n/{key}";

                var article = new Article
                {
                    SourceId = source.Id,
                    Title = name,
                    Url = url,
                    Summary = body?.Length > 500 ? body[..500] + "..." : body,
                    Content = body, // 全文を保存（要約生成用）
                    NativeScore = likeCount,
                    PublishedAt = publishedDate,
                    CollectedAt = DateTime.UtcNow
                };

                articles.Add(article);
            }
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Failed to parse Note.com API response");
        }

        return articles;
    }

    private async Task<List<Article>> CollectFromRssAsync(
        Source source,
        string keyword,
        DateTime? since,
        CancellationToken cancellationToken)
    {
        var articles = new List<Article>();

        var rssUrl = BuildSearchUrl(source, keyword);
        var rssContent = await GetStringAsync(rssUrl, cancellationToken);

        if (string.IsNullOrEmpty(rssContent))
            return articles;

        try
        {
            var doc = XDocument.Parse(rssContent);
            var items = doc.Descendants("item");

            foreach (var item in items)
            {
                var title = item.Element("title")?.Value?.Trim();
                var link = item.Element("link")?.Value?.Trim();
                var description = item.Element("description")?.Value?.Trim();
                var pubDateStr = item.Element("pubDate")?.Value;

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(link))
                    continue;

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
                    NativeScore = null,
                    PublishedAt = publishedDate,
                    CollectedAt = DateTime.UtcNow
                };

                articles.Add(article);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to parse Note.com RSS");
        }

        return articles;
    }
}

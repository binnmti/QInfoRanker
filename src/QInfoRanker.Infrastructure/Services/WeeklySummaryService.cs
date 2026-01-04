using System.ClientModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.OpenAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using QInfoRanker.Core.Entities;
using QInfoRanker.Core.Interfaces.Services;
using QInfoRanker.Infrastructure.Data;
using QInfoRanker.Infrastructure.Scoring;

namespace QInfoRanker.Infrastructure.Services;

public class WeeklySummaryService : IWeeklySummaryService
{
    private readonly AppDbContext _context;
    private readonly AzureOpenAIOptions _openAIOptions;
    private readonly ILogger<WeeklySummaryService> _logger;
    private readonly AzureOpenAIClient? _client;
    private readonly ChatClient? _chatClient;

    private const int MinArticlesForSummary = 3;
    private const int MaxArticlesForSummary = 15;

    public WeeklySummaryService(
        AppDbContext context,
        IOptions<AzureOpenAIOptions> openAIOptions,
        ILogger<WeeklySummaryService> logger)
    {
        _context = context;
        _openAIOptions = openAIOptions.Value;
        _logger = logger;

        if (!string.IsNullOrEmpty(_openAIOptions.Endpoint) && !string.IsNullOrEmpty(_openAIOptions.ApiKey))
        {
            _client = new AzureOpenAIClient(
                new Uri(_openAIOptions.Endpoint),
                new ApiKeyCredential(_openAIOptions.ApiKey));
            _chatClient = _client.GetChatClient(_openAIOptions.DeploymentName);
        }
    }

    public async Task<WeeklySummary?> GetCurrentWeekSummaryAsync(int keywordId, CancellationToken cancellationToken = default)
    {
        var (weekStart, weekEnd) = GetCurrentWeekRange();

        return await _context.WeeklySummaries
            .Include(w => w.Keyword)
            .Where(w => w.KeywordId == keywordId && w.WeekStart == weekStart)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<WeeklySummary>> GetSummariesAsync(int? keywordId = null, int take = 10, CancellationToken cancellationToken = default)
    {
        var query = _context.WeeklySummaries
            .Include(w => w.Keyword)
            .AsQueryable();

        if (keywordId.HasValue)
        {
            query = query.Where(w => w.KeywordId == keywordId.Value);
        }

        return await query
            .OrderByDescending(w => w.WeekStart)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<WeeklySummary?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.WeeklySummaries
            .Include(w => w.Keyword)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<WeeklySummary?> GenerateSummaryIfNeededAsync(int keywordId, CancellationToken cancellationToken = default)
    {
        var existing = await GetCurrentWeekSummaryAsync(keywordId, cancellationToken);
        if (existing != null)
        {
            _logger.LogInformation("Weekly summary already exists for keyword {KeywordId} this week", keywordId);
            return null;
        }

        try
        {
            return await GenerateSummaryAsync(keywordId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate weekly summary for keyword {KeywordId}", keywordId);
            return null;
        }
    }

    public async Task<WeeklySummary> GenerateSummaryAsync(int keywordId, CancellationToken cancellationToken = default)
    {
        var keyword = await _context.Keywords.FindAsync(new object[] { keywordId }, cancellationToken)
            ?? throw new InvalidOperationException($"Keyword {keywordId} not found");

        var (weekStart, weekEnd) = GetCurrentWeekRange();

        var articles = await _context.Articles
            .Where(a => a.KeywordId == keywordId)
            .Where(a => a.CollectedAt >= weekStart && a.CollectedAt <= weekEnd)
            .Where(a => a.IsRelevant == true)
            .OrderByDescending(a => a.FinalScore)
            .Take(MaxArticlesForSummary)
            .ToListAsync(cancellationToken);

        if (articles.Count < MinArticlesForSummary)
        {
            throw new InvalidOperationException(
                $"Not enough articles to generate summary. Found {articles.Count}, need at least {MinArticlesForSummary}.");
        }

        var (title, content) = await GenerateSummaryContentAsync(keyword.Term, articles, cancellationToken);

        var existing = await _context.WeeklySummaries
            .FirstOrDefaultAsync(w => w.KeywordId == keywordId && w.WeekStart == weekStart, cancellationToken);

        if (existing != null)
        {
            existing.Title = title;
            existing.Content = content;
            existing.ArticleCount = articles.Count;
            existing.GeneratedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated weekly summary for keyword '{Keyword}' with {Count} articles",
                keyword.Term, articles.Count);

            return existing;
        }

        var summary = new WeeklySummary
        {
            KeywordId = keywordId,
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            Title = title,
            Content = content,
            ArticleCount = articles.Count,
            GeneratedAt = DateTime.UtcNow
        };

        _context.WeeklySummaries.Add(summary);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Generated weekly summary for keyword '{Keyword}' with {Count} articles",
            keyword.Term, articles.Count);

        return summary;
    }

    private async Task<(string Title, string Content)> GenerateSummaryContentAsync(
        string keywordTerm,
        List<Article> articles,
        CancellationToken cancellationToken)
    {
        if (_chatClient == null)
        {
            _logger.LogWarning("Azure OpenAI not configured. Using fallback summary.");
            return GenerateFallbackSummary(keywordTerm, articles);
        }

        var articleList = new StringBuilder();
        for (var i = 0; i < articles.Count; i++)
        {
            var article = articles[i];
            var summary = !string.IsNullOrEmpty(article.SummaryJa) ? article.SummaryJa : article.Summary;
            articleList.AppendLine($"{i + 1}. 【{article.Title}】");
            articleList.AppendLine($"   要約: {summary}");
            articleList.AppendLine($"   スコア: {article.FinalScore:F1} / ソース: {article.Source?.Name ?? "不明"}");
            articleList.AppendLine();
        }

        var (weekStart, weekEnd) = GetCurrentWeekRange();
        var prompt = $$"""
            あなたは優秀なテクノロジーニュースライターです。
            以下の記事情報を元に、「{{keywordTerm}}」に関する今週（{{weekStart:M/d}}〜{{weekEnd:M/d}}）のニュース記事を日本語で書いてください。

            【収集した記事一覧】
            {{articleList}}

            【出力形式】
            以下のJSON形式で回答してください：
            {
              "title": "キャッチーな見出し（30文字以内）",
              "content": "Markdown形式の本文"
            }

            【本文の構成】
            1. **導入**（1-2段落）: 今週の{{keywordTerm}}に関する動向を簡潔に紹介
            2. **今週のハイライト**（3-5トピック）: 重要なトピックを見出し付きで詳しく解説。各トピックは記事の内容を元に書く
            3. **まとめ**（1段落）: 今週の総括

            【注意事項】
            - 記事の内容を元に、読み応えのあるニュース記事を書いてください
            - 技術用語は適度に使いつつ、分かりやすい表現を心がけてください
            - 各ハイライトには該当する記事タイトルを参照として含めてください
            """;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("あなたはテクノロジーニュースライターです。JSON形式でのみ回答してください。"),
            new UserChatMessage(prompt)
        };

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = 2000,
            Temperature = 0.7f
        };

        try
        {
            var response = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
            var responseContent = response.Value.Content[0].Text;

            var parsed = ParseSummaryResponse(responseContent);
            if (parsed != null)
            {
                return (parsed.Title, parsed.Content);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate summary with Azure OpenAI");
        }

        return GenerateFallbackSummary(keywordTerm, articles);
    }

    private SummaryResponse? ParseSummaryResponse(string content)
    {
        try
        {
            content = CleanJsonResponse(content);
            return JsonSerializer.Deserialize<SummaryResponse>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse summary response: {Content}", content);
            return null;
        }
    }

    private static string CleanJsonResponse(string content)
    {
        content = content.Trim();
        if (content.StartsWith("```"))
        {
            var lines = content.Split('\n');
            content = string.Join('\n', lines.Skip(1).Take(lines.Length - 2));
            if (content.EndsWith("```"))
                content = content[..content.LastIndexOf("```")];
        }
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start >= 0 && end > start)
            content = content[start..(end + 1)];
        return content.Trim();
    }

    private static (string Title, string Content) GenerateFallbackSummary(string keywordTerm, List<Article> articles)
    {
        var title = $"{keywordTerm}：今週のまとめ";
        var sb = new StringBuilder();
        sb.AppendLine($"## {keywordTerm} 今週のニュース");
        sb.AppendLine();
        sb.AppendLine($"今週は{articles.Count}件の関連記事が収集されました。");
        sb.AppendLine();
        sb.AppendLine("### 注目の記事");
        sb.AppendLine();

        foreach (var article in articles.Take(5))
        {
            var summary = !string.IsNullOrEmpty(article.SummaryJa) ? article.SummaryJa : article.Summary;
            sb.AppendLine($"- **{article.Title}**");
            sb.AppendLine($"  - {summary}");
            sb.AppendLine();
        }

        return (title, sb.ToString());
    }

    private static (DateTime WeekStart, DateTime WeekEnd) GetCurrentWeekRange()
    {
        var today = DateTime.UtcNow.Date;
        var culture = new CultureInfo("ja-JP");
        var diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        var weekStart = today.AddDays(-diff);
        var weekEnd = weekStart.AddDays(6);
        return (weekStart, weekEnd);
    }

    private class SummaryResponse
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}

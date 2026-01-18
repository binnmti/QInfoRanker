using System.ClientModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using QInfoRanker.Core.Entities;
using QInfoRanker.Core.Enums;
using QInfoRanker.Core.Interfaces.Services;
using QInfoRanker.Infrastructure.Data;
using QInfoRanker.Infrastructure.Scoring;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only

namespace QInfoRanker.Infrastructure.Services;

public class WeeklySummaryService : IWeeklySummaryService
{
    private readonly AppDbContext _context;
    private readonly AzureOpenAIOptions _openAIOptions;
    private readonly WeeklySummaryOptions _summaryOptions;
    private readonly ILogger<WeeklySummaryService> _logger;
    private readonly ChatClient? _chatClient;

    private const int MinArticlesForSummary = 3;
    private const int MaxArticlesForSummary = 15;

    public WeeklySummaryService(
        AppDbContext context,
        IOptions<AzureOpenAIOptions> openAIOptions,
        IOptions<WeeklySummaryOptions> summaryOptions,
        ILogger<WeeklySummaryService> logger)
    {
        _context = context;
        _openAIOptions = openAIOptions.Value;
        _summaryOptions = summaryOptions.Value;
        _logger = logger;

        if (!string.IsNullOrEmpty(_openAIOptions.Endpoint) && !string.IsNullOrEmpty(_openAIOptions.ApiKey))
        {
            var baseEndpoint = _openAIOptions.Endpoint.TrimEnd('/');
            var v1Endpoint = new Uri($"{baseEndpoint}/openai/v1");
            var credential = new ApiKeyCredential(_openAIOptions.ApiKey);

            var clientOptions = new OpenAIClientOptions
            {
                Endpoint = v1Endpoint
            };

            var openAIClient = new OpenAIClient(credential, clientOptions);
            _chatClient = openAIClient.GetChatClient(_summaryOptions.DeploymentName);
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

        // カテゴリ別に記事を取得
        var categoryArticles = await GetArticlesByCategoryAsync(keywordId, weekStart, weekEnd, cancellationToken);
        var totalCount = categoryArticles.News.Count + categoryArticles.Tech.Count + categoryArticles.Academic.Count;

        if (totalCount < MinArticlesForSummary)
        {
            throw new InvalidOperationException(
                $"Not enough articles to generate summary. Found {totalCount}, need at least {MinArticlesForSummary}.");
        }

        var (title, content) = await GenerateSummaryContentAsync(keyword.Term, categoryArticles, cancellationToken);

        // 履歴として保持するため、常に新しい要約を作成
        var summary = new WeeklySummary
        {
            KeywordId = keywordId,
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            Title = title,
            Content = content,
            ArticleCount = totalCount,
            GeneratedAt = DateTime.UtcNow
        };

        _context.WeeklySummaries.Add(summary);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Generated weekly summary for keyword '{Keyword}' with {Count} articles",
            keyword.Term, totalCount);

        return summary;
    }

    private async Task<CategoryArticles> GetArticlesByCategoryAsync(
        int keywordId,
        DateTime weekStart,
        DateTime weekEnd,
        CancellationToken cancellationToken)
    {
        // ニュース: PublishedAtが今週
        var newsArticles = await _context.Articles
            .Include(a => a.Source)
            .Where(a => a.KeywordId == keywordId)
            .Where(a => a.Source.Category == SourceCategory.News)
            .Where(a => a.IsRelevant == true && a.LlmScore.HasValue)
            .Where(a => a.PublishedAt.HasValue && a.PublishedAt.Value >= weekStart && a.PublishedAt.Value <= weekEnd)
            .OrderByDescending(a => a.FinalScore)
            .Take(10)
            .ToListAsync(cancellationToken);

        // 技術記事: CollectedAtが今週
        var techArticles = await _context.Articles
            .Include(a => a.Source)
            .Where(a => a.KeywordId == keywordId)
            .Where(a => a.Source.Category == SourceCategory.Technology)
            .Where(a => a.IsRelevant == true && a.LlmScore.HasValue)
            .Where(a => a.CollectedAt >= weekStart && a.CollectedAt <= weekEnd)
            .OrderByDescending(a => a.FinalScore)
            .Take(10)
            .ToListAsync(cancellationToken);

        // 研究: CollectedAtが今週
        var academicArticles = await _context.Articles
            .Include(a => a.Source)
            .Where(a => a.KeywordId == keywordId)
            .Where(a => a.Source.Category == SourceCategory.Academic)
            .Where(a => a.IsRelevant == true && a.LlmScore.HasValue)
            .Where(a => a.CollectedAt >= weekStart && a.CollectedAt <= weekEnd)
            .OrderByDescending(a => a.FinalScore)
            .Take(10)
            .ToListAsync(cancellationToken);

        return new CategoryArticles(newsArticles, techArticles, academicArticles);
    }

    private record CategoryArticles(List<Article> News, List<Article> Tech, List<Article> Academic);

    private async Task<(string Title, string Content)> GenerateSummaryContentAsync(
        string keywordTerm,
        CategoryArticles categoryArticles,
        CancellationToken cancellationToken)
    {
        if (_chatClient == null)
        {
            _logger.LogWarning("Azure OpenAI not configured. Using fallback summary.");
            return GenerateFallbackSummary(keywordTerm, categoryArticles);
        }

        var articleInfo = new StringBuilder();

        // ニュース
        if (categoryArticles.News.Any())
        {
            articleInfo.AppendLine("【ニュース】");
            foreach (var article in categoryArticles.News)
            {
                var summary = !string.IsNullOrEmpty(article.SummaryJa) ? article.SummaryJa : article.Summary;
                articleInfo.AppendLine($"- タイトル: {article.Title}");
                articleInfo.AppendLine($"  URL: {article.Url}");
                articleInfo.AppendLine($"  要約: {summary}");
                articleInfo.AppendLine();
            }
        }

        // 技術記事
        if (categoryArticles.Tech.Any())
        {
            articleInfo.AppendLine("【技術記事】");
            foreach (var article in categoryArticles.Tech)
            {
                var summary = !string.IsNullOrEmpty(article.SummaryJa) ? article.SummaryJa : article.Summary;
                articleInfo.AppendLine($"- タイトル: {article.Title}");
                articleInfo.AppendLine($"  URL: {article.Url}");
                articleInfo.AppendLine($"  要約: {summary}");
                articleInfo.AppendLine();
            }
        }

        // 研究
        if (categoryArticles.Academic.Any())
        {
            articleInfo.AppendLine("【研究・論文】");
            foreach (var article in categoryArticles.Academic)
            {
                var summary = !string.IsNullOrEmpty(article.SummaryJa) ? article.SummaryJa : article.Summary;
                articleInfo.AppendLine($"- タイトル: {article.Title}");
                articleInfo.AppendLine($"  URL: {article.Url}");
                articleInfo.AppendLine($"  要約: {summary}");
                articleInfo.AppendLine();
            }
        }

        var (weekStart, weekEnd) = GetCurrentWeekRange();
        var prompt = $$"""
            あなたは優秀なテクノロジーアナリストです。
            以下の記事情報を元に、「{{keywordTerm}}」に関する今週（{{weekStart:M/d}}〜{{weekEnd:M/d}}）の動向を日本語でまとめてください。

            【今週の記事（カテゴリ別）】
            {{articleInfo}}

            【出力形式】
            以下のJSON形式で回答してください：
            {
              "title": "今週の動向を表す具体的な見出し（25文字以内）",
              "content": "Markdown形式の本文（必ずリンクを含む）"
            }

            ★★★ 最重要：Markdownリンクについて ★★★
            本文には必ず5個以上のMarkdownリンクを含めてください。
            - 形式: [事実・出来事の説明](記事URL)
            - 事実そのものをリンクテキストにする
            - 良い例: [IBMが新しい量子プロセッサを発表し史上最高のコヒレンス時間を達成](https://example.com/news)したことが報じられた。
            - 悪い例: IBMが発表した。こちらの記事で詳しく述べられている。← リンクなしはNG
            - 悪い例: [こちらの記事](URL)で詳しく ← メタ的表現はNG

            【本文の要件】
            - 600〜800文字程度
            - 具体的な企業名、製品名、技術名、数値を含める
            - 「今週起きた具体的な出来事」だけを書く。将来の展望や予測は書かない
            - 禁止表現：「期待が高まっている」「注目を集めている」「今後も〜」「可能性を示している」
            - 事実ベース：「〜が〜を発表した」「〜で〜が実現した」「〜が〜%向上した」
            - 箇条書きや見出しは使わず、流れるような文章で書く

            """;

        var systemPrompt = "あなたはテクノロジーニュースライターです。JSON形式でのみ回答してください。本文には必ず複数のMarkdownリンク[テキスト](URL)を含めてください。";
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(prompt)
        };

        var options = new ChatCompletionOptions();

        // 推論モデルでなければ Temperature を設定
        if (!ModelCapabilities.IsReasoningModel(_summaryOptions.DeploymentName))
        {
            options.Temperature = 0.5f;
        }
        else
        {
            options.ReasoningEffortLevel = ChatReasoningEffortLevel.Low;
        }

        try
        {
            var response = await _chatClient.CompleteChatAsync(
                messages: messages,
                options: options,
                cancellationToken: cancellationToken);

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

        return GenerateFallbackSummary(keywordTerm, categoryArticles);
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

    private static (string Title, string Content) GenerateFallbackSummary(string keywordTerm, CategoryArticles categoryArticles)
    {
        var totalCount = categoryArticles.News.Count + categoryArticles.Tech.Count + categoryArticles.Academic.Count;
        var title = $"{keywordTerm}：今週の動向";
        var sb = new StringBuilder();
        sb.AppendLine($"今週は{totalCount}件の関連記事が収集されました。");
        sb.AppendLine();

        if (categoryArticles.News.Any())
        {
            sb.AppendLine($"ニュース分野では{categoryArticles.News.Count}件の記事があり、最新の動向が報じられています。");
        }
        if (categoryArticles.Tech.Any())
        {
            sb.AppendLine($"技術記事では{categoryArticles.Tech.Count}件の投稿があり、実践的な知見が共有されています。");
        }
        if (categoryArticles.Academic.Any())
        {
            sb.AppendLine($"研究分野では{categoryArticles.Academic.Count}件の論文が注目を集めています。");
        }

        return (title, sb.ToString());
    }

    public async Task<int> DeleteByKeywordAsync(int keywordId, CancellationToken cancellationToken = default)
    {
        var summaries = await _context.WeeklySummaries
            .Where(w => w.KeywordId == keywordId)
            .ToListAsync(cancellationToken);

        if (summaries.Count == 0)
            return 0;

        _context.WeeklySummaries.RemoveRange(summaries);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} weekly summaries for keyword {KeywordId}", summaries.Count, keywordId);
        return summaries.Count;
    }

    public async Task<SummaryDiagnostics> GetDiagnosticsAsync(int keywordId, CancellationToken cancellationToken = default)
    {
        var (weekStart, weekEnd) = GetCurrentWeekRange();
        var isConfigured = _chatClient != null;
        var deploymentName = isConfigured ? _summaryOptions.DeploymentName : null;

        // カテゴリ別に記事数をカウント
        var categoryArticles = await GetArticlesByCategoryAsync(keywordId, weekStart, weekEnd, cancellationToken);
        var totalCount = categoryArticles.News.Count + categoryArticles.Tech.Count + categoryArticles.Academic.Count;

        // ブロック理由を判定
        string? blockingReason = null;
        var canGenerate = true;

        if (!isConfigured)
        {
            canGenerate = false;
            blockingReason = "Azure OpenAI が設定されていません（Endpoint または ApiKey が未設定）";
        }
        else if (totalCount < MinArticlesForSummary)
        {
            canGenerate = false;
            blockingReason = $"対象記事が不足しています（{totalCount}件 / 必要{MinArticlesForSummary}件以上）。今週スコアリング済み（IsRelevant=true, LlmScore設定済み）の記事を収集してください。";
        }

        return new SummaryDiagnostics(
            IsAzureOpenAIConfigured: isConfigured,
            DeploymentName: deploymentName,
            NewsArticleCount: categoryArticles.News.Count,
            TechArticleCount: categoryArticles.Tech.Count,
            AcademicArticleCount: categoryArticles.Academic.Count,
            TotalArticleCount: totalCount,
            MinRequiredArticles: MinArticlesForSummary,
            CanGenerateSummary: canGenerate,
            BlockingReason: blockingReason
        );
    }

    public async Task<IReadOnlyList<WeeklySummary>> GetHistoryAsync(int keywordId, int take = 30, CancellationToken cancellationToken = default)
    {
        return await _context.WeeklySummaries
            .Include(w => w.Keyword)
            .Where(w => w.KeywordId == keywordId)
            .OrderByDescending(w => w.GeneratedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<WeeklySummary?> GetPreviousSummaryAsync(int keywordId, DateTime currentGeneratedAt, CancellationToken cancellationToken = default)
    {
        return await _context.WeeklySummaries
            .Include(w => w.Keyword)
            .Where(w => w.KeywordId == keywordId && w.GeneratedAt < currentGeneratedAt)
            .OrderByDescending(w => w.GeneratedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<WeeklySummary?> GetNextSummaryAsync(int keywordId, DateTime currentGeneratedAt, CancellationToken cancellationToken = default)
    {
        return await _context.WeeklySummaries
            .Include(w => w.Keyword)
            .Where(w => w.KeywordId == keywordId && w.GeneratedAt > currentGeneratedAt)
            .OrderBy(w => w.GeneratedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<WeeklySummary?> GetLatestSummaryAsync(int keywordId, CancellationToken cancellationToken = default)
    {
        return await _context.WeeklySummaries
            .Include(w => w.Keyword)
            .Where(w => w.KeywordId == keywordId)
            .OrderByDescending(w => w.GeneratedAt)
            .FirstOrDefaultAsync(cancellationToken);
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

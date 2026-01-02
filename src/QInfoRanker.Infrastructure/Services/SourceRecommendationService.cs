using System.ClientModel;
using System.Text.Json;
using Azure.AI.OpenAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using QInfoRanker.Core.Entities;
using QInfoRanker.Core.Enums;
using QInfoRanker.Core.Interfaces.Services;
using QInfoRanker.Infrastructure.Data;
using QInfoRanker.Infrastructure.Scoring;

namespace QInfoRanker.Infrastructure.Services;

public class SourceRecommendationService : ISourceRecommendationService
{
    private readonly AppDbContext _context;
    private readonly AzureOpenAIOptions _openAIOptions;
    private readonly ILogger<SourceRecommendationService> _logger;
    private readonly AzureOpenAIClient? _client;
    private readonly ChatClient? _chatClient;

    public SourceRecommendationService(
        AppDbContext context,
        IOptions<AzureOpenAIOptions> openAIOptions,
        ILogger<SourceRecommendationService> logger)
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

    public async Task<SourceRecommendationResult> RecommendSourcesAsync(string keyword, CancellationToken cancellationToken = default)
    {
        var result = new SourceRecommendationResult();
        var templateSources = await _context.Sources
            .Where(s => s.IsTemplate)
            .ToListAsync(cancellationToken);

        if (_chatClient == null)
        {
            _logger.LogWarning("Azure OpenAI not configured. Returning all template sources.");
            foreach (var template in templateSources)
            {
                template.RecommendationReason = "AI未設定のためデフォルト選択";
            }
            result.RecommendedSources = templateSources;
            return result;
        }

        try
        {
            var recommendation = await GetAIRecommendationAsync(keyword, templateSources, cancellationToken);

            if (recommendation != null)
            {
                // キーワード分析結果を設定
                if (recommendation.KeywordAnalysis != null)
                {
                    result.KeywordAnalysis = recommendation.KeywordAnalysis.Reasoning;
                    result.DetectedLanguage = recommendation.KeywordAnalysis.DetectedLanguage;
                    result.DetectedCategory = recommendation.KeywordAnalysis.Category;
                }

                // 推薦されたソースと理由を設定
                foreach (var rec in recommendation.Sources)
                {
                    var source = templateSources.FirstOrDefault(s =>
                        s.Name.Equals(rec.Name, StringComparison.OrdinalIgnoreCase));
                    if (source != null && rec.Recommended)
                    {
                        source.RecommendationReason = rec.Reason;
                        result.RecommendedSources.Add(source);
                    }
                }

                _logger.LogInformation(
                    "AI recommended {Count}/{Total} sources for keyword '{Keyword}': {Sources}",
                    result.RecommendedSources.Count,
                    templateSources.Count,
                    keyword,
                    string.Join(", ", result.RecommendedSources.Select(s => s.Name)));

                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get AI recommendation for keyword '{Keyword}'. Returning all sources.", keyword);
        }

        // フォールバック: 全テンプレートを返す
        foreach (var template in templateSources)
        {
            template.RecommendationReason = "AI推薦失敗のためデフォルト選択";
        }
        result.RecommendedSources = templateSources;
        return result;
    }

    private async Task<SourceRecommendation?> GetAIRecommendationAsync(
        string keyword,
        List<Source> templateSources,
        CancellationToken cancellationToken)
    {
        var sourcesInfo = templateSources.Select(s => new
        {
            name = s.Name,
            language = s.Language.ToString(),
            category = s.Category.ToString(),
            description = GetSourceDescription(s.Name)
        });

        var sourcesJson = JsonSerializer.Serialize(sourcesInfo, new JsonSerializerOptions { WriteIndented = false });

        var prompt = $$"""
            キーワード「{{keyword}}」に対して、最も関連性の高い情報ソースを推薦してください。

            利用可能なソース:
            {{sourcesJson}}

            考慮事項:
            1. 言語の一致 - 日本語キーワードには日本語ソース（Hatena, Qiita, Zenn）を含める
            2. トピックの関連性 - 技術トピックには技術系ソース、学術トピックにはarXivが必要
            3. ソースの強み - 各ソースには異なる強みがある

            ソースの説明:
            - Hatena Bookmark: 日本のソーシャルブックマーク、トレンドの日本語技術記事に強い
            - Qiita: 日本の開発者コミュニティ、プログラミングチュートリアルやTipsに優れている
            - Zenn: 日本の技術ブログプラットフォーム、質の高い技術記事
            - arXiv: 学術論文、最先端の研究トピックに必須
            - Hacker News: 英語の技術ニュース、スタートアップやプログラミングの議論
            - Reddit: 様々なトピックの英語コミュニティ、議論に適している

            JSON形式でのみ回答（日本語で）:
            {
              "keyword_analysis": {
                "detected_language": "japanese|english|both",
                "category": "technology|science|business|social|other",
                "reasoning": "簡潔な理由（日本語）"
              },
              "sources": [
                {"name": "ソース名", "recommended": true/false, "reason": "推薦理由（日本語）"}
              ]
            }
            """;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("あなたはキーワードに適した情報ソースを推薦する専門家です。JSON形式でのみ回答してください。"),
            new UserChatMessage(prompt)
        };

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = 1000,
            Temperature = 0.3f
        };

        var response = await _chatClient!.CompleteChatAsync(messages, options, cancellationToken);
        var content = response.Value.Content[0].Text;

        return ParseRecommendation(content);
    }

    private SourceRecommendation? ParseRecommendation(string content)
    {
        try
        {
            content = CleanJsonResponse(content);
            return JsonSerializer.Deserialize<SourceRecommendation>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI recommendation response: {Content}", content);
            return null;
        }
    }

    private string CleanJsonResponse(string content)
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

    private static string GetSourceDescription(string sourceName) => sourceName switch
    {
        "Hatena Bookmark" => "Japanese social bookmarking for trending articles",
        "Qiita" => "Japanese developer Q&A and tutorials",
        "Zenn" => "Japanese tech blog platform",
        "arXiv" => "Academic research papers",
        "Hacker News" => "English tech news and discussions",
        "Reddit" => "English community discussions",
        _ => "Information source"
    };

    private class SourceRecommendation
    {
        public KeywordAnalysis? KeywordAnalysis { get; set; }
        public List<SourceRec> Sources { get; set; } = new();
    }

    private class KeywordAnalysis
    {
        public string DetectedLanguage { get; set; } = "";
        public string Category { get; set; } = "";
        public string Reasoning { get; set; } = "";
    }

    private class SourceRec
    {
        public string Name { get; set; } = "";
        public bool Recommended { get; set; }
        public string Reason { get; set; } = "";
    }
}

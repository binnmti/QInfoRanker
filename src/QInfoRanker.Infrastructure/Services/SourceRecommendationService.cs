using System.ClientModel;
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

public class SourceRecommendationService : ISourceRecommendationService
{
    private readonly AppDbContext _context;
    private readonly AzureOpenAIOptions _openAIOptions;
    private readonly ILogger<SourceRecommendationService> _logger;
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
            var baseEndpoint = _openAIOptions.Endpoint.TrimEnd('/');
            var v1Endpoint = new Uri($"{baseEndpoint}/openai/v1");
            var credential = new ApiKeyCredential(_openAIOptions.ApiKey);

            var clientOptions = new OpenAIClientOptions
            {
                Endpoint = v1Endpoint
            };

            var openAIClient = new OpenAIClient(credential, clientOptions);
            _chatClient = openAIClient.GetChatClient(_openAIOptions.DeploymentName);
        }
    }

    public async Task<SourceRecommendationResult> RecommendSourcesAsync(string keyword, CancellationToken cancellationToken = default)
    {
        var result = new SourceRecommendationResult();
        var templateSources = await _context.Sources
            .Where(s => s.IsTemplate)
            .ToListAsync(cancellationToken);

        // 全テンプレートソースを収集対象とする
        result.RecommendedSources = templateSources;

        // AIで英語エイリアスを生成
        if (_chatClient != null)
        {
            try
            {
                _logger.LogInformation("Requesting keyword analysis for '{Keyword}' using model: {Model}",
                    keyword, _openAIOptions.DeploymentName);

                var analysis = await GetKeywordAnalysisAsync(keyword, cancellationToken);
                if (analysis != null)
                {
                    result.KeywordAnalysis = analysis.Reasoning;
                    result.DetectedLanguage = analysis.DetectedLanguage;
                    result.DetectedCategory = analysis.Category;
                    result.EnglishAliases = analysis.EnglishAliases;

                    _logger.LogInformation(
                        "Keyword analysis successful for '{Keyword}': Language={Language}, Category={Category}, Aliases='{Aliases}'",
                        keyword, analysis.DetectedLanguage, analysis.Category, analysis.EnglishAliases);
                }
                else
                {
                    _logger.LogWarning("Keyword analysis returned null for '{Keyword}'", keyword);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get keyword analysis for '{Keyword}'. Continuing without aliases. " +
                    "Endpoint={Endpoint}, Model={Model}",
                    keyword, _openAIOptions.Endpoint, _openAIOptions.DeploymentName);
            }
        }
        else
        {
            _logger.LogWarning("Azure OpenAI not configured (ChatClient is null). " +
                "Endpoint={Endpoint}, ApiKey={HasKey}. Skipping English alias generation.",
                _openAIOptions.Endpoint ?? "null",
                !string.IsNullOrEmpty(_openAIOptions.ApiKey));
        }

        _logger.LogInformation(
            "Using all {Count} sources for keyword '{Keyword}'. English aliases: {Aliases}",
            templateSources.Count,
            keyword,
            result.EnglishAliases ?? "none");

        return result;
    }

    private async Task<KeywordAnalysis?> GetKeywordAnalysisAsync(
        string keyword,
        CancellationToken cancellationToken)
    {
        var prompt = $$"""
            キーワード「{{keyword}}」を分析して、英語での検索キーワードを生成してください。

            【重要】固有名詞（会社名、製品名、サービス名、人名、ブランド名など）の場合:
            - 意味の拡張や類語展開は行わないでください
            - 元のキーワードと、その読み仮名・英語表記のみを出力してください
            - 事業内容や関連分野への展開は禁止です

            JSON形式でのみ回答（日本語で）:
            {
              "detected_language": "japanese|english|both",
              "category": "technology|science|business|news|entertainment|medical|finance|academic|social|other",
              "is_proper_noun": true/false,
              "reasoning": "キーワードの分析結果（日本語、1-2文）",
              "english_aliases": "英語での検索キーワード（カンマ区切り、2-3個）"
            }

            例1: キーワード「量子コンピュータ」の場合（一般名詞）
            {
              "detected_language": "japanese",
              "category": "technology",
              "is_proper_noun": false,
              "reasoning": "量子力学を応用したコンピュータ技術に関するキーワード",
              "english_aliases": "quantum computer, quantum computing"
            }

            例2: キーワード「HACARUS」の場合（固有名詞・会社名）
            {
              "detected_language": "english",
              "category": "business",
              "is_proper_noun": true,
              "reasoning": "日本のAIスタートアップ企業の社名",
              "english_aliases": "HACARUS"
            }

            例3: キーワード「トヨタ」の場合（固有名詞・会社名）
            {
              "detected_language": "japanese",
              "category": "business",
              "is_proper_noun": true,
              "reasoning": "日本の自動車メーカーの社名",
              "english_aliases": "Toyota, トヨタ"
            }
            """;

        var systemPrompt = "あなたはキーワード分析の専門家です。JSON形式でのみ回答してください。";
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(prompt)
        };

        var options = new ChatCompletionOptions();

        // 推論モデルでなければ Temperature を設定
        if (!ModelCapabilities.IsReasoningModel(_openAIOptions.DeploymentName))
        {
            options.Temperature = 0.3f;
        }
        else
        {
            options.ReasoningEffortLevel = ChatReasoningEffortLevel.Low;
        }

        var response = await _chatClient!.CompleteChatAsync(
            messages: messages,
            options: options,
            cancellationToken: cancellationToken);

        var content = response.Value.Content[0].Text;
        return ParseKeywordAnalysis(content);
    }

    private KeywordAnalysis? ParseKeywordAnalysis(string content)
    {
        try
        {
            content = CleanJsonResponse(content);
            return JsonSerializer.Deserialize<KeywordAnalysis>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse keyword analysis response: {Content}", content);
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

    private class KeywordAnalysis
    {
        [JsonPropertyName("detected_language")]
        public string DetectedLanguage { get; set; } = "";

        [JsonPropertyName("category")]
        public string Category { get; set; } = "";

        [JsonPropertyName("reasoning")]
        public string Reasoning { get; set; } = "";

        [JsonPropertyName("english_aliases")]
        public string EnglishAliases { get; set; } = "";
    }

}

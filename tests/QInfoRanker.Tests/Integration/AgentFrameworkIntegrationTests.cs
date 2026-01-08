using System.ClientModel;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using QInfoRanker.Infrastructure.Scoring;
using Xunit.Abstractions;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only

namespace QInfoRanker.Tests.Integration;

/// <summary>
/// OpenAI SDK による API 統合テスト
/// Chat Completion API と Responses API の両方をテスト
/// CI/CDでスキップ: dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class OpenAIApiIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IConfiguration _configuration;
    private readonly OpenAIClient? _client;
    private readonly AzureOpenAIOptions? _openAIOptions;

    public OpenAIApiIntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.test.json", optional: false)
            .AddJsonFile("appsettings.test.local.json", optional: true)
            .Build();

        _openAIOptions = _configuration.GetSection("AzureOpenAI").Get<AzureOpenAIOptions>();

        if (_openAIOptions != null &&
            !string.IsNullOrEmpty(_openAIOptions.Endpoint) &&
            !string.IsNullOrEmpty(_openAIOptions.ApiKey) &&
            !_openAIOptions.Endpoint.Contains("YOUR_"))
        {
            var baseEndpoint = _openAIOptions.Endpoint.TrimEnd('/');
            var v1Endpoint = new Uri($"{baseEndpoint}/openai/v1");
            var credential = new ApiKeyCredential(_openAIOptions.ApiKey);

            var clientOptions = new OpenAIClientOptions
            {
                Endpoint = v1Endpoint
            };

            _client = new OpenAIClient(credential, clientOptions);
        }
    }

    /// <summary>
    /// Chat Completion API テスト (gpt-4o-mini)
    /// </summary>
    [Fact]
    public async Task ChatCompletionApi_CanComplete()
    {
        if (_client == null)
        {
            _output.WriteLine("Azure OpenAI未設定のためスキップ");
            return;
        }

        _output.WriteLine("=== Chat Completion API Test (gpt-4o-mini) ===\n");

        // Arrange
        var chatClient = _client.GetChatClient("gpt-4o-mini");
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("You are a helpful assistant. Respond in one short sentence."),
            new UserChatMessage("Say hello.")
        };

        // Act
        var response = await chatClient.CompleteChatAsync(messages);
        var result = response.Value.Content[0].Text;

        // Assert
        _output.WriteLine($"Response: {result}");
        _output.WriteLine($"Input tokens: {response.Value.Usage?.InputTokenCount}");
        _output.WriteLine($"Output tokens: {response.Value.Usage?.OutputTokenCount}");
        Assert.False(string.IsNullOrEmpty(result));
    }

    /// <summary>
    /// Responses API テスト (gpt-5.1-codex-mini)
    /// CodexモデルはChat Completion APIをサポートしないため、Responses APIを使用
    /// </summary>
    [Fact]
    public async Task ResponsesApi_CanCreateResponse()
    {
        if (_client == null)
        {
            _output.WriteLine("Azure OpenAI未設定のためスキップ");
            return;
        }

        _output.WriteLine("=== Responses API Test (gpt-5.1-codex-mini) ===\n");

        // Arrange
        var responsesClient = _client.GetResponsesClient("gpt-5.1-codex-mini");

        // Act
        var clientResult = await responsesClient.CreateResponseAsync("Say hello in one short sentence.");
        var response = clientResult.Value;
        var result = response.GetOutputText();

        // Assert
        _output.WriteLine($"Response: {result}");
        _output.WriteLine($"Input tokens: {response.Usage?.InputTokenCount}");
        _output.WriteLine($"Output tokens: {response.Usage?.OutputTokenCount}");
        Assert.False(string.IsNullOrEmpty(result));
    }

    /// <summary>
    /// 推論モデル (gpt-5-nano) テスト
    /// Chat Completion API で動作、Temperature設定不可
    /// </summary>
    [Fact]
    public async Task ReasoningModel_CanComplete()
    {
        if (_client == null)
        {
            _output.WriteLine("Azure OpenAI未設定のためスキップ");
            return;
        }

        _output.WriteLine("=== Reasoning Model Test (gpt-5-nano) ===\n");

        // Arrange
        var chatClient = _client.GetChatClient("gpt-5-nano");
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("You are a helpful assistant. Respond in one short sentence."),
            new UserChatMessage("What is 2+2?")
        };
        var options = new ChatCompletionOptions
        {
            ReasoningEffortLevel = ChatReasoningEffortLevel.Low
        };

        // Act
        var response = await chatClient.CompleteChatAsync(messages, options);
        var result = response.Value.Content[0].Text;

        // Assert
        _output.WriteLine($"Response: {result}");
        _output.WriteLine($"Input tokens: {response.Value.Usage?.InputTokenCount}");
        _output.WriteLine($"Output tokens: {response.Value.Usage?.OutputTokenCount}");
        Assert.False(string.IsNullOrEmpty(result));
        // モデルは "4" または "four" で回答する可能性がある
        Assert.True(result.Contains("4") || result.ToLower().Contains("four"), 
            $"Response should contain '4' or 'four': {result}");
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}

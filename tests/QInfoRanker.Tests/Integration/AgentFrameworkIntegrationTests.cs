using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using QInfoRanker.Infrastructure.Scoring;
using Xunit.Abstractions;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only

namespace QInfoRanker.Tests.Integration;

/// <summary>
/// Microsoft Agent Framework の統合テスト
/// Chat Completion API と Responses API の両方をテスト
/// CI/CDでスキップ: dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class AgentFrameworkIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IConfiguration _configuration;
    private readonly OpenAIClient? _client;
    private readonly AzureOpenAIOptions? _openAIOptions;

    public AgentFrameworkIntegrationTests(ITestOutputHelper output)
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
    /// Chat Completion API を使用したAIAgentテスト (gpt-4o-mini)
    /// </summary>
    [Fact]
    public async Task ChatCompletionAgent_CanRunAsync()
    {
        if (_client == null)
        {
            _output.WriteLine("Azure OpenAI未設定のためスキップ");
            return;
        }

        _output.WriteLine("=== Chat Completion API Agent Test ===\n");

        // Arrange
        var chatClient = _client.GetChatClient("gpt-4o-mini");
        AIAgent agent = chatClient.CreateAIAgent(
            instructions: "You are a helpful assistant. Respond in one short sentence.",
            name: "TestChatAgent");

        // Act
        var response = await agent.RunAsync("Say hello.");
        var result = response.Text;

        // Assert
        _output.WriteLine($"Response: {result}");
        Assert.False(string.IsNullOrEmpty(result));
    }

    /// <summary>
    /// Responses API を使用したAIAgentテスト (gpt-5.1-codex-mini)
    /// codexモデルはResponses APIが必要
    /// </summary>
    [Fact]
    public async Task ResponsesApiAgent_CanRunAsync()
    {
        if (_client == null)
        {
            _output.WriteLine("Azure OpenAI未設定のためスキップ");
            return;
        }

        _output.WriteLine("=== Responses API Agent Test ===\n");

        // Arrange
        var responseClient = _client.GetResponsesClient("gpt-5.1-codex-mini");
        AIAgent agent = responseClient.CreateAIAgent(
            instructions: "You are a helpful assistant. Respond in one short sentence.",
            name: "TestResponseAgent");

        // Act
        var response = await agent.RunAsync("Say hello.");
        var result = response.Text;

        // Assert
        _output.WriteLine($"Response: {result}");
        Assert.False(string.IsNullOrEmpty(result));
    }

    /// <summary>
    /// 推論モデル (gpt-5-nano) のテスト
    /// Chat Completion API で動作
    /// </summary>
    [Fact]
    public async Task ReasoningModelAgent_CanRunAsync()
    {
        if (_client == null)
        {
            _output.WriteLine("Azure OpenAI未設定のためスキップ");
            return;
        }

        _output.WriteLine("=== Reasoning Model Agent Test (gpt-5-nano) ===\n");

        // Arrange
        var chatClient = _client.GetChatClient("gpt-5-nano");
        AIAgent agent = chatClient.CreateAIAgent(
            instructions: "You are a helpful assistant. Respond in one short sentence.",
            name: "TestReasoningAgent");

        // Act
        var response = await agent.RunAsync("What is 2+2?");
        var result = response.Text;

        // Assert
        _output.WriteLine($"Response: {result}");
        Assert.False(string.IsNullOrEmpty(result));
        Assert.Contains("4", result);
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}

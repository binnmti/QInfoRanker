using System;
using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only

Console.WriteLine("=== Microsoft Agent Framework Test ===");

// Configuration (match the actual project settings)
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? "";
var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_APIKEY") ?? "";

if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("Please set AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_APIKEY environment variables");
    return;
}

// Create OpenAI client with v1 endpoint (for Azure OpenAI compatibility)
var baseEndpoint = endpoint.TrimEnd('/');
var v1Endpoint = new Uri($"{baseEndpoint}/openai/v1");
var credential = new ApiKeyCredential(apiKey);
var clientOptions = new OpenAIClientOptions { Endpoint = v1Endpoint };
var client = new OpenAIClient(credential, clientOptions);

// Test 1: Chat Completion API model (gpt-4o-mini or gpt-5-nano)
Console.WriteLine("\n--- Test 1: Chat Completion API ---");
try
{
    var chatClient = client.GetChatClient("gpt-4o-mini");
    AIAgent chatAgent = chatClient.CreateAIAgent(
        instructions: "You are a helpful assistant. Respond briefly.",
        name: "ChatAgent");

    var result = await chatAgent.RunAsync("Say hello in one word.");
    Console.WriteLine($"Chat API Response: {result}");
}
catch (Exception ex)
{
    Console.WriteLine($"Chat API Error: {ex.Message}");
}

// Test 2: Responses API model (gpt-5.1-codex-mini)
Console.WriteLine("\n--- Test 2: Responses API ---");
try
{
    var responseClient = client.GetResponsesClient("gpt-5.1-codex-mini");
    AIAgent responseAgent = responseClient.CreateAIAgent(
        instructions: "You are a helpful assistant. Respond briefly.",
        name: "ResponseAgent");

    var result = await responseAgent.RunAsync("Say hello in one word.");
    Console.WriteLine($"Responses API Response: {result}");
}
catch (Exception ex)
{
    Console.WriteLine($"Responses API Error: {ex.Message}");
}

Console.WriteLine("\n=== Test Complete ===");

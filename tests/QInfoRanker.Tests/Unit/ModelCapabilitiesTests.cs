using QInfoRanker.Infrastructure.Scoring;
using Xunit;

namespace QInfoRanker.Tests.Unit;

/// <summary>
/// Azure OpenAI モデル機能テスト
///
/// このテストファイルは、各モデルがサポートするパラメータを文書化します。
/// 新しいモデルが追加された場合、このテストを更新してください。
///
/// 参照: https://learn.microsoft.com/en-us/azure/ai-foundry/openai/how-to/reasoning
/// </summary>
public class ModelCapabilitiesTests
{
    #region O-Series Reasoning Models (temperature/max_tokens をサポートしない)

    [Theory]
    [InlineData("o1")]
    [InlineData("o1-mini")]
    [InlineData("o1-preview")]
    [InlineData("o1-2024-12-17")]
    public void O1Models_AreReasoningModels(string deploymentName)
    {
        Assert.True(ModelCapabilities.IsReasoningModel(deploymentName),
            $"{deploymentName} should be a reasoning model");
        Assert.False(ModelCapabilities.SupportsTemperature(deploymentName),
            $"{deploymentName} should NOT support temperature");
        Assert.False(ModelCapabilities.SupportsMaxTokens(deploymentName),
            $"{deploymentName} should NOT support max_tokens");
    }

    [Theory]
    [InlineData("o3")]
    [InlineData("o3-mini")]
    [InlineData("o3-pro")]
    [InlineData("o3-2025-04-16")]
    public void O3Models_AreReasoningModels(string deploymentName)
    {
        Assert.True(ModelCapabilities.IsReasoningModel(deploymentName),
            $"{deploymentName} should be a reasoning model");
        Assert.False(ModelCapabilities.SupportsTemperature(deploymentName),
            $"{deploymentName} should NOT support temperature");
        Assert.False(ModelCapabilities.SupportsMaxTokens(deploymentName),
            $"{deploymentName} should NOT support max_tokens");
    }

    [Theory]
    [InlineData("o4-mini")]
    [InlineData("o4-mini-2025-04-16")]
    public void O4Models_AreReasoningModels(string deploymentName)
    {
        Assert.True(ModelCapabilities.IsReasoningModel(deploymentName),
            $"{deploymentName} should be a reasoning model");
        Assert.False(ModelCapabilities.SupportsTemperature(deploymentName),
            $"{deploymentName} should NOT support temperature");
        Assert.False(ModelCapabilities.SupportsMaxTokens(deploymentName),
            $"{deploymentName} should NOT support max_tokens");
    }

    [Theory]
    [InlineData("codex-mini")]
    [InlineData("codex-mini-2025-05-16")]
    public void CodexMini_IsReasoningModel(string deploymentName)
    {
        Assert.True(ModelCapabilities.IsReasoningModel(deploymentName),
            $"{deploymentName} should be a reasoning model");
        Assert.False(ModelCapabilities.SupportsTemperature(deploymentName),
            $"{deploymentName} should NOT support temperature");
        Assert.False(ModelCapabilities.SupportsMaxTokens(deploymentName),
            $"{deploymentName} should NOT support max_tokens");
    }

    #endregion

    #region GPT-5 Series Reasoning Models (全て推論モデル)

    [Theory]
    [InlineData("gpt-5")]
    [InlineData("gpt-5-mini")]
    [InlineData("gpt-5-nano")]
    [InlineData("gpt-5-codex")]
    [InlineData("gpt-5-pro")]
    [InlineData("gpt-5-chat")]
    [InlineData("gpt-5-2025-08-07")]
    public void Gpt5Models_AreReasoningModels(string deploymentName)
    {
        Assert.True(ModelCapabilities.IsReasoningModel(deploymentName),
            $"{deploymentName} should be a reasoning model");
        Assert.False(ModelCapabilities.SupportsTemperature(deploymentName),
            $"{deploymentName} should NOT support temperature");
        Assert.False(ModelCapabilities.SupportsMaxTokens(deploymentName),
            $"{deploymentName} should NOT support max_tokens");
        Assert.True(ModelCapabilities.SupportsReasoningEffort(deploymentName),
            $"{deploymentName} should support reasoning_effort");
    }

    [Theory]
    [InlineData("gpt-5.1")]
    [InlineData("gpt-5.1-chat")]
    [InlineData("gpt-5.1-codex")]
    [InlineData("gpt-5.1-codex-mini")]
    [InlineData("gpt-5.1-codex-max")]
    [InlineData("gpt-5.2")]
    public void Gpt51AndLaterModels_AreReasoningModels(string deploymentName)
    {
        Assert.True(ModelCapabilities.IsReasoningModel(deploymentName),
            $"{deploymentName} should be a reasoning model");
        Assert.False(ModelCapabilities.SupportsTemperature(deploymentName),
            $"{deploymentName} should NOT support temperature");
        Assert.False(ModelCapabilities.SupportsMaxTokens(deploymentName),
            $"{deploymentName} should NOT support max_tokens");
    }

    #endregion

    #region Standard Models (temperature/max_tokens をサポートする)

    [Theory]
    [InlineData("gpt-4o")]
    [InlineData("gpt-4o-mini")]
    [InlineData("gpt-4o-2024-08-06")]
    public void Gpt4oModels_AreStandardModels(string deploymentName)
    {
        Assert.False(ModelCapabilities.IsReasoningModel(deploymentName),
            $"{deploymentName} should NOT be a reasoning model");
        Assert.True(ModelCapabilities.SupportsTemperature(deploymentName),
            $"{deploymentName} should support temperature");
        Assert.True(ModelCapabilities.SupportsMaxTokens(deploymentName),
            $"{deploymentName} should support max_tokens");
        Assert.False(ModelCapabilities.SupportsReasoningEffort(deploymentName),
            $"{deploymentName} should NOT support reasoning_effort");
    }

    [Theory]
    [InlineData("gpt-4")]
    [InlineData("gpt-4-turbo")]
    [InlineData("gpt-4-32k")]
    [InlineData("gpt-4-0125-preview")]
    public void Gpt4Models_AreStandardModels(string deploymentName)
    {
        Assert.False(ModelCapabilities.IsReasoningModel(deploymentName),
            $"{deploymentName} should NOT be a reasoning model");
        Assert.True(ModelCapabilities.SupportsTemperature(deploymentName),
            $"{deploymentName} should support temperature");
        Assert.True(ModelCapabilities.SupportsMaxTokens(deploymentName),
            $"{deploymentName} should support max_tokens");
    }

    [Theory]
    [InlineData("gpt-3.5-turbo")]
    [InlineData("gpt-35-turbo")] // Azure naming variant
    [InlineData("gpt-35-turbo-16k")]
    public void Gpt35Models_AreStandardModels(string deploymentName)
    {
        Assert.False(ModelCapabilities.IsReasoningModel(deploymentName),
            $"{deploymentName} should NOT be a reasoning model");
        Assert.True(ModelCapabilities.SupportsTemperature(deploymentName),
            $"{deploymentName} should support temperature");
        Assert.True(ModelCapabilities.SupportsMaxTokens(deploymentName),
            $"{deploymentName} should support max_tokens");
    }

    #endregion

    #region Custom Deployment Names

    [Theory]
    [InlineData("my-gpt-5-deployment")]      // Contains gpt-5, should be reasoning
    [InlineData("prod-o3-mini")]              // Contains o3, should be reasoning
    public void CustomDeploymentNames_WithReasoningModelPrefix_AreDetected(string deploymentName)
    {
        // Note: Current implementation uses StartsWith, so these will NOT match.
        // If you need to support custom deployment names, the logic should be updated.
        // This test documents the current behavior.
        Assert.False(ModelCapabilities.IsReasoningModel(deploymentName),
            $"Custom deployment '{deploymentName}' with model name in middle is NOT detected as reasoning (current behavior)");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EmptyOrNullDeploymentNames_ReturnFalse(string deploymentName)
    {
        Assert.False(ModelCapabilities.IsReasoningModel(deploymentName!),
            "Empty or null deployment names should return false (treated as unknown)");
    }

    [Theory]
    [InlineData("unknown-model")]
    [InlineData("custom-deployment")]
    [InlineData("my-fine-tuned-model")]
    public void UnknownModels_TreatedAsStandard(string deploymentName)
    {
        // Unknown models default to standard (supports temperature/max_tokens)
        Assert.False(ModelCapabilities.IsReasoningModel(deploymentName),
            $"Unknown model '{deploymentName}' should be treated as standard");
        Assert.True(ModelCapabilities.SupportsTemperature(deploymentName),
            $"Unknown model '{deploymentName}' should support temperature by default");
    }

    #endregion

    #region Capability Summary

    [Fact]
    public void GetCapabilitySummary_ReturnsCorrectInfo_ForReasoningModel()
    {
        var summary = ModelCapabilities.GetCapabilitySummary("gpt-5");

        Assert.True(summary.IsReasoningModel);
        Assert.False(summary.SupportsTemperature);
        Assert.False(summary.SupportsMaxTokens);
        Assert.True(summary.SupportsReasoningEffort);
        Assert.Equal("max_completion_tokens", summary.TokenLimitParameterName);
    }

    [Fact]
    public void GetCapabilitySummary_ReturnsCorrectInfo_ForStandardModel()
    {
        var summary = ModelCapabilities.GetCapabilitySummary("gpt-4o-mini");

        Assert.False(summary.IsReasoningModel);
        Assert.True(summary.SupportsTemperature);
        Assert.True(summary.SupportsMaxTokens);
        Assert.False(summary.SupportsReasoningEffort);
        Assert.Equal("max_tokens", summary.TokenLimitParameterName);
    }

    #endregion

    #region GetEffectiveTemperature / GetEffectiveMaxTokens

    [Theory]
    [InlineData("gpt-5")]
    [InlineData("o3")]
    [InlineData("o1")]
    public void GetEffectiveTemperature_ReturnsNull_ForReasoningModels(string deploymentName)
    {
        // 推論モデルは Temperature をサポートしないので null が返る
        var result = ModelCapabilities.GetEffectiveTemperature(deploymentName);
        Assert.Null(result);

        // オーバーライド値を指定しても無視される
        var resultWithOverride = ModelCapabilities.GetEffectiveTemperature(deploymentName, 0.5f);
        Assert.Null(resultWithOverride);
    }

    [Theory]
    [InlineData("gpt-4o")]
    [InlineData("gpt-4o-mini")]
    [InlineData("gpt-3.5-turbo")]
    public void GetEffectiveTemperature_ReturnsDefault_ForStandardModels(string deploymentName)
    {
        // 通常モデルはデフォルト値が返る
        var result = ModelCapabilities.GetEffectiveTemperature(deploymentName);
        Assert.Equal(ModelCapabilities.DefaultTemperature, result);
    }

    [Fact]
    public void GetEffectiveTemperature_ReturnsOverride_ForStandardModels()
    {
        // 通常モデルはオーバーライド値が有効
        var result = ModelCapabilities.GetEffectiveTemperature("gpt-4o", 0.7f);
        Assert.Equal(0.7f, result);
    }

    [Theory]
    [InlineData("gpt-5")]
    [InlineData("o3")]
    [InlineData("o1")]
    public void GetEffectiveMaxTokens_ReturnsNull_ForReasoningModels(string deploymentName)
    {
        // 推論モデルは MaxTokens をサポートしないので null が返る
        var result = ModelCapabilities.GetEffectiveMaxTokens(deploymentName);
        Assert.Null(result);

        // オーバーライド値を指定しても無視される
        var resultWithOverride = ModelCapabilities.GetEffectiveMaxTokens(deploymentName, 8000);
        Assert.Null(resultWithOverride);
    }

    [Theory]
    [InlineData("gpt-4o")]
    [InlineData("gpt-4o-mini")]
    public void GetEffectiveMaxTokens_ReturnsDefault_ForStandardModels(string deploymentName)
    {
        // 通常モデルはデフォルト値が返る
        var result = ModelCapabilities.GetEffectiveMaxTokens(deploymentName);
        Assert.Equal(ModelCapabilities.DefaultMaxTokens, result);
    }

    [Fact]
    public void GetEffectiveMaxTokens_ReturnsOverride_ForStandardModels()
    {
        // 通常モデルはオーバーライド値が有効
        var result = ModelCapabilities.GetEffectiveMaxTokens("gpt-4o", 8000);
        Assert.Equal(8000, result);
    }

    [Fact]
    public void GetEffectiveMaxTokens_IgnoresZeroOrNegativeOverride()
    {
        // 0以下のオーバーライド値は無視してデフォルトを使用
        var result = ModelCapabilities.GetEffectiveMaxTokens("gpt-4o", 0);
        Assert.Equal(ModelCapabilities.DefaultMaxTokens, result);

        var resultNegative = ModelCapabilities.GetEffectiveMaxTokens("gpt-4o", -100);
        Assert.Equal(ModelCapabilities.DefaultMaxTokens, resultNegative);
    }

    #endregion

    #region Model Parameter Reference (Documentation)

    /// <summary>
    /// このテストはドキュメント目的です。
    /// 各モデルの正しいパラメータ設定を示します。
    /// </summary>
    [Fact]
    public void DocumentModelParameters()
    {
        // ===== 推論モデル (Reasoning Models) =====
        // temperature: 設定不可（デフォルト1固定）
        // max_tokens: 設定不可（max_completion_tokens を使用）
        // reasoning_effort: low/medium/high (gpt-5系は minimal, none もサポート)

        var reasoningModels = new[]
        {
            "o1", "o1-mini",
            "o3", "o3-mini", "o3-pro",
            "o4-mini",
            "codex-mini",
            "gpt-5", "gpt-5-mini", "gpt-5-nano", "gpt-5-codex", "gpt-5-pro",
            "gpt-5.1", "gpt-5.1-chat", "gpt-5.1-codex",
            "gpt-5.2"
        };

        foreach (var model in reasoningModels)
        {
            Assert.True(ModelCapabilities.IsReasoningModel(model),
                $"Reasoning model check failed for: {model}");
        }

        // ===== 通常モデル (Standard Models) =====
        // temperature: 0.0-2.0
        // max_tokens: 設定可
        // reasoning_effort: 設定不可

        var standardModels = new[]
        {
            "gpt-4o", "gpt-4o-mini",
            "gpt-4", "gpt-4-turbo",
            "gpt-3.5-turbo"
        };

        foreach (var model in standardModels)
        {
            Assert.False(ModelCapabilities.IsReasoningModel(model),
                $"Standard model check failed for: {model}");
        }
    }

    #endregion
}

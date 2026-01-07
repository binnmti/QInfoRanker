using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using QInfoRanker.Infrastructure.Scoring;
using Xunit.Abstractions;

namespace QInfoRanker.Tests.Unit;

/// <summary>
/// アンサンブル評価設定クラスのユニットテスト
/// TDD: Red → Green → Refactor
/// </summary>
public class EnsembleScoringOptionsTests
{
    private readonly ITestOutputHelper _output;

    public EnsembleScoringOptionsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Default Values Tests

    [Fact]
    public void EnsembleScoringOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new EnsembleScoringOptions();

        // Assert
        Assert.False(options.EnableEnsemble);
        Assert.Empty(options.Judges);
        Assert.NotNull(options.MetaJudge);
        Assert.Equal(5.0, options.ConsensusThreshold);
        Assert.True(options.SkipMetaJudgeOnConsensus);
        Assert.Equal(3, options.MaxParallelJudges);
        Assert.Equal(60000, options.JudgeTimeoutMs);
    }

    [Fact]
    public void JudgeModelConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new JudgeModelConfiguration();

        // Assert
        Assert.Equal(string.Empty, config.JudgeId);
        Assert.Equal(string.Empty, config.DisplayName);
        Assert.Equal(string.Empty, config.DeploymentName);
        Assert.Equal(1.0, config.Weight);
        Assert.Null(config.Specialty);
        Assert.Equal(4000, config.MaxTokens);
        Assert.Equal(0.3f, config.Temperature);
        Assert.True(config.IsEnabled);
    }

    [Fact]
    public void MetaJudgeConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new MetaJudgeConfiguration();

        // Assert
        Assert.True(config.IsEnabled);
        Assert.Equal("o3-pro", config.DeploymentName);
        Assert.Equal(6000, config.MaxTokens);
        Assert.Equal(0.1f, config.Temperature);
        Assert.Equal(15.0, config.ContradictionThreshold);
    }

    #endregion

    #region Configuration Binding Tests

    [Fact]
    public void EnsembleScoringOptions_BindsFromConfiguration()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["EnsembleScoring:EnableEnsemble"] = "true",
            ["EnsembleScoring:MaxParallelJudges"] = "5",
            ["EnsembleScoring:JudgeTimeoutMs"] = "120000",
            ["EnsembleScoring:ConsensusThreshold"] = "3.5",
            ["EnsembleScoring:SkipMetaJudgeOnConsensus"] = "false",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.Configure<EnsembleScoringOptions>(
            configuration.GetSection(EnsembleScoringOptions.SectionName));

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<EnsembleScoringOptions>>().Value;

        // Assert
        Assert.True(options.EnableEnsemble);
        Assert.Equal(5, options.MaxParallelJudges);
        Assert.Equal(120000, options.JudgeTimeoutMs);
        Assert.Equal(3.5, options.ConsensusThreshold);
        Assert.False(options.SkipMetaJudgeOnConsensus);
    }

    [Fact]
    public void EnsembleScoringOptions_BindsJudgesArray()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["EnsembleScoring:EnableEnsemble"] = "true",
            ["EnsembleScoring:Judges:0:JudgeId"] = "JudgeA",
            ["EnsembleScoring:Judges:0:DisplayName"] = "gpt-5 (汎用評価)",
            ["EnsembleScoring:Judges:0:DeploymentName"] = "gpt-5",
            ["EnsembleScoring:Judges:0:Weight"] = "1.0",
            ["EnsembleScoring:Judges:0:IsEnabled"] = "true",
            ["EnsembleScoring:Judges:1:JudgeId"] = "JudgeB",
            ["EnsembleScoring:Judges:1:DisplayName"] = "gpt-5-codex (技術評価)",
            ["EnsembleScoring:Judges:1:DeploymentName"] = "gpt-5-codex",
            ["EnsembleScoring:Judges:1:Weight"] = "1.2",
            ["EnsembleScoring:Judges:1:Specialty"] = "technical",
            ["EnsembleScoring:Judges:1:IsEnabled"] = "true",
            ["EnsembleScoring:Judges:2:JudgeId"] = "JudgeC",
            ["EnsembleScoring:Judges:2:DisplayName"] = "o3 (推論評価)",
            ["EnsembleScoring:Judges:2:DeploymentName"] = "o3",
            ["EnsembleScoring:Judges:2:Weight"] = "1.0",
            ["EnsembleScoring:Judges:2:Specialty"] = "reasoning",
            ["EnsembleScoring:Judges:2:IsEnabled"] = "true",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.Configure<EnsembleScoringOptions>(
            configuration.GetSection(EnsembleScoringOptions.SectionName));

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<EnsembleScoringOptions>>().Value;

        // Assert
        Assert.Equal(3, options.Judges.Count);

        var judgeA = options.Judges[0];
        Assert.Equal("JudgeA", judgeA.JudgeId);
        Assert.Equal("gpt-5 (汎用評価)", judgeA.DisplayName);
        Assert.Equal("gpt-5", judgeA.DeploymentName);
        Assert.Equal(1.0, judgeA.Weight);
        Assert.Null(judgeA.Specialty);
        Assert.True(judgeA.IsEnabled);

        var judgeB = options.Judges[1];
        Assert.Equal("JudgeB", judgeB.JudgeId);
        Assert.Equal(1.2, judgeB.Weight);
        Assert.Equal("technical", judgeB.Specialty);

        var judgeC = options.Judges[2];
        Assert.Equal("JudgeC", judgeC.JudgeId);
        Assert.Equal("reasoning", judgeC.Specialty);

        _output.WriteLine($"Loaded {options.Judges.Count} judges");
        foreach (var judge in options.Judges)
        {
            _output.WriteLine($"  - {judge.JudgeId}: {judge.DeploymentName} (Weight: {judge.Weight}, Specialty: {judge.Specialty ?? "general"})");
        }
    }

    [Fact]
    public void EnsembleScoringOptions_BindsMetaJudge()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["EnsembleScoring:MetaJudge:IsEnabled"] = "true",
            ["EnsembleScoring:MetaJudge:DeploymentName"] = "o3-pro",
            ["EnsembleScoring:MetaJudge:MaxTokens"] = "8000",
            ["EnsembleScoring:MetaJudge:Temperature"] = "0.05",
            ["EnsembleScoring:MetaJudge:ContradictionThreshold"] = "20.0",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.Configure<EnsembleScoringOptions>(
            configuration.GetSection(EnsembleScoringOptions.SectionName));

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<EnsembleScoringOptions>>().Value;

        // Assert
        Assert.True(options.MetaJudge.IsEnabled);
        Assert.Equal("o3-pro", options.MetaJudge.DeploymentName);
        Assert.Equal(8000, options.MetaJudge.MaxTokens);
        Assert.Equal(0.05f, options.MetaJudge.Temperature);
        Assert.Equal(20.0, options.MetaJudge.ContradictionThreshold);

        _output.WriteLine($"MetaJudge: {options.MetaJudge.DeploymentName}");
        _output.WriteLine($"  MaxTokens: {options.MetaJudge.MaxTokens}");
        _output.WriteLine($"  Temperature: {options.MetaJudge.Temperature}");
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void JudgeModelConfiguration_InvalidWeight_ShouldBeDetected(double weight)
    {
        // Arrange
        var config = new JudgeModelConfiguration { Weight = weight };

        // Assert - Weight should be positive
        Assert.True(config.Weight <= 0, "Weight should be positive for valid configuration");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    public void EnsembleScoringOptions_InvalidTimeout_ShouldBeDetected(int timeout)
    {
        // Arrange
        var options = new EnsembleScoringOptions { JudgeTimeoutMs = timeout };

        // Assert
        Assert.True(options.JudgeTimeoutMs <= 0, "Timeout should be positive for valid configuration");
    }

    [Fact]
    public void EnsembleScoringOptions_GetEnabledJudges_ReturnsOnlyEnabled()
    {
        // Arrange
        var options = new EnsembleScoringOptions
        {
            Judges = new List<JudgeModelConfiguration>
            {
                new() { JudgeId = "JudgeA", IsEnabled = true },
                new() { JudgeId = "JudgeB", IsEnabled = false },
                new() { JudgeId = "JudgeC", IsEnabled = true },
            }
        };

        // Act
        var enabledJudges = options.Judges.Where(j => j.IsEnabled).ToList();

        // Assert
        Assert.Equal(2, enabledJudges.Count);
        Assert.Contains(enabledJudges, j => j.JudgeId == "JudgeA");
        Assert.Contains(enabledJudges, j => j.JudgeId == "JudgeC");
        Assert.DoesNotContain(enabledJudges, j => j.JudgeId == "JudgeB");
    }

    #endregion

    #region SectionName Tests

    [Fact]
    public void EnsembleScoringOptions_SectionName_IsCorrect()
    {
        Assert.Equal("EnsembleScoring", EnsembleScoringOptions.SectionName);
    }

    #endregion
}

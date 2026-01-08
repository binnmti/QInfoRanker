using Microsoft.Extensions.Options;
using QInfoRanker.Core.Entities;
using QInfoRanker.Infrastructure.Scoring;
using Xunit.Abstractions;

namespace QInfoRanker.Tests.Unit;

/// <summary>
/// スコアリングロジックのユニットテスト
/// 外部API呼び出しなし、CI/CDで高速実行可能
/// </summary>
public class ScoringUnitTests
{
    private readonly ITestOutputHelper _output;
    private readonly ScoringService _scoringService;

    public ScoringUnitTests(ITestOutputHelper output)
    {
        _output = output;

        // Azure OpenAI設定なしでScoringServiceを作成（スコア計算ロジックのみテスト）
        var openAIOptions = Options.Create(new AzureOpenAIOptions());
        var scoringOptions = Options.Create(new ScoringOptions());
        var batchOptions = Options.Create(new BatchScoringOptions());
        var ensembleOptions = Options.Create(new EnsembleScoringOptions());

        _scoringService = new ScoringService(
            openAIOptions, scoringOptions, batchOptions, ensembleOptions,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ScoringService>.Instance);
    }

    #region CalculateFinalScore Tests

    [Fact]
    public void CalculateFinalScore_WithAllScores_ReturnsCorrectSum()
    {
        // Arrange - 5項目×20点 = 100点満点
        var article = new Article
        {
            TechnicalScore = 15,
            NoveltyScore = 12,
            ImpactScore = 18,
            QualityScore = 16,
            RelevanceScore = 8  // × 2 = 16
        };

        var source = new Source { Name = "Test" };

        // Act
        var finalScore = _scoringService.CalculateFinalScore(article, source);

        // Assert
        // 15 + 12 + 18 + 16 + (8 × 2) = 77
        _output.WriteLine($"Technical: {article.TechnicalScore}");
        _output.WriteLine($"Novelty: {article.NoveltyScore}");
        _output.WriteLine($"Impact: {article.ImpactScore}");
        _output.WriteLine($"Quality: {article.QualityScore}");
        _output.WriteLine($"Relevance: {article.RelevanceScore} × 2 = {article.RelevanceScore * 2}");
        _output.WriteLine($"Final Score: {finalScore}");

        Assert.Equal(77, finalScore);
    }

    [Fact]
    public void CalculateFinalScore_WithMaxScores_Returns100()
    {
        // Arrange - 全項目満点
        var article = new Article
        {
            TechnicalScore = 20,
            NoveltyScore = 20,
            ImpactScore = 20,
            QualityScore = 20,
            RelevanceScore = 10  // × 2 = 20
        };

        var source = new Source { Name = "Test" };

        // Act
        var finalScore = _scoringService.CalculateFinalScore(article, source);

        // Assert
        Assert.Equal(100, finalScore);
    }

    [Fact]
    public void CalculateFinalScore_WithZeroScores_ReturnsDefaultRelevance()
    {
        // Arrange - スコアなし（デフォルト関連性5×2=10）
        var article = new Article();
        var source = new Source { Name = "Test" };

        // Act
        var finalScore = _scoringService.CalculateFinalScore(article, source);

        // Assert - デフォルトRelevance(5) × 2 = 10
        Assert.Equal(10, finalScore);
    }

    [Fact]
    public void CalculateFinalScore_WithNullScores_HandlesGracefully()
    {
        // Arrange
        var article = new Article
        {
            TechnicalScore = null,
            NoveltyScore = 10,
            ImpactScore = null,
            QualityScore = 15,
            RelevanceScore = null  // デフォルト5
        };

        var source = new Source { Name = "Test" };

        // Act
        var finalScore = _scoringService.CalculateFinalScore(article, source);

        // Assert - 0 + 10 + 0 + 15 + (5 × 2) = 35
        Assert.Equal(35, finalScore);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]      // 全て0 → デフォルト関連性なし → 0
    [InlineData(20, 20, 20, 20, 10, 100)] // 満点
    [InlineData(10, 10, 10, 10, 5, 50)]   // 中央値
    [InlineData(5, 5, 5, 5, 10, 40)]      // 低品質、高関連性
    public void CalculateFinalScore_VariousScenarios(
        int technical, int novelty, int impact, int quality, int relevance, double expected)
    {
        // Arrange
        var article = new Article
        {
            TechnicalScore = technical,
            NoveltyScore = novelty,
            ImpactScore = impact,
            QualityScore = quality,
            RelevanceScore = relevance
        };

        var source = new Source { Name = "Test" };

        // Act
        var finalScore = _scoringService.CalculateFinalScore(article, source);

        // Assert
        Assert.Equal(expected, finalScore);
    }

    [Fact]
    public void CalculateFinalScore_WithStage2Relevance_UsesStage2Score()
    {
        // Arrange - EnsembleRelevanceScoreが設定されている場合はそれを使用
        var article = new Article
        {
            TechnicalScore = 15,
            NoveltyScore = 12,
            ImpactScore = 18,
            QualityScore = 16,
            RelevanceScore = 8,        // Stage 1の値（使用されない）
            EnsembleRelevanceScore = 14  // Stage 2の値（こちらを使用）
        };

        var source = new Source { Name = "Test" };

        // Act
        var finalScore = _scoringService.CalculateFinalScore(article, source);

        // Assert
        // 15 + 12 + 18 + 16 + 14 = 75
        _output.WriteLine($"EnsembleRelevanceScore: {article.EnsembleRelevanceScore}");
        _output.WriteLine($"Final Score: {finalScore}");

        Assert.Equal(75, finalScore);
    }

    [Fact]
    public void CalculateFinalScore_WithoutStage2Relevance_UsesRelevanceScoreDoubled()
    {
        // Arrange - EnsembleRelevanceScoreがnullの場合はRelevanceScore × 2を使用
        var article = new Article
        {
            TechnicalScore = 15,
            NoveltyScore = 12,
            ImpactScore = 18,
            QualityScore = 16,
            RelevanceScore = 8,        // × 2 = 16
            EnsembleRelevanceScore = null
        };

        var source = new Source { Name = "Test" };

        // Act
        var finalScore = _scoringService.CalculateFinalScore(article, source);

        // Assert
        // 15 + 12 + 18 + 16 + (8 × 2) = 77
        Assert.Equal(77, finalScore);
    }

    #endregion

    #region NormalizeNativeScore Tests

    [Fact]
    public void NormalizeNativeScore_WithZero_ReturnsZero()
    {
        var score = _scoringService.NormalizeNativeScore(0, "Qiita");
        Assert.Equal(0, score);
    }

    [Fact]
    public void NormalizeNativeScore_WithNull_ReturnsZero()
    {
        var score = _scoringService.NormalizeNativeScore(null, "Qiita");
        Assert.Equal(0, score);
    }

    [Theory]
    [InlineData(10, "Qiita")]
    [InlineData(100, "Qiita")]
    [InlineData(1000, "Qiita")]
    public void NormalizeNativeScore_ReturnsPositiveValue(int nativeScore, string sourceName)
    {
        var score = _scoringService.NormalizeNativeScore(nativeScore, sourceName);

        _output.WriteLine($"NativeScore {nativeScore} → Normalized: {score:F2}");

        Assert.True(score > 0, "正のスコアを返すべき");
        Assert.True(score <= 100, "100以下であるべき");
    }

    [Fact]
    public void NormalizeNativeScore_IsMonotonicallyIncreasing()
    {
        // より大きいNativeScoreはより大きい正規化スコアになるべき
        var score10 = _scoringService.NormalizeNativeScore(10, "Qiita");
        var score100 = _scoringService.NormalizeNativeScore(100, "Qiita");
        var score1000 = _scoringService.NormalizeNativeScore(1000, "Qiita");

        _output.WriteLine($"10 → {score10:F2}");
        _output.WriteLine($"100 → {score100:F2}");
        _output.WriteLine($"1000 → {score1000:F2}");

        Assert.True(score10 < score100, "10 < 100");
        Assert.True(score100 < score1000, "100 < 1000");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CalculateFinalScore_ExceedsMax_ClampedTo100()
    {
        // Arrange - スコアが範囲外でも100に収まる
        var article = new Article
        {
            TechnicalScore = 25,  // 20を超えている
            NoveltyScore = 25,
            ImpactScore = 25,
            QualityScore = 25,
            RelevanceScore = 15   // 10を超えている → 30
        };

        var source = new Source { Name = "Test" };

        // Act
        var finalScore = _scoringService.CalculateFinalScore(article, source);

        // Assert - 100にクランプされる
        Assert.Equal(100, finalScore);
    }

    [Fact]
    public void CalculateFinalScore_NegativeScores_ClampedToZero()
    {
        // Arrange - 負のスコアは想定外だが、0未満にはならない
        var article = new Article
        {
            TechnicalScore = -10,
            NoveltyScore = -5,
            ImpactScore = 0,
            QualityScore = 0,
            RelevanceScore = -5
        };

        var source = new Source { Name = "Test" };

        // Act
        var finalScore = _scoringService.CalculateFinalScore(article, source);

        // Assert - 0にクランプされる
        Assert.True(finalScore >= 0, "スコアは0以上であるべき");
    }

    #endregion
}

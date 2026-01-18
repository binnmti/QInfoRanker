using QInfoRanker.Core.Entities;

namespace QInfoRanker.Tests.Unit;

/// <summary>
/// キーワードのSlug生成ロジックのユニットテスト
/// </summary>
public class KeywordSlugTests
{
    #region GenerateSlug Tests

    [Theory]
    [InlineData("quantum computing", "quantum-computing")]
    [InlineData("Quantum Computing", "quantum-computing")]
    [InlineData("QUANTUM COMPUTING", "quantum-computing")]
    [InlineData("machine-learning", "machine-learning")]
    [InlineData("Machine Learning", "machine-learning")]
    [InlineData("AI/ML", "aiml")]
    [InlineData("C#", "c")]
    [InlineData("  spaces  ", "spaces")]
    [InlineData("multiple   spaces", "multiple-spaces")]
    [InlineData("test_underscore", "test-underscore")]
    public void GenerateSlug_WithEnglishText_ReturnsSlug(string input, string expected)
    {
        // Act
        var slug = Keyword.GenerateSlug(input);

        // Assert
        Assert.Equal(expected, slug);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("量子コンピュータ")]
    [InlineData("日本語のみ")]
    public void GenerateSlug_WithInvalidOrJapaneseText_ReturnsNull(string? input)
    {
        // Act
        var slug = Keyword.GenerateSlug(input);

        // Assert
        Assert.Null(slug);
    }

    #endregion

    #region GenerateSlugFromAliases Tests

    [Fact]
    public void GenerateSlugFromAliases_WithEnglishAlias_ReturnsSlug()
    {
        // Arrange
        var keyword = new Keyword
        {
            Term = "量子コンピュータ",
            Aliases = "quantum computer, quantum computing"
        };

        // Act
        var slug = keyword.GenerateSlugFromAliases();

        // Assert
        Assert.Equal("quantum-computer", slug);
    }

    [Fact]
    public void GenerateSlugFromAliases_WithMultipleAliases_UsesFirstEnglishOne()
    {
        // Arrange
        var keyword = new Keyword
        {
            Term = "機械学習",
            Aliases = "machine learning, ML, deep learning"
        };

        // Act
        var slug = keyword.GenerateSlugFromAliases();

        // Assert
        Assert.Equal("machine-learning", slug);
    }

    [Fact]
    public void GenerateSlugFromAliases_WithNoEnglishAlias_ReturnsNull()
    {
        // Arrange
        var keyword = new Keyword
        {
            Term = "量子コンピュータ",
            Aliases = "量子計算機, クォンタム"
        };

        // Act
        var slug = keyword.GenerateSlugFromAliases();

        // Assert
        Assert.Null(slug);
    }

    [Fact]
    public void GenerateSlugFromAliases_WithNoAliases_ReturnsNull()
    {
        // Arrange
        var keyword = new Keyword
        {
            Term = "量子コンピュータ",
            Aliases = null
        };

        // Act
        var slug = keyword.GenerateSlugFromAliases();

        // Assert
        Assert.Null(slug);
    }

    [Fact]
    public void GenerateSlugFromAliases_WithEmptyAliases_ReturnsNull()
    {
        // Arrange
        var keyword = new Keyword
        {
            Term = "量子コンピュータ",
            Aliases = ""
        };

        // Act
        var slug = keyword.GenerateSlugFromAliases();

        // Assert
        Assert.Null(slug);
    }

    #endregion

    #region GetUrlIdentifier Tests

    [Fact]
    public void GetUrlIdentifier_WithSlug_ReturnsSlug()
    {
        // Arrange
        var keyword = new Keyword
        {
            Id = 42,
            Term = "量子コンピュータ",
            Slug = "quantum-computer"
        };

        // Act
        var identifier = keyword.GetUrlIdentifier();

        // Assert
        Assert.Equal("quantum-computer", identifier);
    }

    [Fact]
    public void GetUrlIdentifier_WithoutSlug_ReturnsId()
    {
        // Arrange
        var keyword = new Keyword
        {
            Id = 42,
            Term = "量子コンピュータ",
            Slug = null
        };

        // Act
        var identifier = keyword.GetUrlIdentifier();

        // Assert
        Assert.Equal("42", identifier);
    }

    #endregion
}

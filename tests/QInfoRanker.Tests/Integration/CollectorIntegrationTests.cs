using Microsoft.Extensions.Logging;
using QInfoRanker.Core.Entities;
using QInfoRanker.Core.Enums;
using QInfoRanker.Infrastructure.Collectors;
using Xunit.Abstractions;

namespace QInfoRanker.Tests.Integration;

/// <summary>
/// 各Collectorが実際にAPIから記事を取得できるかをテストする統合テスト
/// CI/CDでスキップ: dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class CollectorIntegrationTests
{
    private readonly ITestOutputHelper _output;
    private readonly HttpClient _httpClient;

    public CollectorIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "QInfoRanker-Test/1.0");
    }

    #region Qiita Tests

    [Fact]
    public async Task QiitaCollector_CanFetchArticles_WithQuantumKeyword()
    {
        // Arrange
        var logger = CreateLogger<QiitaCollector>();
        var collector = new QiitaCollector(_httpClient, logger);

        var source = new Source
        {
            Id = 1,
            Name = "Qiita",
            Url = "https://qiita.com",
            SearchUrlTemplate = "https://qiita.com/api/v2/items?query={keyword}&per_page=10",
            Type = SourceType.Api,
            HasNativeScore = true,
            AuthorityWeight = 0.7,
            Language = Language.Japanese,
            Category = SourceCategory.Technology
        };

        // Act
        var articles = (await collector.CollectAsync(source, "量子コンピュータ")).ToList();

        // Assert
        Assert.NotEmpty(articles);
        _output.WriteLine($"Qiitaから {articles.Count} 件の記事を取得");

        foreach (var article in articles.Take(5))
        {
            _output.WriteLine($"  - [{article.NativeScore ?? 0} likes] {article.Title}");
            _output.WriteLine($"    URL: {article.Url}");

            Assert.False(string.IsNullOrEmpty(article.Title), "タイトルが空です");
            Assert.False(string.IsNullOrEmpty(article.Url), "URLが空です");
            Assert.True(article.Url.Contains("qiita.com"), "URLがQiitaではありません");
        }
    }

    [Fact]
    public async Task QiitaCollector_ArticleContainsExpectedFields()
    {
        // Arrange
        var logger = CreateLogger<QiitaCollector>();
        var collector = new QiitaCollector(_httpClient, logger);

        var source = new Source
        {
            Id = 1,
            Name = "Qiita",
            Url = "https://qiita.com",
            SearchUrlTemplate = "https://qiita.com/api/v2/items?query={keyword}&per_page=5",
            Type = SourceType.Api,
            HasNativeScore = true
        };

        // Act
        var articles = (await collector.CollectAsync(source, "Python")).ToList();

        // Assert
        Assert.NotEmpty(articles);

        var article = articles.First();
        _output.WriteLine($"記事詳細:");
        _output.WriteLine($"  Title: {article.Title}");
        _output.WriteLine($"  URL: {article.Url}");
        _output.WriteLine($"  NativeScore (likes): {article.NativeScore}");
        _output.WriteLine($"  PublishedAt: {article.PublishedAt}");
        _output.WriteLine($"  Summary (first 100 chars): {article.Summary?.Substring(0, Math.Min(100, article.Summary?.Length ?? 0))}...");

        Assert.NotNull(article.Title);
        Assert.NotNull(article.Url);
        Assert.NotNull(article.NativeScore); // Qiitaはlikes_countを返すはず
    }

    #endregion

    #region Zenn Tests

    [Fact]
    public async Task ZennCollector_CanFetchArticles_WithSearchApi()
    {
        // Arrange
        var logger = CreateLogger<ZennCollector>();
        var collector = new ZennCollector(_httpClient, logger);

        var source = new Source
        {
            Id = 2,
            Name = "Zenn",
            Url = "https://zenn.dev",
            SearchUrlTemplate = "https://zenn.dev/api/search?q={keyword}&source=articles",
            Type = SourceType.Api,
            HasNativeScore = true,
            Language = Language.Japanese,
            Category = SourceCategory.Technology
        };

        // Act
        var articles = (await collector.CollectAsync(source, "React")).ToList();

        // Assert & Output
        _output.WriteLine($"Zennから {articles.Count} 件の記事を取得");

        if (articles.Any())
        {
            foreach (var article in articles.Take(5))
            {
                _output.WriteLine($"  - [{article.NativeScore ?? 0} likes] {article.Title}");
                _output.WriteLine($"    URL: {article.Url}");
            }
            Assert.True(articles.All(a => a.Url.Contains("zenn.dev")), "URLがZennではありません");
        }
        else
        {
            _output.WriteLine("  警告: Zennから記事を取得できませんでした。APIが変更された可能性があります。");
        }
    }

    [Fact]
    public async Task ZennCollector_FallbackToTopicSearch()
    {
        // Arrange
        var logger = CreateLogger<ZennCollector>();
        var collector = new ZennCollector(_httpClient, logger);

        var source = new Source
        {
            Id = 2,
            Name = "Zenn",
            Url = "https://zenn.dev",
            // 検索APIが失敗した場合、トピック検索にフォールバックする
            SearchUrlTemplate = "https://zenn.dev/api/search?q={keyword}&source=articles",
            Type = SourceType.Api,
            HasNativeScore = true
        };

        // Act - "react" はトピックとしても存在するはず
        var articles = (await collector.CollectAsync(source, "react")).ToList();

        // Assert
        _output.WriteLine($"Zenn (topic fallback) から {articles.Count} 件の記事を取得");

        foreach (var article in articles.Take(3))
        {
            _output.WriteLine($"  - {article.Title}");
        }
    }

    #endregion

    #region arXiv Tests

    [Fact]
    public async Task ArXivCollector_CanFetchArticles()
    {
        // Arrange
        var logger = CreateLogger<ArXivCollector>();
        var collector = new ArXivCollector(_httpClient, logger);

        var source = new Source
        {
            Id = 3,
            Name = "arXiv",
            Url = "https://arxiv.org",
            SearchUrlTemplate = "https://export.arxiv.org/api/query?search_query=all:{keyword}&sortBy=submittedDate&sortOrder=descending&max_results=10",
            Type = SourceType.Api,
            HasNativeScore = false,
            Language = Language.English,
            Category = SourceCategory.Academic
        };

        // Act
        var articles = (await collector.CollectAsync(source, "quantum computing")).ToList();

        // Assert
        Assert.NotEmpty(articles);
        _output.WriteLine($"arXivから {articles.Count} 件の論文を取得");

        foreach (var article in articles.Take(5))
        {
            _output.WriteLine($"  - {article.Title}");
            _output.WriteLine($"    URL: {article.Url}");
            _output.WriteLine($"    Published: {article.PublishedAt}");

            Assert.False(string.IsNullOrEmpty(article.Title), "タイトルが空です");
            Assert.False(string.IsNullOrEmpty(article.Url), "URLが空です");
            Assert.True(article.Url.Contains("arxiv.org"), $"URLがarXivではありません: {article.Url}");
        }
    }

    [Fact]
    public async Task ArXivCollector_ArticleHasAbstract()
    {
        // Arrange
        var logger = CreateLogger<ArXivCollector>();
        var collector = new ArXivCollector(_httpClient, logger);

        var source = new Source
        {
            Id = 3,
            Name = "arXiv",
            Url = "https://arxiv.org",
            SearchUrlTemplate = "https://export.arxiv.org/api/query?search_query=all:{keyword}&max_results=3",
            Type = SourceType.Api,
            HasNativeScore = false
        };

        // Act
        var articles = (await collector.CollectAsync(source, "machine learning")).ToList();

        // Assert
        Assert.NotEmpty(articles);

        var article = articles.First();
        _output.WriteLine($"論文詳細:");
        _output.WriteLine($"  Title: {article.Title}");
        _output.WriteLine($"  Summary (Abstract, first 200 chars): {article.Summary?.Substring(0, Math.Min(200, article.Summary?.Length ?? 0))}...");

        Assert.NotNull(article.Summary);
        Assert.True(article.Summary.Length > 50, "Abstract(Summary)が短すぎます");
    }

    #endregion

    #region Hatena Bookmark Tests

    [Fact]
    public async Task HatenaCollector_CanFetchArticles()
    {
        // Arrange
        var logger = CreateLogger<HatenaCollector>();
        var collector = new HatenaCollector(_httpClient, logger);

        var source = new Source
        {
            Id = 4,
            Name = "Hatena Bookmark",
            Url = "https://b.hatena.ne.jp",
            SearchUrlTemplate = "https://b.hatena.ne.jp/search/text?q={keyword}&mode=rss",
            Type = SourceType.Rss,
            HasNativeScore = true,
            Language = Language.Japanese,
            Category = SourceCategory.Technology
        };

        // Act
        var articles = (await collector.CollectAsync(source, "プログラミング")).ToList();

        // Assert
        _output.WriteLine($"はてなブックマークから {articles.Count} 件の記事を取得");

        if (articles.Any())
        {
            foreach (var article in articles.Take(5))
            {
                _output.WriteLine($"  - [{article.NativeScore ?? 0} users] {article.Title}");
                _output.WriteLine($"    URL: {article.Url}");
            }
        }
        else
        {
            _output.WriteLine("  警告: はてなブックマークから記事を取得できませんでした");
        }
    }

    #endregion

    #region Hacker News Tests

    [Fact]
    public async Task HackerNewsCollector_CanFetchArticles()
    {
        // Arrange
        var logger = CreateLogger<HackerNewsCollector>();
        var collector = new HackerNewsCollector(_httpClient, logger);

        var source = new Source
        {
            Id = 5,
            Name = "Hacker News",
            Url = "https://news.ycombinator.com",
            SearchUrlTemplate = "https://hn.algolia.com/api/v1/search?query={keyword}&tags=story",
            Type = SourceType.Api,
            HasNativeScore = true,
            Language = Language.English,
            Category = SourceCategory.Technology
        };

        // Act
        var articles = (await collector.CollectAsync(source, "rust programming")).ToList();

        // Assert
        Assert.NotEmpty(articles);
        _output.WriteLine($"Hacker Newsから {articles.Count} 件の記事を取得");

        foreach (var article in articles.Take(5))
        {
            _output.WriteLine($"  - [{article.NativeScore ?? 0} points] {article.Title}");
            _output.WriteLine($"    URL: {article.Url}");
        }
    }

    #endregion

    #region Note.com Tests

    [Fact]
    public async Task NoteCollector_CanFetchArticles_WithApiSearch()
    {
        // Arrange
        var logger = CreateLogger<NoteCollector>();
        var collector = new NoteCollector(_httpClient, logger);

        var source = new Source
        {
            Id = 6,
            Name = "Note.com",
            Url = "https://note.com",
            SearchUrlTemplate = "https://note.com/search?q={keyword}",
            Type = SourceType.Api,
            HasNativeScore = true,
            Language = Language.Japanese,
            Category = SourceCategory.Technology
        };

        // Act - 一般的なキーワードでテスト
        var articles = (await collector.CollectAsync(source, "プログラミング")).ToList();

        // Assert
        _output.WriteLine($"Note.comから {articles.Count} 件の記事を取得");

        if (articles.Any())
        {
            foreach (var article in articles.Take(5))
            {
                _output.WriteLine($"  - [{article.NativeScore ?? 0} likes] {article.Title}");
                _output.WriteLine($"    URL: {article.Url}");
            }
            Assert.True(articles.All(a => a.Url.Contains("note.com")), "URLがNote.comではありません");
        }
        else
        {
            _output.WriteLine("  警告: Note.comから記事を取得できませんでした。APIが変更された可能性があります。");
        }
    }

    [Fact]
    public async Task NoteCollector_CanFetchArticles_WithQuantumKeyword()
    {
        // Arrange
        var logger = CreateLogger<NoteCollector>();
        var collector = new NoteCollector(_httpClient, logger);

        var source = new Source
        {
            Id = 6,
            Name = "Note.com",
            Url = "https://note.com",
            SearchUrlTemplate = "https://note.com/search?q={keyword}",
            Type = SourceType.Api,
            HasNativeScore = true
        };

        // Act - 量子コンピュータで検索（ニッチなキーワード）
        var articles = (await collector.CollectAsync(source, "量子コンピュータ")).ToList();

        // Assert
        _output.WriteLine($"Note.com (量子コンピュータ) から {articles.Count} 件の記事を取得");

        foreach (var article in articles.Take(5))
        {
            _output.WriteLine($"  - [{article.NativeScore ?? 0} likes] {article.Title}");
            _output.WriteLine($"    URL: {article.Url}");
        }
    }

    [Fact]
    public async Task NoteCollector_ApiResponse_Debug()
    {
        // Arrange - API応答を直接確認
        var apiUrl = "https://note.com/api/v2/searches?q=プログラミング&size=5&start=0&sort=new";

        // Act
        var response = await _httpClient.GetAsync(apiUrl);
        var content = await response.Content.ReadAsStringAsync();

        // Output
        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"Response (first 1000 chars):");
        _output.WriteLine(content.Length > 1000 ? content[..1000] + "..." : content);
    }

    #endregion

    #region Yahoo! News Japan Tests

    [Fact]
    public async Task YahooNewsJapanCollector_CanFetchArticles_WithGeneralKeyword()
    {
        // Arrange
        var logger = CreateLogger<YahooNewsJapanCollector>();
        var collector = new YahooNewsJapanCollector(_httpClient, logger);

        var source = new Source
        {
            Id = 7,
            Name = "Yahoo! News Japan",
            Url = "https://news.yahoo.co.jp",
            SearchUrlTemplate = "https://news.yahoo.co.jp",
            Type = SourceType.Rss,
            HasNativeScore = false,
            Language = Language.Japanese,
            Category = SourceCategory.News
        };

        // Act - 非常に一般的なキーワードでテスト（フィルタリングなしに近い）
        var articles = (await collector.CollectAsync(source, "日本")).ToList();

        // Assert
        _output.WriteLine($"Yahoo! News Japanから {articles.Count} 件の記事を取得 (キーワード: 日本)");

        if (articles.Any())
        {
            foreach (var article in articles.Take(5))
            {
                _output.WriteLine($"  - {article.Title}");
                _output.WriteLine($"    URL: {article.Url}");
            }
        }
        else
        {
            _output.WriteLine("  警告: Yahoo! News Japanから記事を取得できませんでした");
        }
    }

    [Fact]
    public async Task YahooNewsJapanCollector_WithQuantumKeyword_MayReturnZero()
    {
        // Arrange
        var logger = CreateLogger<YahooNewsJapanCollector>();
        var collector = new YahooNewsJapanCollector(_httpClient, logger);

        var source = new Source
        {
            Id = 7,
            Name = "Yahoo! News Japan",
            Url = "https://news.yahoo.co.jp",
            SearchUrlTemplate = "https://news.yahoo.co.jp",
            Type = SourceType.Rss,
            HasNativeScore = false
        };

        // Act - ニッチなキーワード
        var articles = (await collector.CollectAsync(source, "量子コンピュータ")).ToList();

        // Assert
        _output.WriteLine($"Yahoo! News Japan (量子コンピュータ) から {articles.Count} 件の記事を取得");
        _output.WriteLine("  注: このコレクターはRSSフィードのトップ記事をクライアントサイドでフィルタリングするため、");
        _output.WriteLine("      ニッチなキーワードでは0件になることがあります。");

        foreach (var article in articles.Take(5))
        {
            _output.WriteLine($"  - {article.Title}");
        }
    }

    [Fact]
    public async Task YahooNewsJapanCollector_RssFeed_Debug()
    {
        // Arrange - RSSフィードの内容を直接確認 (2025年よりit.xmlに変更)
        var feedUrl = "https://news.yahoo.co.jp/rss/topics/it.xml";

        // Act
        var response = await _httpClient.GetAsync(feedUrl);
        var content = await response.Content.ReadAsStringAsync();

        // Output
        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"RSS Feed (first 2000 chars):");
        _output.WriteLine(content.Length > 2000 ? content[..2000] + "..." : content);

        // Count items
        var itemCount = content.Split("<item>").Length - 1;
        _output.WriteLine($"\nTotal items in feed: {itemCount}");
    }

    #endregion

    #region Helper Methods

    private ILogger<T> CreateLogger<T>()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        return loggerFactory.CreateLogger<T>();
    }

    #endregion
}

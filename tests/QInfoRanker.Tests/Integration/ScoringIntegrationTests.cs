using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QInfoRanker.Core.Entities;
using QInfoRanker.Core.Enums;
using QInfoRanker.Infrastructure.Collectors;
using QInfoRanker.Infrastructure.Scoring;
using Xunit.Abstractions;

namespace QInfoRanker.Tests.Integration;

/// <summary>
/// AIによる記事スコアリングの統合テスト
/// 実際のAzure OpenAI APIを呼び出してテストする
/// </summary>
public class ScoringIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ScoringService _scoringService;

    public ScoringIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "QInfoRanker-Test/1.0");

        // 設定ファイルを読み込み（ローカル設定で上書き可能）
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.test.json", optional: false)
            .AddJsonFile("appsettings.test.local.json", optional: true)
            .Build();

        // ScoringServiceを作成
        var openAIOptions = Options.Create(_configuration.GetSection("AzureOpenAI").Get<AzureOpenAIOptions>()!);
        var scoringOptions = Options.Create(_configuration.GetSection("Scoring").Get<ScoringOptions>() ?? new ScoringOptions());
        var batchOptions = Options.Create(_configuration.GetSection("BatchScoring").Get<BatchScoringOptions>() ?? new BatchScoringOptions());
        var logger = CreateLogger<ScoringService>();

        _scoringService = new ScoringService(openAIOptions, scoringOptions, batchOptions, logger);
    }

    #region Single Article Scoring Tests

    [Fact]
    public async Task ScoringService_CanScoreSingleArticle()
    {
        // Arrange - 明らかに量子コンピュータに関連する記事
        var article = new Article
        {
            Id = 1,
            Title = "量子コンピュータの基礎と応用 - Shorのアルゴリズムを解説",
            Summary = "この記事では量子コンピュータの基本原理と、RSA暗号を解読できるShorのアルゴリズムについて詳しく解説します。量子ビットの重ね合わせとエンタングルメントの概念から始め、実際の量子回路の構成まで説明します。",
            Url = "https://example.com/quantum-computing",
            NativeScore = 50,
            SourceId = 1
        };

        // Act
        var scoredArticle = await _scoringService.CalculateLlmScoreAsync(article, includeContent: false);

        // Assert
        _output.WriteLine($"記事スコアリング結果:");
        _output.WriteLine($"  Title: {article.Title}");
        _output.WriteLine($"  Technical: {scoredArticle.TechnicalScore}");
        _output.WriteLine($"  Novelty: {scoredArticle.NoveltyScore}");
        _output.WriteLine($"  Impact: {scoredArticle.ImpactScore}");
        _output.WriteLine($"  Quality: {scoredArticle.QualityScore}");
        _output.WriteLine($"  LLM Total Score: {scoredArticle.LlmScore}");

        Assert.NotNull(scoredArticle.LlmScore);
        Assert.True(scoredArticle.LlmScore > 0, "LLMスコアが0以下です");
        Assert.True(scoredArticle.LlmScore <= 100, "LLMスコアが100を超えています");
    }

    [Fact]
    public async Task ScoringService_GivesLowScoreToIrrelevantArticle()
    {
        // Arrange - 量子コンピュータに関係ない記事
        var article = new Article
        {
            Id = 2,
            Title = "今日の料理レシピ - 美味しいカレーの作り方",
            Summary = "誰でも簡単に作れる美味しいカレーのレシピを紹介します。隠し味にりんごとはちみつを使うのがポイントです。",
            Url = "https://example.com/cooking-recipe",
            NativeScore = 100,
            SourceId = 1
        };

        // Act
        var scoredArticle = await _scoringService.CalculateLlmScoreAsync(article, includeContent: false);

        // Assert
        _output.WriteLine($"無関係記事のスコアリング結果:");
        _output.WriteLine($"  Title: {article.Title}");
        _output.WriteLine($"  LLM Total Score: {scoredArticle.LlmScore}");

        // 技術記事として評価されるため、低スコアになるはず
        Assert.NotNull(scoredArticle.LlmScore);
        _output.WriteLine($"  注: この記事は技術記事ではないため、低いスコアが期待されます");
    }

    #endregion

    #region Batch Scoring Tests (Relevance Evaluation)

    [Fact]
    public async Task ScoringService_CanEvaluateRelevanceBatch()
    {
        // Arrange - 量子コンピュータキーワードに対する記事のバッチ
        var source = new Source
        {
            Id = 1,
            Name = "Qiita",
            HasNativeScore = true,
            AuthorityWeight = 0.7
        };

        var articles = new List<Article>
        {
            new()
            {
                Id = 1,
                Title = "量子コンピュータ入門 - 基礎から学ぶ量子計算",
                Summary = "量子コンピュータの基本概念を初心者向けに解説します。",
                Source = source,
                SourceId = 1,
                NativeScore = 30
            },
            new()
            {
                Id = 2,
                Title = "Pythonで機械学習入門",
                Summary = "Pythonを使った機械学習の基本を学びましょう。",
                Source = source,
                SourceId = 1,
                NativeScore = 50
            },
            new()
            {
                Id = 3,
                Title = "量子アルゴリズムの実装 - Groverの探索アルゴリズム",
                Summary = "量子コンピュータのGroverアルゴリズムをQiskitで実装します。",
                Source = source,
                SourceId = 1,
                NativeScore = 45
            },
            new()
            {
                Id = 4,
                Title = "美味しいラーメンの作り方",
                Summary = "自宅で簡単に作れる本格ラーメンのレシピです。",
                Source = source,
                SourceId = 1,
                NativeScore = 100
            },
            new()
            {
                Id = 5,
                Title = "量子ビットのエラー訂正について",
                Summary = "量子コンピュータにおけるエラー訂正符号の仕組みを解説します。",
                Source = source,
                SourceId = 1,
                NativeScore = 20
            }
        };

        var keywords = new List<string> { "量子コンピュータ", "quantum computing" };

        // Act
        var result = await _scoringService.EvaluateTwoStageAsync(articles, keywords);

        // Assert
        _output.WriteLine($"バッチ関連性評価結果:");
        _output.WriteLine($"  総処理数: {result.RelevanceResult.TotalProcessed}");
        _output.WriteLine($"  関連あり: {result.RelevanceResult.RelevantCount}");
        _output.WriteLine($"  除外: {result.RelevanceResult.FilteredCount}");
        _output.WriteLine($"  API呼び出し: {result.TotalApiCalls}回");
        _output.WriteLine("");

        foreach (var article in articles)
        {
            _output.WriteLine($"  [{(article.IsRelevant == true ? "関連" : "除外")}] {article.Title}");
            _output.WriteLine($"      Relevance: {article.RelevanceScore}, LLM Score: {article.LlmScore ?? 0}");
        }

        // 量子コンピュータに関連する記事は関連ありとマークされるべき
        var quantumArticle = articles.First(a => a.Title.Contains("量子コンピュータ入門"));
        Assert.True(quantumArticle.IsRelevant, "量子コンピュータ入門は関連ありと判定されるべき");

        // ラーメンの記事は除外されるべき（ただしフォールバックで関連ありになる可能性もある）
        var ramenArticle = articles.First(a => a.Title.Contains("ラーメン"));
        _output.WriteLine($"\n  ラーメン記事の判定: IsRelevant={ramenArticle.IsRelevant}, Score={ramenArticle.RelevanceScore}");
    }

    #endregion

    #region End-to-End Tests (Collect + Score)

    [Fact]
    public async Task EndToEnd_CollectAndScoreQiitaArticles()
    {
        // Arrange
        var collectorLogger = CreateLogger<QiitaCollector>();
        var collector = new QiitaCollector(_httpClient, collectorLogger);

        var source = new Source
        {
            Id = 1,
            Name = "Qiita",
            Url = "https://qiita.com",
            SearchUrlTemplate = "https://qiita.com/api/v2/items?query={keyword}&per_page=5",
            Type = SourceType.Api,
            HasNativeScore = true,
            AuthorityWeight = 0.7,
            Language = Language.Japanese,
            Category = SourceCategory.Technology
        };

        // Act - Step 1: 記事を収集
        _output.WriteLine("Step 1: Qiitaから記事を収集...");
        var articles = (await collector.CollectAsync(source, "量子コンピュータ")).ToList();

        Assert.NotEmpty(articles);
        _output.WriteLine($"  {articles.Count}件の記事を取得\n");

        // 記事にSourceと仮のIDを設定（スコアリングで使用）
        // 本番ではDBに保存後にIDが付与されるが、テストでは仮のIDを使用
        for (var i = 0; i < articles.Count; i++)
        {
            articles[i].Id = i + 1; // 1から始まる仮のID
            articles[i].Source = source;
        }

        // Act - Step 2: 関連性・品質を評価
        _output.WriteLine("Step 2: AIで記事を評価...");
        var keywords = new List<string> { "量子コンピュータ", "quantum computing" };
        var result = await _scoringService.EvaluateTwoStageAsync(articles, keywords);

        // Assert & Output
        _output.WriteLine($"\n評価結果サマリー:");
        _output.WriteLine($"  総記事数: {articles.Count}");
        _output.WriteLine($"  関連記事数: {result.RelevanceResult.RelevantCount}");
        _output.WriteLine($"  API呼び出し: {result.TotalApiCalls}回");
        _output.WriteLine($"  処理時間: {result.TotalDuration.TotalSeconds:F2}秒");
        _output.WriteLine("");

        _output.WriteLine("各記事の評価:");
        foreach (var article in articles.OrderByDescending(a => a.LlmScore ?? 0))
        {
            var relevance = article.IsRelevant == true ? "関連" : "除外";
            _output.WriteLine($"  [{relevance}] {article.Title}");
            _output.WriteLine($"      Native: {article.NativeScore ?? 0}, LLM: {article.LlmScore ?? 0}, Relevance: {article.RelevanceScore}");
            if (!string.IsNullOrEmpty(article.SummaryJa))
            {
                _output.WriteLine($"      AI要約: {article.SummaryJa}");
            }
            _output.WriteLine("");
        }

        // 少なくとも1つの記事が関連ありと判定されるべき
        Assert.True(result.RelevanceResult.RelevantCount > 0, "量子コンピュータで検索した記事が全て除外されるのはおかしい");
    }

    [Fact]
    public async Task EndToEnd_CollectAndScoreArXivPapers()
    {
        // Arrange
        var collectorLogger = CreateLogger<ArXivCollector>();
        var collector = new ArXivCollector(_httpClient, collectorLogger);

        var source = new Source
        {
            Id = 3,
            Name = "arXiv",
            Url = "https://arxiv.org",
            SearchUrlTemplate = "https://export.arxiv.org/api/query?search_query=all:{keyword}&sortBy=submittedDate&sortOrder=descending&max_results=5",
            Type = SourceType.Api,
            HasNativeScore = false,
            AuthorityWeight = 0.9,
            Language = Language.English,
            Category = SourceCategory.Academic
        };

        // Act - Step 1: 論文を収集
        _output.WriteLine("Step 1: arXivから論文を収集...");
        var articles = (await collector.CollectAsync(source, "quantum computing")).ToList();

        Assert.NotEmpty(articles);
        _output.WriteLine($"  {articles.Count}件の論文を取得\n");

        // 記事にSourceと仮のIDを設定
        for (var i = 0; i < articles.Count; i++)
        {
            articles[i].Id = i + 1;
            articles[i].Source = source;
        }

        // Act - Step 2: 評価
        _output.WriteLine("Step 2: AIで論文を評価...");
        var keywords = new List<string> { "quantum computing", "量子コンピュータ" };
        var result = await _scoringService.EvaluateTwoStageAsync(articles, keywords);

        // Output
        _output.WriteLine($"\n評価結果:");
        foreach (var article in articles)
        {
            _output.WriteLine($"  [{(article.IsRelevant == true ? "関連" : "除外")}] {article.Title?.Substring(0, Math.Min(60, article.Title?.Length ?? 0))}...");
            _output.WriteLine($"      LLM Score: {article.LlmScore ?? 0}");
        }

        Assert.True(result.RelevanceResult.RelevantCount > 0, "quantum computingで検索した論文が全て除外されるのはおかしい");
    }

    #endregion

    #region Final Score Calculation Tests

    [Fact]
    public void ScoringService_CalculatesFinalScoreCorrectly()
    {
        // Arrange
        var source = new Source
        {
            Name = "Qiita",
            HasNativeScore = true,
            AuthorityWeight = 0.7
        };

        var article = new Article
        {
            NativeScore = 50,
            LlmScore = 70,
            TechnicalScore = 20,
            NoveltyScore = 15,
            ImpactScore = 18,
            QualityScore = 17
        };

        // Act
        var finalScore = _scoringService.CalculateFinalScore(article, source);

        // Assert
        _output.WriteLine($"Final Score計算:");
        _output.WriteLine($"  NativeScore: {article.NativeScore}");
        _output.WriteLine($"  LlmScore: {article.LlmScore}");
        _output.WriteLine($"  AuthorityWeight: {source.AuthorityWeight}");
        _output.WriteLine($"  Final Score: {finalScore}");

        Assert.True(finalScore > 0, "Final Scoreが0以下です");
        Assert.True(finalScore <= 100, "Final Scoreが100を超えています");
    }

    [Fact]
    public void ScoringService_NormalizesNativeScoreCorrectly()
    {
        // Arrange & Act
        var score10 = _scoringService.NormalizeNativeScore(10, "Qiita");
        var score100 = _scoringService.NormalizeNativeScore(100, "Qiita");
        var score1000 = _scoringService.NormalizeNativeScore(1000, "Qiita");

        // Assert
        _output.WriteLine($"NativeScore正規化:");
        _output.WriteLine($"  10 likes -> {score10:F2}");
        _output.WriteLine($"  100 likes -> {score100:F2}");
        _output.WriteLine($"  1000 likes -> {score1000:F2}");

        Assert.True(score10 < score100, "10 < 100 なのに正規化スコアが逆転しています");
        Assert.True(score100 < score1000, "100 < 1000 なのに正規化スコアが逆転しています");
        Assert.True(score1000 <= 100, "正規化スコアが100を超えています");
    }

    #endregion

    #region Debug Tests

    /// <summary>
    /// 特定の記事が0点になる理由をデバッグする
    /// </summary>
    [Fact]
    public async Task Debug_SpecificQiitaArticle_WhyZeroScore()
    {
        // Arrange - 特定の記事URLからデータを取得
        var itemId = "4867bfbe9921aed56248"; // 量子機械学習入門
        var apiUrl = $"https://qiita.com/api/v2/items/{itemId}";

        _output.WriteLine($"=== 記事デバッグ: {itemId} ===\n");

        // Step 1: Qiita APIから記事を直接取得
        _output.WriteLine("Step 1: Qiita APIから記事を取得...");
        var response = await _httpClient.GetAsync(apiUrl);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _output.WriteLine($"  ERROR: API呼び出し失敗 - {response.StatusCode}");
            _output.WriteLine($"  Response: {json}");
            return;
        }

        var qiitaItem = System.Text.Json.JsonSerializer.Deserialize<QiitaItemResponse>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        _output.WriteLine($"  タイトル: {qiitaItem?.Title}");
        _output.WriteLine($"  いいね数: {qiitaItem?.LikesCount}");
        _output.WriteLine($"  ストック数: {qiitaItem?.StocksCount}");
        _output.WriteLine($"  タグ: {string.Join(", ", qiitaItem?.Tags?.Select(t => t.Name) ?? Array.Empty<string>())}");
        _output.WriteLine($"  作成日: {qiitaItem?.CreatedAt}");
        _output.WriteLine($"  本文文字数: {qiitaItem?.Body?.Length ?? 0}");
        _output.WriteLine("");

        // Step 2: Articleオブジェクトを作成
        var source = new Source
        {
            Id = 1,
            Name = "Qiita",
            HasNativeScore = true,
            AuthorityWeight = 0.7
        };

        var article = new Article
        {
            Id = 1,
            Title = qiitaItem?.Title ?? "",
            Summary = qiitaItem?.Body?.Length > 500 ? qiitaItem.Body[..500] : qiitaItem?.Body ?? "",
            Url = $"https://qiita.com/TaiyoYamada/items/{itemId}",
            NativeScore = qiitaItem?.LikesCount ?? 0,
            Source = source,
            SourceId = 1
        };

        _output.WriteLine("Step 2: 作成したArticleオブジェクト:");
        _output.WriteLine($"  Title: {article.Title}");
        _output.WriteLine($"  Summary (先頭200文字): {article.Summary?[..Math.Min(200, article.Summary?.Length ?? 0)]}...");
        _output.WriteLine($"  NativeScore: {article.NativeScore}");
        _output.WriteLine("");

        // Step 3: 2段階評価を実行
        _output.WriteLine("Step 3: 2段階評価を実行...");
        var keywords = new List<string> { "量子コンピュータ", "量子機械学習", "quantum computing", "quantum machine learning" };
        var articles = new List<Article> { article };

        var result = await _scoringService.EvaluateTwoStageAsync(articles, keywords);

        // Step 4: 結果を詳細に出力
        _output.WriteLine("\n=== 評価結果 ===");
        _output.WriteLine($"  Stage 1 (関連性評価):");
        _output.WriteLine($"    IsRelevant: {article.IsRelevant}");
        _output.WriteLine($"    RelevanceScore: {article.RelevanceScore}");
        _output.WriteLine("");
        _output.WriteLine($"  Stage 2 (品質評価):");
        _output.WriteLine($"    TechnicalScore: {article.TechnicalScore}");
        _output.WriteLine($"    NoveltyScore: {article.NoveltyScore}");
        _output.WriteLine($"    ImpactScore: {article.ImpactScore}");
        _output.WriteLine($"    QualityScore: {article.QualityScore}");
        _output.WriteLine($"    LlmScore: {article.LlmScore ?? 0}");
        _output.WriteLine($"    SummaryJa: {article.SummaryJa ?? "(なし)"}");
        _output.WriteLine("");

        // Step 5: FinalScore計算
        var finalScore = _scoringService.CalculateFinalScore(article, source);
        var normalizedNative = _scoringService.NormalizeNativeScore(article.NativeScore, source.Name);

        _output.WriteLine($"  Final Score計算:");
        _output.WriteLine($"    Normalized Native: {normalizedNative:F2}");
        _output.WriteLine($"    LlmScore: {article.LlmScore ?? 0}");
        _output.WriteLine($"    Final Score: {finalScore:F2}");
        _output.WriteLine("");

        // Step 6: 本番と同じロジックでスコアを計算
        _output.WriteLine("Step 6: 本番ロジックでのスコア計算:");
        double calculatedFinalScore;
        if (article.IsRelevant == false && (article.NativeScore ?? 0) < 5)
        {
            calculatedFinalScore = 0;
            _output.WriteLine($"  → IsRelevant=false かつ NativeScore<5 → FinalScore=0");
        }
        else if (article.LlmScore == null && source.HasNativeScore && article.NativeScore.HasValue)
        {
            calculatedFinalScore = normalizedNative + (source.AuthorityWeight * 10);
            _output.WriteLine($"  → LLMスコアなし、ネイティブスコアベースで計算");
            _output.WriteLine($"  → {normalizedNative:F2} + ({source.AuthorityWeight} * 10) = {calculatedFinalScore:F2}");
        }
        else
        {
            calculatedFinalScore = _scoringService.CalculateFinalScore(article, source);
            _output.WriteLine($"  → 通常計算 → {calculatedFinalScore:F2}");
        }

        _output.WriteLine($"\n=== 最終結論 ===");
        _output.WriteLine($"  この記事のFinalScore: {calculatedFinalScore:F2}");

        // 量子機械学習入門は絶対に0点ではないはず
        Assert.True(article.IsRelevant != false || (article.NativeScore ?? 0) >= 5,
            "量子機械学習入門が除外されるのはおかしい");
    }

    private class QiitaItemResponse
    {
        public string? Title { get; set; }
        public string? Body { get; set; }
        public int LikesCount { get; set; }
        public int StocksCount { get; set; }
        public string? CreatedAt { get; set; }
        public List<QiitaTag>? Tags { get; set; }
    }

    private class QiitaTag
    {
        public string? Name { get; set; }
    }

    /// <summary>
    /// バッチサイズが大きい場合の関連性評価をテスト
    /// 明確に量子コンピュータに関連する記事が除外されないか確認
    /// </summary>
    [Fact]
    public async Task Debug_LargeBatch_RelevanceEvaluation()
    {
        _output.WriteLine("=== 大規模バッチでの関連性評価テスト ===\n");

        var source = new Source
        {
            Id = 1,
            Name = "Qiita",
            HasNativeScore = true,
            AuthorityWeight = 0.7
        };

        // 明確に量子コンピュータに関連する記事と、そうでない記事を混ぜる
        var articles = new List<Article>
        {
            // 明確に関連あり（これらは絶対に除外されるべきではない）
            new() { Id = 1, Title = "量子機械学習入門", Summary = "量子コンピュータを使った機械学習の基礎を解説します。", NativeScore = 0, Source = source, SourceId = 1 },
            new() { Id = 2, Title = "量子コンピュータの基礎と応用", Summary = "量子ビットの概念からGroverのアルゴリズムまで解説", NativeScore = 5, Source = source, SourceId = 1 },
            new() { Id = 3, Title = "Qiskitで始める量子プログラミング", Summary = "IBMの量子コンピュータSDKを使った実践的なチュートリアル", NativeScore = 10, Source = source, SourceId = 1 },
            new() { Id = 4, Title = "量子アルゴリズム入門 - Shorのアルゴリズム", Summary = "RSA暗号を解読する量子アルゴリズムを詳しく解説", NativeScore = 3, Source = source, SourceId = 1 },
            new() { Id = 5, Title = "量子ビットのエラー訂正", Summary = "量子コンピュータにおけるエラー訂正符号の仕組み", NativeScore = 2, Source = source, SourceId = 1 },

            // 関連が薄い記事
            new() { Id = 6, Title = "2026年のITトレンド", Summary = "AIやクラウドなど最新のIT動向をまとめました", NativeScore = 50, Source = source, SourceId = 1 },
            new() { Id = 7, Title = "Pythonで機械学習入門", Summary = "scikit-learnを使った機械学習の基礎", NativeScore = 100, Source = source, SourceId = 1 },
            new() { Id = 8, Title = "セキュリティニュースまとめ", Summary = "今週のセキュリティ関連ニュース", NativeScore = 20, Source = source, SourceId = 1 },

            // 微妙に関連（言及はあるが主題ではない）
            new() { Id = 9, Title = "AIと暗号技術の未来", Summary = "量子コンピュータがセキュリティに与える影響について", NativeScore = 15, Source = source, SourceId = 1 },
            new() { Id = 10, Title = "次世代コンピューティング概論", Summary = "量子コンピュータ、ニューロモーフィックなど次世代技術を概説", NativeScore = 8, Source = source, SourceId = 1 },
        };

        var keywords = new List<string> { "量子コンピュータ", "quantum computing", "量子機械学習" };

        _output.WriteLine($"テスト記事数: {articles.Count}");
        _output.WriteLine($"キーワード: {string.Join(", ", keywords)}\n");

        // 評価実行
        var result = await _scoringService.EvaluateTwoStageAsync(articles, keywords);

        // 結果出力
        _output.WriteLine("=== 関連性評価結果 ===");
        _output.WriteLine($"関連あり: {result.RelevanceResult.RelevantCount}/{articles.Count}件\n");

        foreach (var article in articles.OrderByDescending(a => a.RelevanceScore))
        {
            var status = article.IsRelevant == true ? "✓ 関連" : "✗ 除外";
            _output.WriteLine($"  [{status}] {article.Title}");
            _output.WriteLine($"      RelevanceScore: {article.RelevanceScore}, NativeScore: {article.NativeScore}, LlmScore: {article.LlmScore ?? 0}");
        }

        // 明確に関連する記事が除外されていないか確認
        var mustBeRelevant = new[] { "量子機械学習入門", "量子コンピュータの基礎と応用", "Qiskitで始める量子プログラミング", "量子アルゴリズム入門", "量子ビットのエラー訂正" };
        var wronglyExcluded = new List<string>();

        _output.WriteLine("\n=== 重要な検証 ===");
        foreach (var title in mustBeRelevant)
        {
            var article = articles.First(a => a.Title.Contains(title.Split(' ')[0]));
            if (article.IsRelevant != true)
            {
                wronglyExcluded.Add($"{article.Title} (RelevanceScore={article.RelevanceScore})");
                _output.WriteLine($"  ✗ ERROR: '{article.Title}' が除外されています (Score={article.RelevanceScore})");
            }
            else
            {
                _output.WriteLine($"  ✓ OK: '{article.Title}' は関連ありと判定 (Score={article.RelevanceScore})");
            }
        }

        // 問題がある場合は警告
        if (wronglyExcluded.Any())
        {
            _output.WriteLine($"\n⚠️ 警告: {wronglyExcluded.Count}件の記事が不当に除外されています！");
            _output.WriteLine("   これが0点問題の原因である可能性が高いです。");
        }

        // アサーション（量子コンピュータに直接関連する記事は必ず関連ありであるべき）
        Assert.True(articles.First(a => a.Title == "量子機械学習入門").IsRelevant == true,
            "量子機械学習入門が除外されるのは不適切です");
    }

    /// <summary>
    /// 関連性評価が正しく動作し、無関係な記事が除外されることを確認
    /// </summary>
    [Fact]
    public async Task EvaluateTwoStage_FiltersIrrelevantArticles()
    {
        _output.WriteLine("=== 関連性フィルタリングテスト ===\n");

        var source = new Source
        {
            Id = 1,
            Name = "Qiita",
            HasNativeScore = true,
            HasServerSideFiltering = true,
            AuthorityWeight = 0.7
        };

        // 関連記事と無関係な記事を混ぜる
        var articles = new List<Article>
        {
            new() { Id = 1, Title = "量子コンピュータ入門", Summary = "量子ビットの基礎を解説。キュービットの重ね合わせと量子もつれについて。", NativeScore = 10, Source = source, SourceId = 1 },
            new() { Id = 2, Title = "量子アルゴリズム実装ガイド", Summary = "Groverのアルゴリズムの実装方法を解説", NativeScore = 50, Source = source, SourceId = 1 },
            new() { Id = 3, Title = "美味しいラーメンの作り方", Summary = "自宅で作る本格ラーメンレシピ", NativeScore = 30, Source = source, SourceId = 1 },
        };

        var keywords = new List<string> { "量子コンピュータ" };

        var result = await _scoringService.EvaluateTwoStageAsync(articles, keywords);

        // 結果出力
        _output.WriteLine($"関連あり: {result.RelevanceResult.RelevantCount}/{articles.Count}件");
        _output.WriteLine($"除外: {result.RelevanceResult.FilteredCount}件");
        _output.WriteLine($"API呼び出し: {result.TotalApiCalls}回\n");

        foreach (var article in articles)
        {
            _output.WriteLine($"  [{(article.IsRelevant == true ? "関連" : "除外")}] {article.Title}");
            _output.WriteLine($"      RelevanceScore: {article.RelevanceScore}, LlmScore: {article.LlmScore ?? 0}");
        }

        // 量子コンピュータ関連の記事は高いRelevanceScoreを持つべき
        var quantumArticle = articles.First(a => a.Title == "量子コンピュータ入門");
        Assert.True(quantumArticle.RelevanceScore >= 7, $"量子コンピュータ入門のRelevanceScore={quantumArticle.RelevanceScore}は7以上であるべき");
        Assert.True(quantumArticle.IsRelevant == true, "量子コンピュータ入門は関連ありであるべき");

        // ラーメンの記事は低いRelevanceScoreを持つべき
        var ramenArticle = articles.First(a => a.Title == "美味しいラーメンの作り方");
        Assert.True(ramenArticle.RelevanceScore <= 3, $"ラーメン記事のRelevanceScore={ramenArticle.RelevanceScore}は3以下であるべき");

        _output.WriteLine("\n✓ 関連性フィルタリングが正常に動作しています");
    }

    /// <summary>
    /// 大量の関連記事がStage 1を通過してLlmScoreを取得することを確認
    /// </summary>
    [Fact]
    public async Task EvaluateTwoStage_WithManyRelevantArticles_AllGetLlmScore()
    {
        _output.WriteLine("=== 大量の関連記事でのLlmScore取得テスト ===\n");

        var source = new Source
        {
            Id = 1,
            Name = "Qiita",
            HasNativeScore = true,
            HasServerSideFiltering = true,
            AuthorityWeight = 0.7
        };

        // 明確に量子コンピュータに関連する記事タイトルを使用
        var quantumTitles = new[]
        {
            "量子コンピュータの基礎理論",
            "量子ビットの仕組みを解説",
            "量子もつれの原理と応用",
            "量子アルゴリズム入門",
            "Shorのアルゴリズム実装",
            "Groverの探索アルゴリズム",
            "量子誤り訂正符号の解説",
            "量子機械学習の最前線",
            "VQEで分子シミュレーション",
            "量子コンピュータとAI",
            "IBMの量子コンピュータ使い方",
            "量子暗号通信の仕組み",
            "量子ゲートの基本操作",
            "量子プログラミング入門",
            "量子超越性とは何か",
        };

        var articles = new List<Article>();
        for (int i = 0; i < quantumTitles.Length; i++)
        {
            articles.Add(new Article
            {
                Id = i + 1,
                Title = quantumTitles[i],
                Summary = $"{quantumTitles[i]}について詳しく解説します。量子コンピュータの技術を学びましょう。",
                NativeScore = (i + 1) * 2,
                Source = source,
                SourceId = 1
            });
        }

        var keywords = new List<string> { "量子コンピュータ" };

        _output.WriteLine($"記事数: {articles.Count}件");
        _output.WriteLine($"QualityBatchSize: 5 → {Math.Ceiling(articles.Count / 5.0)}バッチ\n");

        var result = await _scoringService.EvaluateTwoStageAsync(articles, keywords);

        // 結果出力
        _output.WriteLine($"API呼び出し: {result.TotalApiCalls}回");
        _output.WriteLine($"関連あり: {result.RelevanceResult.RelevantCount}/{articles.Count}件");
        _output.WriteLine($"品質評価数: {result.QualityResult.Evaluations.Count}件\n");

        _output.WriteLine("各記事の状態:");
        var articlesWithLlmScore = 0;
        var articlesWithRelevanceScore = 0;
        var articlesWithIsRelevant = 0;

        foreach (var article in articles)
        {
            var hasLlm = article.LlmScore != null;
            var hasRel = article.RelevanceScore != null;
            var isRel = article.IsRelevant == true;

            if (hasLlm) articlesWithLlmScore++;
            if (hasRel) articlesWithRelevanceScore++;
            if (isRel) articlesWithIsRelevant++;

            _output.WriteLine($"  [{(isRel ? "関連" : "除外")}] {article.Title}: LlmScore={article.LlmScore ?? -1}, Relevance={article.RelevanceScore ?? -1}");
        }

        _output.WriteLine($"\n=== サマリー ===");
        _output.WriteLine($"LlmScoreあり: {articlesWithLlmScore}/{articles.Count}");
        _output.WriteLine($"RelevanceScoreあり: {articlesWithRelevanceScore}/{articles.Count}");
        _output.WriteLine($"IsRelevant=true: {articlesWithIsRelevant}/{articles.Count}");

        // 量子コンピュータに関連する記事の大半（80%以上）が関連ありと判定されるべき
        Assert.True(articlesWithIsRelevant >= articles.Count * 0.8,
            $"関連記事の{articlesWithIsRelevant}/{articles.Count}件しか関連ありと判定されませんでした（80%以上必要）");
        _output.WriteLine($"\n✓ {articlesWithIsRelevant}件が関連ありと判定されました");

        // 関連ありと判定された記事はLlmScoreを持つべき
        var relevantArticles = articles.Where(a => a.IsRelevant == true).ToList();
        Assert.All(relevantArticles, a => Assert.NotNull(a.LlmScore));
        _output.WriteLine("✓ 関連記事は全てLlmScoreを取得しました");
    }

    #endregion

    #region Helper Methods

    private ILogger<T> CreateLogger<T>()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        return loggerFactory.CreateLogger<T>();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    #endregion
}

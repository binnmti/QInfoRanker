using System.ClientModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using QInfoRanker.Core.Entities;
using QInfoRanker.Infrastructure.Scoring;
using Xunit.Abstractions;

namespace QInfoRanker.Tests.Integration;

/// <summary>
/// Weekly Summary（ニュース原稿生成）の統合テスト
/// 実際のAzure OpenAI APIを呼び出してテストする
/// CI/CDでスキップ: dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class WeeklySummaryIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IConfiguration _configuration;
    private readonly ChatClient? _chatClient;

    public WeeklySummaryIntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.test.json", optional: false)
            .AddJsonFile("appsettings.test.local.json", optional: true)
            .Build();

        var openAIOptions = _configuration.GetSection("AzureOpenAI").Get<AzureOpenAIOptions>()!;

        if (!string.IsNullOrEmpty(openAIOptions.Endpoint) && !string.IsNullOrEmpty(openAIOptions.ApiKey))
        {
            var client = new AzureOpenAIClient(
                new Uri(openAIOptions.Endpoint),
                new ApiKeyCredential(openAIOptions.ApiKey));
            _chatClient = client.GetChatClient(openAIOptions.DeploymentName);
        }
    }

    #region ニュース原稿生成テスト

    /// <summary>
    /// TOP10記事からニュース原稿を生成するテスト
    /// テスト名: GenerateNewsArticle_FromTop10Articles
    /// </summary>
    [Fact]
    public async Task GenerateNewsArticle_FromTop10Articles()
    {
        if (_chatClient == null)
        {
            _output.WriteLine("Azure OpenAI未設定のためスキップ");
            return;
        }

        _output.WriteLine("=== TOP10記事からニュース原稿を生成 ===\n");

        // Arrange - 架空のTOP10記事（量子コンピュータ関連）
        var articles = CreateSampleTop10Articles();
        var keywordTerm = "量子コンピュータ";

        _output.WriteLine($"キーワード: {keywordTerm}");
        _output.WriteLine($"記事数: {articles.Count}件\n");

        _output.WriteLine("--- 入力記事一覧 ---");
        for (var i = 0; i < articles.Count; i++)
        {
            _output.WriteLine($"{i + 1}. [{articles[i].FinalScore:F0}点] {articles[i].Title}");
            _output.WriteLine($"   ソース: {articles[i].Source?.Name ?? "不明"}");
        }
        _output.WriteLine("");

        // Act - ニュース原稿を生成
        var (title, content) = await GenerateNewsArticleAsync(keywordTerm, articles);

        // Output
        _output.WriteLine("=== 生成されたニュース原稿 ===\n");
        _output.WriteLine($"【タイトル】{title}\n");
        _output.WriteLine("【本文】");
        _output.WriteLine(content);
        _output.WriteLine("\n=== 原稿生成完了 ===");

        // Assert
        Assert.False(string.IsNullOrEmpty(title), "タイトルが空です");
        Assert.False(string.IsNullOrEmpty(content), "本文が空です");
        Assert.True(content.Length > 500, $"本文が短すぎます（{content.Length}文字）");
    }

    /// <summary>
    /// 実際のDBから取得した記事風のデータでニュース原稿を生成
    /// テスト名: GenerateNewsArticle_WithRealisticArticles
    /// </summary>
    [Fact]
    public async Task GenerateNewsArticle_WithRealisticArticles()
    {
        if (_chatClient == null)
        {
            _output.WriteLine("Azure OpenAI未設定のためスキップ");
            return;
        }

        _output.WriteLine("=== リアルな記事データでニュース原稿を生成 ===\n");

        // Arrange - よりリアルな記事データ
        var articles = CreateRealisticTop10Articles();
        var keywordTerm = "量子コンピュータ";

        _output.WriteLine($"キーワード: {keywordTerm}");
        _output.WriteLine($"記事数: {articles.Count}件\n");

        _output.WriteLine("--- 入力記事一覧 ---");
        for (var i = 0; i < articles.Count; i++)
        {
            var article = articles[i];
            _output.WriteLine($"{i + 1}. [{article.FinalScore:F0}点] {article.Title}");
            _output.WriteLine($"   ソース: {article.Source?.Name ?? "不明"} | 日付: {article.PublishedAt?.ToString("yyyy-MM-dd") ?? "不明"}");
            _output.WriteLine($"   要約: {TruncateText(article.SummaryJa ?? article.Summary ?? "", 100)}");
            _output.WriteLine("");
        }

        // Act
        var (title, content) = await GenerateNewsArticleAsync(keywordTerm, articles);

        // Output
        _output.WriteLine("=== 生成されたニュース原稿 ===\n");
        _output.WriteLine($"【タイトル】{title}\n");
        _output.WriteLine("【本文】");
        _output.WriteLine(content);
        _output.WriteLine("\n=== 原稿生成完了 ===");

        Assert.False(string.IsNullOrEmpty(content));
    }

    #endregion

    #region プロンプト・生成ロジック

    private async Task<(string Title, string Content)> GenerateNewsArticleAsync(
        string keywordTerm,
        List<Article> articles,
        CancellationToken cancellationToken = default)
    {
        var articleList = new StringBuilder();
        for (var i = 0; i < articles.Count; i++)
        {
            var article = articles[i];
            var summary = !string.IsNullOrEmpty(article.SummaryJa) ? article.SummaryJa : article.Summary;
            articleList.AppendLine($"{i + 1}. 【{article.Title}】");
            articleList.AppendLine($"   URL: {article.Url}");
            articleList.AppendLine($"   要約: {summary}");
            articleList.AppendLine($"   スコア: {article.FinalScore:F1} / ソース: {article.Source?.Name ?? "不明"}");
            articleList.AppendLine($"   日付: {article.PublishedAt?.ToString("yyyy-MM-dd") ?? "不明"}");
            articleList.AppendLine();
        }

        var weekStart = DateTime.UtcNow.AddDays(-(int)DateTime.UtcNow.DayOfWeek + 1).Date;
        var weekEnd = weekStart.AddDays(6);

        // 改良版プロンプト - ニュース原稿形式
        var prompt = $$"""
            あなたはプロのテクノロジーニュースライターです。
            以下のTOP10記事（スコア順）を元に、「{{keywordTerm}}」に関する今週（{{weekStart:M/d}}〜{{weekEnd:M/d}}）の
            **ニュース原稿**を執筆してください。

            【重要】単なる記事の羅列ではなく、以下のような**読み物としてのニュース原稿**を書いてください：
            - 記者が書いたような、流れのある文章
            - 複数の記事の内容を統合・関連付けて解説
            - 業界動向や背景情報を交えた分析
            - 読者が「なるほど」と思える洞察

            【収集したTOP10記事】
            {{articleList}}

            【出力形式】
            以下のJSON形式で回答してください：
            {
              "title": "キャッチーで内容を反映した見出し（25-35文字）",
              "content": "Markdown形式の本文"
            }

            【本文の構成（目安：1500-2500文字）】

            ## リード文（150-200文字）
            今週の{{keywordTerm}}界隈で起きた主要な動きを、記者の視点で簡潔にまとめる。
            「今週、量子コンピュータ業界では○○が大きな話題となった。」のような導入。

            ## 今週のトピック（3-5項目、各300-500文字）

            ### トピック見出し1
            最も重要なニュースを深掘り。記事の内容を元に、背景や意義を解説。
            関連する他の記事があれば統合して言及。
            （出典: [記事タイトル](URL)）

            ### トピック見出し2
            次に重要なトピック。単独の記事紹介ではなく、
            複数記事を横断した分析や、業界への影響を考察。

            （以下同様）

            ## 来週の展望（100-150文字）
            今週の動向を踏まえ、今後注目すべきポイントを提示。

            【注意事項】
            - 記事をただ並べるのではなく、**ストーリー**として構成する
            - 技術的な内容は正確に、しかし読みやすく
            - 各トピックの最後に出典をMarkdownリンクで記載
            - 記事間の関連性があれば積極的に言及
            - 業界全体の文脈で解説（「この発表は○○の流れを受けて...」など）
            """;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("あなたはプロのテクノロジーニュースライターです。JSON形式でのみ回答してください。"),
            new UserChatMessage(prompt)
        };

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = 4000,
            Temperature = 0.7f
        };

        try
        {
            var response = await _chatClient!.CompleteChatAsync(messages, options, cancellationToken);
            var responseContent = response.Value.Content[0].Text;

            _output.WriteLine($"[DEBUG] Token使用量: Input={response.Value.Usage.InputTokenCount}, Output={response.Value.Usage.OutputTokenCount}");

            var parsed = ParseSummaryResponse(responseContent);
            if (parsed != null)
            {
                return (parsed.Title, parsed.Content);
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[ERROR] 生成失敗: {ex.Message}");
        }

        return ("生成失敗", "ニュース原稿の生成に失敗しました。");
    }

    private SummaryResponse? ParseSummaryResponse(string content)
    {
        try
        {
            content = CleanJsonResponse(content);
            return JsonSerializer.Deserialize<SummaryResponse>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _output.WriteLine($"[WARN] JSONパース失敗: {ex.Message}");
            _output.WriteLine($"[DEBUG] Raw content: {content[..Math.Min(500, content.Length)]}...");
            return null;
        }
    }

    private static string CleanJsonResponse(string content)
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

    #endregion

    #region テストデータ

    private List<Article> CreateSampleTop10Articles()
    {
        var sources = new Dictionary<string, Source>
        {
            ["Qiita"] = new Source { Id = 1, Name = "Qiita" },
            ["arXiv"] = new Source { Id = 2, Name = "arXiv" },
            ["Hacker News"] = new Source { Id = 3, Name = "Hacker News" },
            ["Zenn"] = new Source { Id = 4, Name = "Zenn" },
        };

        return new List<Article>
        {
            new()
            {
                Id = 1, Title = "IBM、1000量子ビット超のCondorプロセッサを発表",
                SummaryJa = "IBMが新型量子プロセッサ「Condor」を発表。1121量子ビットを搭載し、同社の量子ロードマップにおける重要なマイルストーンを達成。エラー率の改善と実用的な量子計算への道筋を示した。",
                Url = "https://example.com/ibm-condor", FinalScore = 92, Source = sources["Hacker News"],
                PublishedAt = DateTime.UtcNow.AddDays(-2)
            },
            new()
            {
                Id = 2, Title = "Google、量子誤り訂正で重大なブレークスルーを達成",
                SummaryJa = "Googleの量子AIチームが、論理量子ビットのエラー率を物理量子ビット以下に抑えることに成功。実用的な量子コンピュータ実現に向けた大きな一歩。",
                Url = "https://example.com/google-qec", FinalScore = 88, Source = sources["arXiv"],
                PublishedAt = DateTime.UtcNow.AddDays(-3)
            },
            new()
            {
                Id = 3, Title = "Qiskitで学ぶ量子機械学習入門",
                SummaryJa = "IBMのQiskitを使った量子機械学習の実践的チュートリアル。VQCやQSVMの実装例を通じて、量子コンピュータを使った機械学習の基礎を解説。",
                Url = "https://example.com/qiskit-qml", FinalScore = 85, Source = sources["Qiita"],
                PublishedAt = DateTime.UtcNow.AddDays(-1)
            },
            new()
            {
                Id = 4, Title = "量子コンピュータが暗号を破る日 - 耐量子暗号への移行",
                SummaryJa = "量子コンピュータの発展により、現行のRSA暗号が脅威にさらされる可能性について解説。NISTの耐量子暗号標準化の動向と、企業が今から準備すべきことを紹介。",
                Url = "https://example.com/pqc", FinalScore = 82, Source = sources["Zenn"],
                PublishedAt = DateTime.UtcNow.AddDays(-4)
            },
            new()
            {
                Id = 5, Title = "Microsoft、トポロジカル量子ビットの実現に前進",
                SummaryJa = "Microsoftが長年研究してきたトポロジカル量子ビットについて、実験的な検証に成功したと発表。ノイズに強い量子計算の実現可能性を示した。",
                Url = "https://example.com/ms-topo", FinalScore = 78, Source = sources["Hacker News"],
                PublishedAt = DateTime.UtcNow.AddDays(-2)
            },
            new()
            {
                Id = 6, Title = "量子コンピュータの現状と限界 - 2024年版",
                SummaryJa = "現在の量子コンピュータで実際に何ができて、何ができないのかを冷静に分析。NISQ時代の限界と、今後5年間で期待される進歩を解説。",
                Url = "https://example.com/quantum-2024", FinalScore = 75, Source = sources["Qiita"],
                PublishedAt = DateTime.UtcNow.AddDays(-5)
            },
            new()
            {
                Id = 7, Title = "量子アニーリングで組合せ最適化を解く",
                SummaryJa = "D-Waveの量子アニーリングマシンを使った組合せ最適化問題の解法を解説。実際のビジネス問題への適用事例と、古典コンピュータとの比較を紹介。",
                Url = "https://example.com/quantum-annealing", FinalScore = 72, Source = sources["Zenn"],
                PublishedAt = DateTime.UtcNow.AddDays(-3)
            },
            new()
            {
                Id = 8, Title = "中国、新型量子コンピュータで超電導量子ビット数記録を更新",
                SummaryJa = "中国科学技術大学が504量子ビットの超電導量子プロセッサを開発。量子超越性の実証実験に向けた取り組みを加速。",
                Url = "https://example.com/china-quantum", FinalScore = 70, Source = sources["arXiv"],
                PublishedAt = DateTime.UtcNow.AddDays(-1)
            },
            new()
            {
                Id = 9, Title = "量子コンピュータ×創薬：分子シミュレーションの可能性",
                SummaryJa = "量子コンピュータを使った創薬研究の最前線。VQEアルゴリズムによる分子シミュレーションの実例と、製薬会社との共同研究の動向を紹介。",
                Url = "https://example.com/quantum-drug", FinalScore = 68, Source = sources["Qiita"],
                PublishedAt = DateTime.UtcNow.AddDays(-4)
            },
            new()
            {
                Id = 10, Title = "AWS Braketで始める量子プログラミング入門",
                SummaryJa = "AWSの量子コンピューティングサービスBraketを使った実践的な入門ガイド。複数の量子ハードウェアにアクセスし、量子回路を実行する方法を解説。",
                Url = "https://example.com/aws-braket", FinalScore = 65, Source = sources["Zenn"],
                PublishedAt = DateTime.UtcNow.AddDays(-2)
            },
        };
    }

    private List<Article> CreateRealisticTop10Articles()
    {
        var sources = new Dictionary<string, Source>
        {
            ["Qiita"] = new Source { Id = 1, Name = "Qiita" },
            ["arXiv"] = new Source { Id = 2, Name = "arXiv" },
            ["Hacker News"] = new Source { Id = 3, Name = "Hacker News" },
            ["Zenn"] = new Source { Id = 4, Name = "Zenn" },
            ["日経"] = new Source { Id = 5, Name = "日経新聞" },
        };

        return new List<Article>
        {
            new()
            {
                Id = 1,
                Title = "IBM Unveils 1,121-Qubit Condor Processor, Marking Major Quantum Computing Milestone",
                SummaryJa = "IBMは量子コンピューティングの新時代を告げる「Condor」プロセッサを発表した。1,121量子ビットを搭載するこのプロセッサは、同社の量子ロードマップにおける重要なマイルストーン。IBMは同時に、新しいモジュラー量子アーキテクチャ「Heron」も発表し、より少ない量子ビット数でも高い性能を発揮できるアプローチを示した。これにより、量子コンピュータの実用化に向けた道筋がより明確になった。",
                Url = "https://newsroom.ibm.com/quantum-condor",
                FinalScore = 95,
                Source = sources["Hacker News"],
                PublishedAt = DateTime.UtcNow.AddDays(-2),
                NativeScore = 523
            },
            new()
            {
                Id = 2,
                Title = "Suppressing quantum errors by scaling a surface code logical qubit",
                SummaryJa = "Google Quantum AIチームによる画期的な研究成果。表面符号を用いた論理量子ビットのスケーリングにより、量子誤り訂正の効率が大幅に向上。物理量子ビットの数を増やすことで、論理量子ビットのエラー率を指数関数的に低減できることを実証した。これは実用的なフォールトトレラント量子コンピュータ実現への重要なステップとなる。",
                Url = "https://arxiv.org/abs/2312.00000",
                FinalScore = 91,
                Source = sources["arXiv"],
                PublishedAt = DateTime.UtcNow.AddDays(-3)
            },
            new()
            {
                Id = 3,
                Title = "量子コンピュータ実用化へ、日本発スタートアップが100億円調達",
                SummaryJa = "国産量子コンピュータの開発を手がけるスタートアップが、シリーズBラウンドで100億円の資金調達を完了。光量子方式による独自アーキテクチャを採用し、2025年中の商用サービス開始を目指す。国内外の製薬・金融企業との共同研究も進行中で、日本の量子技術産業における存在感を高めている。",
                Url = "https://nikkei.com/quantum-startup",
                FinalScore = 87,
                Source = sources["日経"],
                PublishedAt = DateTime.UtcNow.AddDays(-1)
            },
            new()
            {
                Id = 4,
                Title = "【実践】Qiskitで量子機械学習を始めよう - VQCの実装から評価まで",
                SummaryJa = "IBMのオープンソース量子開発フレームワークQiskitを使った量子機械学習の実践的チュートリアル。Variational Quantum Classifier (VQC)の理論的背景から実装、MNISTデータセットを使った分類タスクでの評価まで、ステップバイステップで解説。古典的な機械学習手法との比較実験も含み、現時点での量子機械学習の可能性と限界を明らかにしている。",
                Url = "https://qiita.com/quantum-ml-tutorial",
                FinalScore = 84,
                Source = sources["Qiita"],
                PublishedAt = DateTime.UtcNow.AddDays(-4),
                NativeScore = 156
            },
            new()
            {
                Id = 5,
                Title = "NIST、耐量子暗号標準を正式発表 - 企業は移行準備を",
                SummaryJa = "米国標準技術研究所(NIST)が、量子コンピュータによる攻撃に耐えうる新しい暗号標準を正式に発表。CRYSTALS-Kyber、CRYSTALS-Dilithium、SPHINCS+の3種類が標準化され、世界中の企業・政府機関に移行を促している。専門家は、現行のRSA/ECC暗号から新標準への移行には3-5年かかると予測しており、早期の対応が求められる。",
                Url = "https://example.com/nist-pqc",
                FinalScore = 81,
                Source = sources["Hacker News"],
                PublishedAt = DateTime.UtcNow.AddDays(-5),
                NativeScore = 287
            },
            new()
            {
                Id = 6,
                Title = "量子アニーリングによる金融ポートフォリオ最適化の実証実験",
                SummaryJa = "D-Wave社の量子アニーリングマシンを使用した金融ポートフォリオ最適化の実証実験結果を報告。従来の古典的最適化手法と比較して、特定の条件下で計算時間を大幅に短縮できることを確認。ただし、問題サイズが大きくなると量子的優位性が失われる傾向も観察され、実用化に向けた課題も明らかになった。",
                Url = "https://zenn.dev/quantum-finance",
                FinalScore = 77,
                Source = sources["Zenn"],
                PublishedAt = DateTime.UtcNow.AddDays(-3)
            },
            new()
            {
                Id = 7,
                Title = "Microsoft achieves first milestone towards a quantum supercomputer",
                SummaryJa = "Microsoftが長年研究してきたトポロジカル量子ビットの実現に向けて重要なマイルストーンを達成。マヨラナゼロモードの存在を実験的に確認し、従来の超電導量子ビットよりもノイズに強い量子計算の可能性を示した。同社は2025年までに論理量子ビットの実証を目指すとしている。",
                Url = "https://news.microsoft.com/quantum",
                FinalScore = 74,
                Source = sources["Hacker News"],
                PublishedAt = DateTime.UtcNow.AddDays(-2),
                NativeScore = 198
            },
            new()
            {
                Id = 8,
                Title = "量子コンピュータで創薬シミュレーション - VQEによる分子エネルギー計算",
                SummaryJa = "変分量子固有値ソルバー(VQE)を用いた創薬向け分子シミュレーションの最新動向を解説。現在の量子コンピュータでシミュレーション可能な分子サイズの限界と、ノイズの影響を軽減するためのエラー緩和技術について詳述。製薬企業と量子コンピュータベンダーの共同研究事例も紹介している。",
                Url = "https://qiita.com/quantum-drug-discovery",
                FinalScore = 71,
                Source = sources["Qiita"],
                PublishedAt = DateTime.UtcNow.AddDays(-4),
                NativeScore = 89
            },
            new()
            {
                Id = 9,
                Title = "中国、66量子ビットの「祖冲之3号」でランダム回路サンプリングを実証",
                SummaryJa = "中国科学技術大学のチームが、66量子ビットの超電導量子プロセッサ「祖冲之3号」を使用し、量子超越性の実証実験に成功。Googleのsycamoreと同等以上の性能を示し、中国の量子コンピューティング能力が着実に向上していることを示した。米中間の量子技術競争がさらに激化する見通し。",
                Url = "https://arxiv.org/abs/2312.99999",
                FinalScore = 68,
                Source = sources["arXiv"],
                PublishedAt = DateTime.UtcNow.AddDays(-1)
            },
            new()
            {
                Id = 10,
                Title = "AWS Braket入門 - 複数の量子ハードウェアを使い分ける方法",
                SummaryJa = "AWSの量子コンピューティングサービスBraketを使って、IonQ、Rigetti、D-Waveなど複数の量子ハードウェアにアクセスする方法を解説。各ハードウェアの特徴と得意な計算タスク、料金体系の違いを比較。ハイブリッド古典-量子アルゴリズムの実装例も含む実践的なガイド。",
                Url = "https://zenn.dev/aws-braket-guide",
                FinalScore = 65,
                Source = sources["Zenn"],
                PublishedAt = DateTime.UtcNow.AddDays(-5)
            },
        };
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }

    #endregion

    #region Helper Classes

    private class SummaryResponse
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    #endregion

    public void Dispose()
    {
        // Cleanup if needed
    }
}

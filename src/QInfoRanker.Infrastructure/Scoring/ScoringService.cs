using System.ClientModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using QInfoRanker.Core.Entities;
using QInfoRanker.Core.Interfaces.Services;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only

namespace QInfoRanker.Infrastructure.Scoring;

public class ScoringService : IScoringService
{
    private readonly AzureOpenAIOptions _openAIOptions;
    private readonly ScoringOptions _scoringOptions;
    private readonly BatchScoringOptions _batchOptions;
    private readonly EnsembleScoringOptions _ensembleOptions;
    private readonly ILogger<ScoringService> _logger;

    // v1 API用のOpenAIClient
    private readonly OpenAIClient? _openAIClient;

    // Chat Completion API 用クライアント（v1 APIエンドポイント）
    private readonly ChatClient? _chatClient;

    // v1 API用のエンドポイントとAPIキー（Judge用に保持）
    private readonly Uri? _v1Endpoint;
    private readonly ApiKeyCredential? _credential;

    // アンサンブル評価用のJudge別クライアント
    // ChatClient: 通常モデル用、ResponsesClient: Codexモデル用
    private readonly Dictionary<string, ChatClient> _judgeChatClients = new();
    private readonly Dictionary<string, ResponsesClient> _judgeResponsesClients = new();
    private ChatClient? _metaJudgeChatClient;
    private ResponsesClient? _metaJudgeResponsesClient;

    public ScoringService(
        IOptions<AzureOpenAIOptions> openAIOptions,
        IOptions<ScoringOptions> scoringOptions,
        IOptions<BatchScoringOptions> batchOptions,
        IOptions<EnsembleScoringOptions> ensembleOptions,
        ILogger<ScoringService> logger)
    {
        _openAIOptions = openAIOptions.Value;
        _scoringOptions = scoringOptions.Value;
        _batchOptions = batchOptions.Value;
        _ensembleOptions = ensembleOptions.Value;
        _logger = logger;

        if (!string.IsNullOrEmpty(_openAIOptions.Endpoint) && !string.IsNullOrEmpty(_openAIOptions.ApiKey))
        {
            // v1 API エンドポイント（APIバージョン指定不要）
            var baseEndpoint = _openAIOptions.Endpoint.TrimEnd('/');
            _v1Endpoint = new Uri($"{baseEndpoint}/openai/v1");
            _credential = new ApiKeyCredential(_openAIOptions.ApiKey);

            var clientOptions = new OpenAIClientOptions
            {
                Endpoint = _v1Endpoint
            };

            // OpenAIClient を作成し、Chat Completion API クライアントを取得
            _openAIClient = new OpenAIClient(_credential, clientOptions);
            _chatClient = _openAIClient.GetChatClient(_openAIOptions.DeploymentName);

            _logger.LogInformation("Initialized Chat Completion API client: {DeploymentName} at {Endpoint}",
                _openAIOptions.DeploymentName, _v1Endpoint);

            // アンサンブル評価用クライアントを初期化
            InitializeEnsembleClients();
        }
    }

    private void InitializeEnsembleClients()
    {
        if (_openAIClient == null) return;

        foreach (var judge in _ensembleOptions.Judges.Where(j => j.IsEnabled))
        {
            try
            {
                var deploymentName = judge.DeploymentName;
                var apiType = ModelCapabilities.RequiresResponsesApi(deploymentName) ? "Responses" : "ChatCompletion";

                if (ModelCapabilities.RequiresResponsesApi(deploymentName))
                {
                    _judgeResponsesClients[judge.JudgeId] = _openAIClient.GetResponsesClient(deploymentName);
                }
                else
                {
                    _judgeChatClients[judge.JudgeId] = _openAIClient.GetChatClient(deploymentName);
                }

                _logger.LogInformation("Initialized Judge client: {JudgeId} ({DeploymentName}) using {ApiType} API",
                    judge.JudgeId, deploymentName, apiType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Judge client: {JudgeId}", judge.JudgeId);
            }
        }

        if (_ensembleOptions.MetaJudge.IsEnabled)
        {
            try
            {
                var deploymentName = _ensembleOptions.MetaJudge.DeploymentName;
                var apiType = ModelCapabilities.RequiresResponsesApi(deploymentName) ? "Responses" : "ChatCompletion";

                if (ModelCapabilities.RequiresResponsesApi(deploymentName))
                {
                    _metaJudgeResponsesClient = _openAIClient.GetResponsesClient(deploymentName);
                }
                else
                {
                    _metaJudgeChatClient = _openAIClient.GetChatClient(deploymentName);
                }

                _logger.LogInformation("Initialized Meta-Judge client: {DeploymentName} using {ApiType} API",
                    deploymentName, apiType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Meta-Judge client");
            }
        }
    }

    public async Task<Article> CalculateLlmScoreAsync(
        Article article,
        bool includeContent = false,
        CancellationToken cancellationToken = default)
    {
        if (_chatClient == null)
        {
            _logger.LogWarning("Azure OpenAI not configured. Skipping LLM scoring.");
            return article;
        }

        try
        {
            var prompt = BuildScoringPrompt(article, includeContent);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are an expert evaluator of technical articles. Respond only with valid JSON."),
                new UserChatMessage(prompt)
            };

            var options = new ChatCompletionOptions();

            // 推論モデルでなければ Temperature を設定、推論モデルは ReasoningEffortLevel を使用
            if (!ModelCapabilities.IsReasoningModel(_openAIOptions.DeploymentName))
            {
                options.Temperature = _openAIOptions.Temperature;
            }
            else
            {
                options.ReasoningEffortLevel = ChatReasoningEffortLevel.Low;
            }

            var response = await _chatClient.CompleteChatAsync(
                messages: messages,
                options: options,
                cancellationToken: cancellationToken);

            var content = response.Value.Content[0].Text;

            var scores = ParseScores(content);

            if (scores != null)
            {
                article.TechnicalScore = scores.Technical;
                article.NoveltyScore = scores.Novelty;
                article.ImpactScore = scores.Impact;
                article.QualityScore = scores.Quality;
                article.LlmScore = scores.Total;

                _logger.LogInformation(
                    "LLM scored article '{Title}': Technical={Technical}, Novelty={Novelty}, Impact={Impact}, Quality={Quality}, Total={Total}",
                    article.Title, scores.Technical, scores.Novelty, scores.Impact, scores.Quality, scores.Total);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating LLM score for article: {Title}", article.Title);
        }

        return article;
    }

    public async Task<IEnumerable<Article>> CalculateLlmScoresAsync(
        IEnumerable<Article> articles,
        CancellationToken cancellationToken = default)
    {
        var articleList = articles.ToList();

        foreach (var article in articleList)
        {
            // Include content for articles without native scores
            var includeContent = !article.NativeScore.HasValue;
            await CalculateLlmScoreAsync(article, includeContent, cancellationToken);

            // Small delay to avoid rate limiting
            await Task.Delay(500, cancellationToken);
        }

        return articleList;
    }

    public double CalculateFinalScore(Article article, Source source)
    {
        // 5項目×20点 = 100点満点スコアリング
        // - relevance: Stage 2での最終関連性評価 (0-20)
        // - technical: 技術的深さ (0-20)
        // - novelty: 新規性 (0-20)
        // - impact: 実用性 (0-20)
        // - quality: 情報の質 (0-20)

        // Stage 2でのrelevance評価がある場合はそれを使用、なければStage 1の値を2倍
        var relevanceScore = article.EnsembleRelevanceScore ?? ((article.RelevanceScore ?? 5) * 2);

        var finalScore = relevanceScore +
                        (article.TechnicalScore ?? 0) +
                        (article.NoveltyScore ?? 0) +
                        (article.ImpactScore ?? 0) +
                        (article.QualityScore ?? 0);

        return Math.Min(100, Math.Max(0, finalScore));
    }

    public double NormalizeNativeScore(int? nativeScore, string sourceName)
    {
        if (!nativeScore.HasValue || nativeScore.Value <= 0)
            return 0;

        var maxScore = _scoringOptions.MaxNativeScores.TryGetValue(sourceName, out var max)
            ? max
            : _scoringOptions.DefaultMaxNativeScore;

        // Use logarithmic scaling for better distribution
        var logScore = Math.Log10(nativeScore.Value + 1);
        var logMax = Math.Log10(maxScore + 1);

        return Math.Min(100, (logScore / logMax) * 100);
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        if (_chatClient == null)
        {
            throw new InvalidOperationException("Azure OpenAI is not configured. Check Endpoint and ApiKey settings.");
        }

        try
        {
            var messages = new List<ChatMessage>
            {
                new UserChatMessage("Reply with 'OK' only.")
            };

            var options = new ChatCompletionOptions();

            // 推論モデルでなければ Temperature を設定
            if (!ModelCapabilities.IsReasoningModel(_openAIOptions.DeploymentName))
            {
                options.Temperature = 0;
            }

            var response = await _chatClient.CompleteChatAsync(
                messages: messages,
                options: options,
                cancellationToken: cancellationToken);

            return !string.IsNullOrEmpty(response.Value.Content[0].Text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI service health check failed");
            throw new InvalidOperationException($"AI service is unavailable: {ex.Message}", ex);
        }
    }

    private string BuildScoringPrompt(Article article, bool includeContent)
    {
        var prompt = $"""
            Evaluate the following article and provide scores (0-20 each) for:
            - technical: Technical importance and depth
            - novelty: Originality and newness of the content
            - impact: Practical impact and usefulness
            - quality: Overall quality and reliability of information

            Title: {article.Title}
            """;

        if (!string.IsNullOrEmpty(article.Summary))
        {
            prompt += $"\nSummary: {article.Summary}";
        }

        if (includeContent && !string.IsNullOrEmpty(article.Content))
        {
            var content = article.Content.Length > 2000
                ? article.Content[..2000] + "..."
                : article.Content;
            prompt += $"\nContent: {content}";
        }

        prompt += """

            Respond with JSON only:
            {"technical": X, "novelty": X, "impact": X, "quality": X, "total": X}
            Where total is the sum of all scores (0-80).
            """;

        return prompt;
    }

    private LlmScoreResult? ParseScores(string content)
    {
        try
        {
            // Clean up the response (remove markdown code blocks if present)
            content = content.Trim();
            if (content.StartsWith("```"))
            {
                content = content.Split('\n', 2)[1];
                content = content[..content.LastIndexOf("```")];
            }

            var result = JsonSerializer.Deserialize<LlmScoreResult>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse LLM score response: {Content}", content);
            return null;
        }
    }

    private class LlmScoreResult
    {
        public int Technical { get; set; }
        public int Novelty { get; set; }
        public int Impact { get; set; }
        public int Quality { get; set; }
        public int Total { get; set; }
    }

    #region 2段階バッチスコアリング

    public Task<TwoStageResult> EvaluateTwoStageAsync(
        IEnumerable<Article> articles,
        IEnumerable<string> keywords,
        bool skipRelevanceFilter = false,
        CancellationToken cancellationToken = default)
    {
        return EvaluateTwoStageAsync(articles, keywords, skipRelevanceFilter, null, cancellationToken);
    }

    public Task<TwoStageResult> EvaluateTwoStageAsync(
        IEnumerable<Article> articles,
        IEnumerable<string> keywords,
        bool skipRelevanceFilter,
        IProgress<ScoringProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        return EvaluateTwoStageAsync(articles, keywords, skipRelevanceFilter, progress, null, null, cancellationToken);
    }

    public async Task<TwoStageResult> EvaluateTwoStageAsync(
        IEnumerable<Article> articles,
        IEnumerable<string> keywords,
        bool skipRelevanceFilter,
        IProgress<ScoringProgress>? progress,
        Action<BatchRelevanceResult, IEnumerable<Article>>? onRelevanceComplete,
        Action<IEnumerable<Article>, IEnumerable<ArticleQuality>>? onQualityBatchComplete,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new TwoStageResult();
        var articleList = articles.ToList();
        var keywordList = keywords.ToList();

        if (!_batchOptions.EnableBatchProcessing || _chatClient == null)
        {
            _logger.LogWarning("バッチ処理が無効またはOpenAI未設定。全記事を関連ありとしてマーク。");
            result.RelevanceResult = MarkAllAsRelevant(articleList);

            // 記事にIsRelevant=trueを直接反映
            foreach (var article in articleList)
            {
                article.IsRelevant = true;
                article.RelevanceScore = 10;
            }

            result.TotalDuration = stopwatch.Elapsed;
            return result;
        }

        // Stage 1: 常に関連性評価を実行（内容がキーワードと合っているかを確認）
        _logger.LogInformation("Stage 1: {Count}件の記事の関連性を評価中...", articleList.Count);
        result.RelevanceResult = await EvaluateRelevanceBatchAsync(articleList, keywordList, progress, cancellationToken);

        // 記事にRelevanceScoreを反映
        foreach (var eval in result.RelevanceResult.Evaluations)
        {
            var article = articleList.FirstOrDefault(a => a.Id == eval.ArticleId);
            if (article != null)
            {
                article.RelevanceScore = eval.RelevanceScore;
                article.IsRelevant = eval.IsRelevant;
            }
        }

        // 評価されなかった記事（LLMレスポンス不足）にデフォルト値を設定
        var unevaluatedArticles = articleList.Where(a => a.IsRelevant == null).ToList();
        if (unevaluatedArticles.Any())
        {
            _logger.LogWarning("{Count}件の記事が評価されませんでした。デフォルトで関連ありとしてマーク。", unevaluatedArticles.Count);
            foreach (var article in unevaluatedArticles)
            {
                article.IsRelevant = true;
                article.RelevanceScore = 7; // デフォルト関連性スコア
                result.RelevanceResult.Evaluations.Add(new ArticleRelevance
                {
                    ArticleId = article.Id,
                    RelevanceScore = 7,
                    IsRelevant = true,
                    Reason = "LLMレスポンス不足のためデフォルト設定"
                });
            }
            result.RelevanceResult.RelevantCount += unevaluatedArticles.Count;
        }

        // Stage 2: 関連記事のみ品質評価（閾値を超えた記事のみ）
        var relevantArticles = articleList.Where(a => a.IsRelevant == true).ToList();
        _logger.LogInformation("Stage 2: {Relevant}件の関連記事を品質評価中... ({Filtered}件を除外)",
            relevantArticles.Count, result.RelevanceResult.FilteredCount);

        // フィルタリング完了コールバック（Stage 1終了、Stage 2開始前）
        if (relevantArticles.Any())
        {
            onRelevanceComplete?.Invoke(result.RelevanceResult, relevantArticles);
        }

        if (relevantArticles.Any())
        {
            result.QualityResult = await EvaluateQualityBatchAsync(relevantArticles, keywordList, progress, onQualityBatchComplete, cancellationToken);

            // 記事に品質スコアを反映
            var evaluatedArticleIds = new HashSet<int>();
            var stage2FilteredCount = 0;
            foreach (var eval in result.QualityResult.Evaluations)
            {
                var article = relevantArticles.FirstOrDefault(a => a.Id == eval.ArticleId);
                if (article != null)
                {
                    article.EnsembleRelevanceScore = eval.Relevance;
                    article.TechnicalScore = eval.Technical;
                    article.NoveltyScore = eval.Novelty;
                    article.ImpactScore = eval.Impact;
                    article.QualityScore = eval.Quality;
                    article.LlmScore = eval.Total;
                    article.SummaryJa = eval.SummaryJa;
                    evaluatedArticleIds.Add(article.Id);

                    // Stage 2での関連性再評価で閾値未満なら除外
                    if (eval.Relevance < _scoringOptions.EnsembleRelevanceThreshold)
                    {
                        article.IsRelevant = false;
                        stage2FilteredCount++;
                        _logger.LogDebug("Stage 2で除外: {Title} (Relevance={Relevance} < {Threshold})",
                            article.Title, eval.Relevance, _scoringOptions.EnsembleRelevanceThreshold);
                    }
                }
            }

            if (stage2FilteredCount > 0)
            {
                _logger.LogInformation("Stage 2の関連性再評価で{Count}件を除外 (閾値: {Threshold})",
                    stage2FilteredCount, _scoringOptions.EnsembleRelevanceThreshold);
            }

            // 品質評価がされなかった記事にデフォルトスコアを設定
            var unevaluatedQualityArticles = relevantArticles.Where(a => !evaluatedArticleIds.Contains(a.Id)).ToList();
            if (unevaluatedQualityArticles.Any())
            {
                _logger.LogWarning("{Count}件の関連記事の品質評価がされませんでした。デフォルトスコアを設定します。",
                    unevaluatedQualityArticles.Count);
                foreach (var article in unevaluatedQualityArticles)
                {
                    // デフォルトの品質スコア（中程度の評価）
                    article.TechnicalScore = 15;
                    article.NoveltyScore = 12;
                    article.ImpactScore = 13;
                    article.QualityScore = 15;
                    article.LlmScore = 55; // 中程度のスコア
                }
            }
        }

        result.TotalApiCalls = result.RelevanceResult.ApiCallCount + result.QualityResult.ApiCallCount;
        result.TotalDuration = stopwatch.Elapsed;

        // トークン使用量を集約
        result.TotalInputTokens = result.RelevanceResult.InputTokens + result.QualityResult.InputTokens;
        result.TotalOutputTokens = result.RelevanceResult.OutputTokens + result.QualityResult.OutputTokens;

        _logger.LogInformation(
            "2段階評価完了: {Total}件中{Relevant}件が関連あり, API呼び出し{ApiCalls}回, 処理時間{Duration:F1}s",
            articleList.Count, relevantArticles.Count, result.TotalApiCalls, result.TotalDuration.TotalSeconds);

        _logger.LogInformation(
            "トークン使用量: 入力={InputTokens:N0}, 出力={OutputTokens:N0}, 合計={TotalTokens:N0}, 推定コスト=${Cost:F4} USD",
            result.TotalInputTokens, result.TotalOutputTokens, result.TotalTokens, result.EstimatedCostUsd);

        return result;
    }

    private async Task<BatchRelevanceResult> EvaluateRelevanceBatchAsync(
        List<Article> articles,
        List<string> keywords,
        IProgress<ScoringProgress>? progress,
        CancellationToken cancellationToken)
    {
        var result = new BatchRelevanceResult { TotalProcessed = articles.Count };

        var batches = articles
            .Select((article, index) => new { article, index })
            .GroupBy(x => x.index / _batchOptions.Filtering.BatchSize)
            .Select(g => g.Select(x => x.article).ToList())
            .ToList();

        var totalBatches = batches.Count;
        var processedArticles = 0;
        var relevantSoFar = 0;

        for (int i = 0; i < batches.Count; i++)
        {
            var batch = batches[i];
            result.ApiCallCount++;

            // 進捗通知（バッチ処理前）
            progress?.Report(new ScoringProgress(
                ScoringStage.RelevanceEvaluation,
                i + 1, totalBatches,
                processedArticles, articles.Count,
                relevantSoFar,
                $"フィルタリング中..."
            ));

            try
            {
                var (evaluations, inputTokens, outputTokens) = await ProcessRelevanceBatchAsync(batch, keywords, cancellationToken);
                result.Evaluations.AddRange(evaluations);
                result.InputTokens += inputTokens;
                result.OutputTokens += outputTokens;
                processedArticles += batch.Count;
                relevantSoFar = result.Evaluations.Count(e => e.IsRelevant);

                // 進捗通知（バッチ処理後 - 結果を含む）
                progress?.Report(new ScoringProgress(
                    ScoringStage.RelevanceEvaluation,
                    i + 1, totalBatches,
                    processedArticles, articles.Count,
                    relevantSoFar,
                    $"フィルタリング完了 ({processedArticles}件中 {relevantSoFar}件が関連あり)"
                ));

                if (_batchOptions.DelayBetweenBatchesMs > 0)
                    await Task.Delay(_batchOptions.DelayBetweenBatchesMs, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "関連性バッチ評価に失敗。フォールバックで全て関連ありとしてマーク。");
                if (_batchOptions.FallbackToIndividual)
                {
                    foreach (var article in batch)
                    {
                        result.Evaluations.Add(new ArticleRelevance
                        {
                            ArticleId = article.Id,
                            RelevanceScore = 10,
                            IsRelevant = true,
                            Reason = "バッチ処理失敗のためフォールバック"
                        });
                    }
                }
                processedArticles += batch.Count;
                relevantSoFar = result.Evaluations.Count(e => e.IsRelevant);
            }
        }

        result.RelevantCount = result.Evaluations.Count(e => e.IsRelevant);
        result.FilteredCount = result.TotalProcessed - result.RelevantCount;
        return result;
    }

    private async Task<(List<ArticleRelevance> Evaluations, int InputTokens, int OutputTokens)> ProcessRelevanceBatchAsync(
        List<Article> batch,
        List<string> keywords,
        CancellationToken cancellationToken)
    {
        var articlesJson = batch.Select((a, i) => new
        {
            id = i + 1,
            title = a.Title,
            summary = TruncateText(a.Summary, 300),
            source = a.Source?.Name ?? "Unknown"
        });

        var keywordsStr = string.Join(", ", keywords);
        var articlesJsonStr = JsonSerializer.Serialize(articlesJson, new JsonSerializerOptions { WriteIndented = false });

        var prompt = $$"""
            キーワード「{{keywordsStr}}」に対する各記事の関連性を0-10で評価してください。

            【重要】
            - サマリー（summary）の内容を最も重視して判断してください
            - タイトルがクイズ形式やキャッチーな表現でも、サマリーがキーワードについて実質的に扱っていれば高評価にしてください
            - キーワードが記事内に単に「含まれている」だけでは高評価にしないでください

            評価基準:
            - 9-10: サマリーの主題がキーワードのトピックそのもの（技術解説、入門記事、実装例など）
            - 7-8: サマリーがキーワードのトピックについて実質的・技術的に扱っている
            - 5-6: キーワードのトピックに部分的に関連（関連技術、応用例など）
            - 3-4: サマリーでキーワードに触れているが主題は別のトピック
            - 0-2: キーワードと無関係、または単に言葉として出てくるだけ

            例（キーワードが「量子コンピュータ」の場合）:
            - タイトル「クイズ：量子コンピュータは何台？」＋サマリー「量子コンピュータの技術動向と日本の研究状況を解説...」→ 8-9点（サマリーが技術的）
            - タイトル「量子コンピュータの仕組み解説」＋サマリー「量子ビットの原理から...」→ 9-10点
            - タイトル「AIの未来」＋サマリー「...量子コンピュータについても1文だけ言及...」→ 2-3点（サマリーで軽く触れるだけ）

            記事一覧:
            {{articlesJsonStr}}

            JSON形式で回答（理由は日本語で）:
            {"evaluations": [{"id": 1, "relevance": 8, "reason": "サマリーが技術的に詳しく扱っている"}, ...]}
            """;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("あなたは技術記事の関連性を評価する専門家です。JSON形式でのみ回答してください。"),
            new UserChatMessage(prompt)
        };

        var options = new ChatCompletionOptions();

        // 推論モデルでなければ Temperature を設定
        if (!ModelCapabilities.IsReasoningModel(_openAIOptions.DeploymentName))
        {
            options.Temperature = 0.2f;
        }

        var response = await _chatClient!.CompleteChatAsync(
            messages: messages,
            options: options,
            cancellationToken: cancellationToken);

        var content = response.Value.Content[0].Text;
        var inputTokens = response.Value.Usage?.InputTokenCount ?? 0;
        var outputTokens = response.Value.Usage?.OutputTokenCount ?? 0;

        var evaluations = ParseRelevanceResponse(content, batch);
        return (evaluations, inputTokens, outputTokens);
    }

    private async Task<BatchQualityResult> EvaluateQualityBatchAsync(
        List<Article> articles,
        List<string> keywords,
        IProgress<ScoringProgress>? progress,
        Action<IEnumerable<Article>, IEnumerable<ArticleQuality>>? onQualityBatchComplete,
        CancellationToken cancellationToken)
    {
        var result = new BatchQualityResult { TotalProcessed = articles.Count };
        var evaluatedArticleIds = new HashSet<int>();

        var batches = articles
            .Select((article, index) => new { article, index })
            .GroupBy(x => x.index / _batchOptions.QualityFallback.BatchSize)
            .Select(g => g.Select(x => x.article).ToList())
            .ToList();

        var totalBatches = batches.Count;
        var processedArticles = 0;

        for (int i = 0; i < batches.Count; i++)
        {
            var batch = batches[i];
            result.ApiCallCount++;

            // 進捗通知
            progress?.Report(new ScoringProgress(
                ScoringStage.QualityEvaluation,
                i + 1, totalBatches,
                processedArticles, articles.Count,
                articles.Count,  // RelevantCount = 品質評価対象の記事数
                $"スコアリング中 ({i + 1}/{totalBatches})"
            ));

            try
            {
                var (evaluations, inputTokens, outputTokens) = await ProcessQualityBatchAsync(batch, keywords, cancellationToken);
                result.Evaluations.AddRange(evaluations);
                result.InputTokens += inputTokens;
                result.OutputTokens += outputTokens;
                processedArticles += batch.Count;

                // 記事に品質スコアを即座に反映（コールバック前に必要）
                foreach (var eval in evaluations)
                {
                    var article = batch.FirstOrDefault(a => a.Id == eval.ArticleId);
                    if (article != null)
                    {
                        article.EnsembleRelevanceScore = eval.Relevance;
                        article.TechnicalScore = eval.Technical;
                        article.NoveltyScore = eval.Novelty;
                        article.ImpactScore = eval.Impact;
                        article.QualityScore = eval.Quality;
                        article.LlmScore = eval.Total;
                        article.SummaryJa = eval.SummaryJa;
                        evaluatedArticleIds.Add(article.Id);
                    }
                }

                // 品質評価完了コールバック（このバッチで評価された記事と評価結果を通知）
                if (evaluations.Any())
                {
                    var evaluatedArticles = batch.Where(a => evaluations.Any(r => r.ArticleId == a.Id)).ToList();
                    onQualityBatchComplete?.Invoke(evaluatedArticles, evaluations);
                }

                if (_batchOptions.DelayBetweenBatchesMs > 0)
                    await Task.Delay(_batchOptions.DelayBetweenBatchesMs, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "品質バッチ評価に失敗。");
                // 品質評価失敗時はスコアなしのままにする
                processedArticles += batch.Count;

                // 失敗した記事も「完了」として通知（採点待ちリストから削除するため）
                onQualityBatchComplete?.Invoke(batch, Enumerable.Empty<ArticleQuality>());
            }
        }

        // リトライ: 評価が漏れた記事（SummaryJaが空）を1件ずつ再評価
        var missedArticles = articles.Where(a => !evaluatedArticleIds.Contains(a.Id) && string.IsNullOrEmpty(a.SummaryJa)).ToList();
        if (missedArticles.Any() && _batchOptions.MaxRetries > 0)
        {
            _logger.LogInformation("{Count}件の記事がバッチ評価で漏れました。リトライします...", missedArticles.Count);

            progress?.Report(new ScoringProgress(
                ScoringStage.QualityEvaluation,
                totalBatches, totalBatches,
                processedArticles, articles.Count,
                articles.Count,
                $"漏れた記事をリトライ中 ({missedArticles.Count}件)"
            ));

            foreach (var article in missedArticles)
            {
                result.ApiCallCount++;

                try
                {
                    // 1件ずつ評価（確実に成功させるため）
                    var (evaluations, inputTokens, outputTokens) = await ProcessQualityBatchAsync(
                        new List<Article> { article }, keywords, cancellationToken);

                    result.InputTokens += inputTokens;
                    result.OutputTokens += outputTokens;

                    if (evaluations.Any())
                    {
                        var eval = evaluations.First();
                        result.Evaluations.Add(eval);

                        article.EnsembleRelevanceScore = eval.Relevance;
                        article.TechnicalScore = eval.Technical;
                        article.NoveltyScore = eval.Novelty;
                        article.ImpactScore = eval.Impact;
                        article.QualityScore = eval.Quality;
                        article.LlmScore = eval.Total;
                        article.SummaryJa = eval.SummaryJa;
                        evaluatedArticleIds.Add(article.Id);

                        // リトライ成功時もコールバック
                        onQualityBatchComplete?.Invoke(new[] { article }, evaluations);

                        _logger.LogDebug("リトライ成功: {Title}", article.Title);
                    }

                    if (_batchOptions.DelayBetweenBatchesMs > 0)
                        await Task.Delay(_batchOptions.DelayBetweenBatchesMs, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "リトライも失敗: {Title}", article.Title);
                }
            }

            var retriedCount = missedArticles.Count(a => evaluatedArticleIds.Contains(a.Id));
            _logger.LogInformation("リトライ完了: {Success}/{Total}件が成功", retriedCount, missedArticles.Count);
        }

        return result;
    }

    private async Task<(List<ArticleQuality> Evaluations, int InputTokens, int OutputTokens)> ProcessQualityBatchAsync(
        List<Article> batch,
        List<string> keywords,
        CancellationToken cancellationToken)
    {
        var articlesJson = batch.Select((a, i) => new
        {
            id = i + 1,
            title = a.Title,
            summary = TruncateText(a.Summary, 500),
            content = TruncateText(a.Content, 1000),
            source = a.Source?.Name ?? "Unknown",
            nativeScore = a.NativeScore
        });

        var keywordsStr = string.Join(", ", keywords);
        var articlesJsonStr = JsonSerializer.Serialize(articlesJson, new JsonSerializerOptions { WriteIndented = false });

        var prompt = $$"""
            以下の技術記事（キーワード: {{keywordsStr}}）を評価し、日本語で要約してください。

            評価項目（各0-20点、合計100点満点）:
            - relevance: キーワードとの最終関連性（詳細を見た上での再評価）
            - technical: 技術的深さと重要性
            - novelty: 新規性・独自性
            - impact: 実用的な影響度・有用性
            - quality: 情報の質と信頼性

            ※ relevance が 6 未満の場合は除外対象となるため、詳細を確認して正確に評価してください

            記事一覧:
            {{articlesJsonStr}}

            JSON形式で回答:
            {
              "evaluations": [
                {
                  "id": 1,
                  "relevance": 16,
                  "technical": 14,
                  "novelty": 12,
                  "impact": 14,
                  "quality": 14,
                  "total": 70,
                  "summary_ja": "要約をここに記載..."
                }
              ]
            }

            【summary_ja の書き方】
            250-400文字程度で、記事固有の具体的な情報を簡潔に記述すること。技術者がこの要約だけで記事を読むべきか判断できる情報量を提供すること。

            ■ 禁止事項（これらを使うと評価が下がります）:
            - 「この記事は」「本記事では」で始める
            - 「〜が期待されます」「〜に寄与する可能性があります」「〜に注目が集まっています」
            - 「大きな影響を与える」「重要な意味を持つ」などの抽象的評価
            - 「技術者必見」「興味深い」などの主観的形容

            ■ 必須事項:
            - 具体的な技術名・手法名・数値・結果を含める
            - 何が・どうなった（または、どうする）を明確に書く
            - 冒頭から本題に入る

            ■ 良い例:
            「GPT-4oのファインチューニングAPIが公開された。カスタムデータで追加学習が可能になり、1000サンプルで約15%の精度向上を達成。料金はベースモデルの2倍。」

            ■ 悪い例:
            「この記事はGPT-4oのファインチューニングについて解説しています。AI技術の発展に大きな影響が期待されます。技術者にとって重要な情報が含まれています。」
            """;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("あなたは技術記事を評価する専門家です。JSON形式でのみ回答してください。"),
            new UserChatMessage(prompt)
        };

        var options = new ChatCompletionOptions();

        // 推論モデルでなければ Temperature を設定
        if (!ModelCapabilities.IsReasoningModel(_openAIOptions.DeploymentName))
        {
            options.Temperature = 0.3f;
        }

        var response = await _chatClient!.CompleteChatAsync(
            messages: messages,
            options: options,
            cancellationToken: cancellationToken);

        var content = response.Value.Content[0].Text;
        var inputTokens = response.Value.Usage?.InputTokenCount ?? 0;
        var outputTokens = response.Value.Usage?.OutputTokenCount ?? 0;

        var evaluations = ParseQualityResponse(content, batch);
        return (evaluations, inputTokens, outputTokens);
    }

    private List<ArticleRelevance> ParseRelevanceResponse(string content, List<Article> batch)
    {
        var results = new List<ArticleRelevance>();
        try
        {
            _logger.LogDebug("LLM関連性レスポンス (raw): {Content}", content);
            content = CleanJsonResponse(content);
            _logger.LogDebug("LLM関連性レスポンス (cleaned): {Content}", content);

            var response = JsonSerializer.Deserialize<RelevanceBatchResponse>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (response?.Evaluations != null)
            {
                _logger.LogInformation("LLM関連性評価: {Count}件の評価を受信", response.Evaluations.Count);

                // LLMが返すidフィールド（1-indexed）を使ってバッチ内の記事とマッチング
                foreach (var eval in response.Evaluations)
                {
                    var index = eval.Id - 1; // idは1から始まるので0-indexedに変換
                    if (index >= 0 && index < batch.Count)
                    {
                        var article = batch[index];
                        var isRelevant = eval.Relevance >= _batchOptions.EffectiveRelevanceThreshold;

                        _logger.LogDebug("記事評価: ID={Id}, Title='{Title}', Relevance={Relevance}, IsRelevant={IsRelevant}",
                            article.Id, article.Title?.Substring(0, Math.Min(30, article.Title?.Length ?? 0)),
                            eval.Relevance, isRelevant);

                        results.Add(new ArticleRelevance
                        {
                            ArticleId = article.Id,
                            RelevanceScore = Math.Clamp(eval.Relevance, 0, 10),
                            Reason = eval.Reason ?? "",
                            IsRelevant = isRelevant
                        });
                    }
                    else
                    {
                        _logger.LogWarning("無効なインデックス: LLM ID={Id}, バッチサイズ={BatchSize}", eval.Id, batch.Count);
                    }
                }

                // 評価されなかった記事をフォールバックで追加
                var evaluatedIds = results.Select(r => r.ArticleId).ToHashSet();
                foreach (var article in batch.Where(a => !evaluatedIds.Contains(a.Id)))
                {
                    _logger.LogWarning("記事 {ArticleId} ({Title}) の関連性評価がLLMレスポンスに含まれていません。フォールバック処理。",
                        article.Id, article.Title);
                    results.Add(new ArticleRelevance
                    {
                        ArticleId = article.Id,
                        RelevanceScore = 7, // デフォルト関連性スコア
                        IsRelevant = true,
                        Reason = "LLMレスポンス不足のためフォールバック"
                    });
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "関連性レスポンスのパースに失敗。全て関連ありとしてマーク。Content: {Content}", content);
            foreach (var article in batch)
            {
                results.Add(new ArticleRelevance
                {
                    ArticleId = article.Id,
                    RelevanceScore = 10,
                    IsRelevant = true,
                    Reason = "パースエラーのためフォールバック"
                });
            }
        }

        // 結果サマリーをログ出力
        var relevantCount = results.Count(r => r.IsRelevant);
        _logger.LogInformation("関連性評価結果: {Relevant}/{Total}件が関連あり (閾値: {Threshold})",
            relevantCount, results.Count, _batchOptions.EffectiveRelevanceThreshold);

        return results;
    }

    private List<ArticleQuality> ParseQualityResponse(string content, List<Article> batch)
    {
        var results = new List<ArticleQuality>();
        try
        {
            content = CleanJsonResponse(content);
            var response = JsonSerializer.Deserialize<QualityBatchResponse>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (response?.Evaluations != null)
            {
                // LLMが返すidフィールド（1-indexed）を使ってバッチ内の記事とマッチング
                foreach (var eval in response.Evaluations)
                {
                    var index = eval.Id - 1; // idは1から始まるので0-indexedに変換
                    if (index >= 0 && index < batch.Count)
                    {
                        var article = batch[index];
                        results.Add(new ArticleQuality
                        {
                            ArticleId = article.Id,
                            Relevance = Math.Clamp(eval.Relevance, 0, 20),
                            Technical = Math.Clamp(eval.Technical, 0, 20),
                            Novelty = Math.Clamp(eval.Novelty, 0, 20),
                            Impact = Math.Clamp(eval.Impact, 0, 20),
                            Quality = Math.Clamp(eval.Quality, 0, 20),
                            Total = Math.Clamp(eval.Total, 0, 100),
                            SummaryJa = eval.SummaryJa ?? ""
                        });
                    }
                }

                // 評価されなかった記事をログ出力（品質評価は必須ではないのでフォールバックは追加しない）
                var evaluatedIds = results.Select(r => r.ArticleId).ToHashSet();
                var unevaluatedCount = batch.Count(a => !evaluatedIds.Contains(a.Id));
                if (unevaluatedCount > 0)
                {
                    _logger.LogWarning("{Count}件の記事の品質評価がLLMレスポンスに含まれていません。", unevaluatedCount);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "品質レスポンスのパースに失敗。");
        }
        return results;
    }

    private BatchRelevanceResult MarkAllAsRelevant(List<Article> articles)
    {
        return new BatchRelevanceResult
        {
            TotalProcessed = articles.Count,
            RelevantCount = articles.Count,
            FilteredCount = 0,
            Evaluations = articles.Select(a => new ArticleRelevance
            {
                ArticleId = a.Id,
                RelevanceScore = 10,
                IsRelevant = true,
                Reason = "バッチ処理無効"
            }).ToList()
        };
    }

    private string CleanJsonResponse(string content)
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

    private static string TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length > maxLength ? text[..maxLength] + "..." : text;
    }

    private class RelevanceBatchResponse
    {
        public List<RelevanceEvaluation>? Evaluations { get; set; }
    }

    private class RelevanceEvaluation
    {
        public int Id { get; set; }
        public double Relevance { get; set; }
        public string? Reason { get; set; }
    }

    private class QualityBatchResponse
    {
        public List<QualityEvaluation>? Evaluations { get; set; }
    }

    private class QualityEvaluation
    {
        public int Id { get; set; }
        public int Relevance { get; set; }  // Stage 2での最終関連性 (0-20)
        public int Technical { get; set; }
        public int Novelty { get; set; }
        public int Impact { get; set; }
        public int Quality { get; set; }
        public int Total { get; set; }

        [JsonPropertyName("summary_ja")]
        public string? SummaryJa { get; set; }
    }

    #endregion

    #region アンサンブル評価

    /// <inheritdoc />
    public async Task<EnsembleEvaluationResult> EvaluateEnsembleAsync(
        Article article,
        IEnumerable<string> keywords,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var keywordList = keywords.ToList();
        var result = new EnsembleEvaluationResult { ArticleId = article.Id };

        // Judge クライアントがない場合のみフォールバック
        if (!_judgeChatClients.Any() && !_judgeResponsesClients.Any())
        {
            _logger.LogInformation("No judge clients available, falling back to single model for article: {ArticleId}", article.Id);
            return await FallbackToSingleModelEvaluationAsync(article, keywordList, cancellationToken);
        }

        // Phase 1: 全Judgeで並列評価
        var judgeEvaluations = await EvaluateWithAllJudgesAsync(article, keywordList, cancellationToken);
        result.JudgeEvaluations = judgeEvaluations;

        // 評価が得られなかった場合のフォールバック
        if (!judgeEvaluations.Any())
        {
            _logger.LogWarning("No judge evaluations obtained for article {ArticleId}, falling back", article.Id);
            return await FallbackToSingleModelEvaluationAsync(article, keywordList, cancellationToken);
        }

        // Phase 2: Meta-Judge統合評価（常に実行）
        var hasMetaJudge = (_metaJudgeChatClient != null || _metaJudgeResponsesClient != null) && _ensembleOptions.MetaJudge.IsEnabled;
        if (hasMetaJudge)
        {
            var metaResult = await EvaluateWithMetaJudgeAsync(article, keywordList, judgeEvaluations, cancellationToken);
            result.MetaJudgeResult = metaResult;
            if (metaResult != null)
            {
                ApplyMetaJudgeResults(result, metaResult);
            }
            else
            {
                // Meta-Judgeの解析に失敗した場合は重み付き平均
                _logger.LogWarning("Meta-Judge parsing failed for article {ArticleId}, using weighted average", article.Id);
                AggregateJudgeResults(result, judgeEvaluations);
            }
        }
        else
        {
            // Meta-Judgeが使えない場合は重み付き平均
            _logger.LogWarning("Meta-Judge unavailable for article {ArticleId}, using weighted average", article.Id);
            result.SkippedMetaJudge = true;
            AggregateJudgeResults(result, judgeEvaluations);
        }

        stopwatch.Stop();
        result.TotalDuration = stopwatch.Elapsed;
        result.TotalInputTokens = judgeEvaluations.Sum(j => j.InputTokens) + (result.MetaJudgeResult?.InputTokens ?? 0);
        result.TotalOutputTokens = judgeEvaluations.Sum(j => j.OutputTokens) + (result.MetaJudgeResult?.OutputTokens ?? 0);

        _logger.LogInformation(
            "Ensemble evaluation completed for article {ArticleId}: Final={FinalTotal}, Confidence={Confidence:F2}, Duration={Duration}ms",
            article.Id, result.FinalTotal, result.Confidence, stopwatch.ElapsedMilliseconds);

        return result;
    }

    /// <inheritdoc />
    public async Task<ThreeStageResult> EvaluateThreeStageAsync(
        IEnumerable<Article> articles,
        IEnumerable<string> keywords,
        bool skipRelevanceFilter,
        IProgress<ScoringProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var articleList = articles.ToList();
        var keywordList = keywords.ToList();
        var result = new ThreeStageResult();

        _logger.LogInformation("Starting 3-stage evaluation for {Count} articles (ensemble mode)",
            articleList.Count);

        // Stage 1: 関連性評価のみ実行（品質評価は行わない）
        List<Article> relevantArticles;
        if (skipRelevanceFilter)
        {
            result.RelevanceResult = MarkAllAsRelevant(articleList);
            foreach (var article in articleList)
            {
                article.IsRelevant = true;
                article.RelevanceScore = 10;
            }
            relevantArticles = articleList;
        }
        else
        {
            // Stage 1: 関連性評価のみ（EvaluateRelevanceBatchAsyncを直接呼び出し）
            _logger.LogInformation("Stage 1: {Count}件の記事の関連性を評価中...", articleList.Count);
            result.RelevanceResult = await EvaluateRelevanceBatchAsync(articleList, keywordList, progress, cancellationToken);

            // 記事にRelevanceScoreを反映
            foreach (var eval in result.RelevanceResult.Evaluations)
            {
                var article = articleList.FirstOrDefault(a => a.Id == eval.ArticleId);
                if (article != null)
                {
                    article.RelevanceScore = eval.RelevanceScore;
                    article.IsRelevant = eval.IsRelevant;
                }
            }

            // 評価されなかった記事（LLMレスポンス不足）にデフォルト値を設定
            var unevaluatedArticles = articleList.Where(a => a.IsRelevant == null).ToList();
            if (unevaluatedArticles.Any())
            {
                _logger.LogWarning("{Count}件の記事が関連性評価されませんでした。デフォルトで関連ありとしてマーク。", unevaluatedArticles.Count);
                foreach (var article in unevaluatedArticles)
                {
                    article.IsRelevant = true;
                    article.RelevanceScore = 7;
                    result.RelevanceResult.Evaluations.Add(new ArticleRelevance
                    {
                        ArticleId = article.Id,
                        RelevanceScore = 7,
                        IsRelevant = true,
                        Reason = "LLMレスポンス不足のためデフォルト設定"
                    });
                }
                result.RelevanceResult.RelevantCount += unevaluatedArticles.Count;
            }

            relevantArticles = articleList.Where(a => a.IsRelevant == true).ToList();
            _logger.LogInformation("Stage 1完了: {Relevant}件が関連あり ({Filtered}件を除外)",
                relevantArticles.Count, result.RelevanceResult.FilteredCount);
        }

        if (!relevantArticles.Any())
        {
            _logger.LogInformation("No relevant articles after Stage 1 filtering");
            stopwatch.Stop();
            result.TotalDuration = stopwatch.Elapsed;
            return result;
        }

        // Stage 2 + 3: アンサンブル評価
        var ensembleResults = new List<EnsembleEvaluationResult>();
        var processedCount = 0;

        foreach (var article in relevantArticles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ensembleResult = await EvaluateEnsembleAsync(article, keywordList, cancellationToken);
            ensembleResults.Add(ensembleResult);

            // 結果をArticleに反映
            article.TechnicalScore = ensembleResult.FinalTechnical;
            article.NoveltyScore = ensembleResult.FinalNovelty;
            article.ImpactScore = ensembleResult.FinalImpact;
            article.QualityScore = ensembleResult.FinalQuality;
            article.LlmScore = ensembleResult.FinalTotal;
            article.SummaryJa = ensembleResult.FinalSummaryJa;

            processedCount++;
            progress?.Report(new ScoringProgress(
                ScoringStage.QualityEvaluation,
                processedCount,
                relevantArticles.Count,
                processedCount,
                relevantArticles.Count,
                relevantArticles.Count,
                $"アンサンブル評価中: {processedCount}/{relevantArticles.Count}"));

            // レート制限対策の待機
            if (processedCount < relevantArticles.Count)
            {
                await Task.Delay(_batchOptions.DelayBetweenBatchesMs, cancellationToken);
            }
        }

        result.EnsembleResults = ensembleResults;
        result.MetaJudgeSkippedCount = ensembleResults.Count(e => e.SkippedMetaJudge);
        result.TotalInputTokens = result.RelevanceResult.InputTokens + ensembleResults.Sum(e => e.TotalInputTokens);
        result.TotalOutputTokens = result.RelevanceResult.OutputTokens + ensembleResults.Sum(e => e.TotalOutputTokens);
        result.TotalApiCalls = result.RelevanceResult.ApiCallCount +
            ensembleResults.Sum(e => e.JudgeEvaluations.Count) +
            ensembleResults.Count(e => e.MetaJudgeResult != null);

        stopwatch.Stop();
        result.TotalDuration = stopwatch.Elapsed;

        // コスト概算（モデル料金は変動するため概算）
        result.EstimatedCostUsd = CalculateEstimatedCost(result);

        _logger.LogInformation(
            "3-stage evaluation completed: {Relevant}/{Total} articles, Duration={Duration}s, EstimatedCost=${Cost:F4}",
            relevantArticles.Count, articleList.Count, result.TotalDuration.TotalSeconds, result.EstimatedCostUsd);

        return result;
    }

    private async Task<List<JudgeEvaluation>> EvaluateWithAllJudgesAsync(
        Article article,
        List<string> keywords,
        CancellationToken cancellationToken)
    {
        var enabledJudges = _ensembleOptions.Judges
            .Where(j => j.IsEnabled && (_judgeChatClients.ContainsKey(j.JudgeId) || _judgeResponsesClients.ContainsKey(j.JudgeId)))
            .ToList();
        var semaphore = new SemaphoreSlim(_ensembleOptions.MaxParallelJudges);
        var results = new List<JudgeEvaluation>();

        var tasks = enabledJudges.Select(async judge =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_ensembleOptions.JudgeTimeoutMs);

                return await EvaluateWithSingleJudgeAsync(article, keywords, judge, cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Judge {JudgeId} timed out for article {ArticleId}", judge.JudgeId, article.Id);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Judge {JudgeId} failed for article {ArticleId}", judge.JudgeId, article.Id);
                return null;
            }
            finally
            {
                semaphore.Release();
            }
        });

        var evaluations = await Task.WhenAll(tasks);
        return evaluations.Where(e => e != null).Cast<JudgeEvaluation>().ToList();
    }

    private async Task<JudgeEvaluation?> EvaluateWithSingleJudgeAsync(
        Article article,
        List<string> keywords,
        JudgeModelConfiguration judgeConfig,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var deploymentName = judgeConfig.DeploymentName;
        var isReasoningModel = ModelCapabilities.IsReasoningModel(deploymentName);
        var requiresResponsesApi = ModelCapabilities.RequiresResponsesApi(deploymentName);

        var systemPrompt = BuildJudgeSystemPrompt(judgeConfig.Specialty);
        var userPrompt = BuildJudgeUserPrompt(article, keywords);

        _logger.LogDebug("Judge {JudgeId} using model {Model} (reasoning={IsReasoning}, responsesApi={RequiresResponses})",
            judgeConfig.JudgeId, deploymentName, isReasoningModel, requiresResponsesApi);

        string content;
        int inputTokens = 0;
        int outputTokens = 0;

        try
        {
            if (requiresResponsesApi)
            {
                // Codexモデル: Responses APIを使用
                if (!_judgeResponsesClients.TryGetValue(judgeConfig.JudgeId, out var responsesClient))
                {
                    _logger.LogError("Judge Responses client not found: {JudgeId}", judgeConfig.JudgeId);
                    return null;
                }

                var fullPrompt = $"{systemPrompt}\n\n{userPrompt}";
                var clientResult = await responsesClient.CreateResponseAsync(fullPrompt, cancellationToken: cancellationToken);
                var response = clientResult.Value;
                content = response.GetOutputText();
                inputTokens = response.Usage?.InputTokenCount ?? 0;
                outputTokens = response.Usage?.OutputTokenCount ?? 0;
            }
            else
            {
                // 通常モデル: Chat Completion APIを使用
                if (!_judgeChatClients.TryGetValue(judgeConfig.JudgeId, out var chatClient))
                {
                    _logger.LogError("Judge Chat client not found: {JudgeId}", judgeConfig.JudgeId);
                    return null;
                }

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userPrompt)
                };

                var options = new ChatCompletionOptions();

                if (!isReasoningModel)
                {
                    var effectiveTemp = ModelCapabilities.GetEffectiveTemperature(deploymentName, judgeConfig.Temperature);
                    if (effectiveTemp.HasValue)
                    {
                        options.Temperature = effectiveTemp.Value;
                    }
                }
                else
                {
                    options.ReasoningEffortLevel = ChatReasoningEffortLevel.Low;
                }

                var response = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
                content = response.Value.Content[0].Text;
                inputTokens = response.Value.Usage?.InputTokenCount ?? 0;
                outputTokens = response.Value.Usage?.OutputTokenCount ?? 0;
            }
        }
        catch (ClientResultException ex)
        {
            _logger.LogError(ex, "Judge {JudgeId} API error. Model={Model}", judgeConfig.JudgeId, deploymentName);
            return null;
        }

        if (string.IsNullOrEmpty(content))
        {
            _logger.LogError("Judge {JudgeId} failed to get response for article {ArticleId}", judgeConfig.JudgeId, article.Id);
            return null;
        }

        stopwatch.Stop();

        var parsed = ParseJudgeEvaluation(content, judgeConfig);
        if (parsed != null)
        {
            parsed.Duration = stopwatch.Elapsed;
            parsed.InputTokens = inputTokens;
            parsed.OutputTokens = outputTokens;
        }

        return parsed;
    }

    private async Task<MetaJudgeResult?> EvaluateWithMetaJudgeAsync(
        Article article,
        List<string> keywords,
        List<JudgeEvaluation> judgeEvaluations,
        CancellationToken cancellationToken)
    {
        if (_metaJudgeChatClient == null && _metaJudgeResponsesClient == null) return null;

        var stopwatch = Stopwatch.StartNew();
        var systemPrompt = BuildMetaJudgeSystemPrompt();
        var userPrompt = BuildMetaJudgeUserPrompt(article, keywords, judgeEvaluations);

        var metaJudgeDeployment = _ensembleOptions.MetaJudge.DeploymentName;
        var isReasoningModel = ModelCapabilities.IsReasoningModel(metaJudgeDeployment);
        var requiresResponsesApi = ModelCapabilities.RequiresResponsesApi(metaJudgeDeployment);

        _logger.LogDebug("Meta-Judge using model {Model} (reasoning={IsReasoning}, responsesApi={RequiresResponses})",
            metaJudgeDeployment, isReasoningModel, requiresResponsesApi);

        string content;
        int inputTokens = 0;
        int outputTokens = 0;

        try
        {
            if (requiresResponsesApi && _metaJudgeResponsesClient != null)
            {
                // Codexモデル: Responses APIを使用
                var fullPrompt = $"{systemPrompt}\n\n{userPrompt}";
                var clientResult = await _metaJudgeResponsesClient.CreateResponseAsync(fullPrompt, cancellationToken: cancellationToken);
                var response = clientResult.Value;
                content = response.GetOutputText();
                inputTokens = response.Usage?.InputTokenCount ?? 0;
                outputTokens = response.Usage?.OutputTokenCount ?? 0;
            }
            else if (_metaJudgeChatClient != null)
            {
                // 通常モデル: Chat Completion APIを使用
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userPrompt)
                };

                var options = new ChatCompletionOptions();

                if (!isReasoningModel)
                {
                    var effectiveTemp = ModelCapabilities.GetEffectiveTemperature(metaJudgeDeployment, _ensembleOptions.MetaJudge.Temperature);
                    if (effectiveTemp.HasValue)
                    {
                        options.Temperature = effectiveTemp.Value;
                    }
                }
                else
                {
                    options.ReasoningEffortLevel = ChatReasoningEffortLevel.Low;
                }

                var response = await _metaJudgeChatClient.CompleteChatAsync(messages, options, cancellationToken);
                content = response.Value.Content[0].Text;
                inputTokens = response.Value.Usage?.InputTokenCount ?? 0;
                outputTokens = response.Value.Usage?.OutputTokenCount ?? 0;
            }
            else
            {
                _logger.LogError("Meta-Judge client not available");
                return null;
            }
        }
        catch (ClientResultException ex)
        {
            _logger.LogError(ex, "Meta-Judge API error. Model={Model}", metaJudgeDeployment);
            return null;
        }

        if (string.IsNullOrEmpty(content))
        {
            _logger.LogError("Meta-Judge failed to get response for article {ArticleId}", article.Id);
            return null;
        }

        stopwatch.Stop();

        try
        {
            var metaResult = ParseMetaJudgeResult(content);
            if (metaResult != null)
            {
                metaResult.Duration = stopwatch.Elapsed;
                metaResult.InputTokens = inputTokens;
                metaResult.OutputTokens = outputTokens;

                // 矛盾検出
                metaResult.HasContradiction = DetectContradictions(judgeEvaluations, metaResult.Contradictions);
            }

            return metaResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Meta-Judge evaluation failed for article {ArticleId}", article.Id);
            return null;
        }
    }

    private bool DetectContradictions(List<JudgeEvaluation> evaluations, List<ContradictionDetail> contradictions)
    {
        var threshold = _ensembleOptions.MetaJudge.ContradictionThreshold;
        var hasContradiction = false;

        var dimensions = new[] { "Technical", "Novelty", "Impact", "Quality" };
        var getters = new Func<JudgeEvaluation, int>[]
        {
            e => e.Technical, e => e.Novelty, e => e.Impact, e => e.Quality
        };

        for (int i = 0; i < dimensions.Length; i++)
        {
            for (int j = 0; j < evaluations.Count; j++)
            {
                for (int k = j + 1; k < evaluations.Count; k++)
                {
                    var scoreA = getters[i](evaluations[j]);
                    var scoreB = getters[i](evaluations[k]);
                    var diff = Math.Abs(scoreA - scoreB);

                    if (diff > threshold)
                    {
                        hasContradiction = true;
                        contradictions.Add(new ContradictionDetail
                        {
                            Dimension = dimensions[i],
                            JudgeA = evaluations[j].JudgeId,
                            ScoreA = scoreA,
                            JudgeB = evaluations[k].JudgeId,
                            ScoreB = scoreB,
                            Difference = diff
                        });
                    }
                }
            }
        }

        return hasContradiction;
    }

    private void AggregateJudgeResults(EnsembleEvaluationResult result, List<JudgeEvaluation> evaluations)
    {
        var totalWeight = evaluations.Sum(e => e.Weight);
        if (totalWeight == 0) totalWeight = 1;

        result.FinalTechnical = (int)Math.Round(evaluations.Sum(e => e.Technical * e.Weight) / totalWeight);
        result.FinalNovelty = (int)Math.Round(evaluations.Sum(e => e.Novelty * e.Weight) / totalWeight);
        result.FinalImpact = (int)Math.Round(evaluations.Sum(e => e.Impact * e.Weight) / totalWeight);
        result.FinalQuality = (int)Math.Round(evaluations.Sum(e => e.Quality * e.Weight) / totalWeight);
        result.FinalTotal = result.FinalTechnical + result.FinalNovelty + result.FinalImpact + result.FinalQuality;

        // 信頼度はコンセンサス度合いから計算（標準偏差の逆数）
        var stdDevs = new[]
        {
            CalculateStdDev(evaluations.Select(e => (double)e.Technical)),
            CalculateStdDev(evaluations.Select(e => (double)e.Novelty)),
            CalculateStdDev(evaluations.Select(e => (double)e.Impact)),
            CalculateStdDev(evaluations.Select(e => (double)e.Quality))
        };
        var avgStdDev = stdDevs.Average();
        result.Confidence = Math.Max(0, Math.Min(1, 1 - (avgStdDev / 25))); // 25点満点の軸を想定

        // サマリーは最初のJudgeのものを使用（後でMeta-Judgeが統合する場合は上書きされる）
        result.FinalSummaryJa = evaluations.FirstOrDefault()?.SummaryJa ?? string.Empty;
    }

    private void ApplyMetaJudgeResults(EnsembleEvaluationResult result, MetaJudgeResult metaResult)
    {
        result.FinalTechnical = metaResult.FinalTechnical;
        result.FinalNovelty = metaResult.FinalNovelty;
        result.FinalImpact = metaResult.FinalImpact;
        result.FinalQuality = metaResult.FinalQuality;
        result.FinalTotal = metaResult.FinalTotal;
        result.Confidence = metaResult.Confidence;
        result.FinalSummaryJa = metaResult.ConsolidatedSummary;
    }

    private async Task<EnsembleEvaluationResult> FallbackToSingleModelEvaluationAsync(
        Article article,
        List<string> keywords,
        CancellationToken cancellationToken)
    {
        var result = new EnsembleEvaluationResult { ArticleId = article.Id, SkippedMetaJudge = true };

        // 既存の単一モデル評価を利用
        var articles = new[] { article }.ToList();
        var twoStageResult = await EvaluateTwoStageAsync(articles, keywords, true, null, cancellationToken);

        if (twoStageResult.QualityResult.Evaluations.Any())
        {
            var quality = twoStageResult.QualityResult.Evaluations.First();
            result.FinalTechnical = quality.Technical;
            result.FinalNovelty = quality.Novelty;
            result.FinalImpact = quality.Impact;
            result.FinalQuality = quality.Quality;
            result.FinalTotal = quality.Total;
            result.FinalSummaryJa = quality.SummaryJa;
            result.Confidence = 0.7; // 単一モデルなので信頼度は固定
            result.TotalInputTokens = twoStageResult.TotalInputTokens;
            result.TotalOutputTokens = twoStageResult.TotalOutputTokens;
        }

        return result;
    }

    private double CalculateStdDev(IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count <= 1) return 0;
        var avg = list.Average();
        var sumSquares = list.Sum(v => (v - avg) * (v - avg));
        return Math.Sqrt(sumSquares / list.Count);
    }

    private decimal CalculateEstimatedCost(ThreeStageResult result)
    {
        // 概算コスト（モデルごとの料金は変動するため概算）
        // gpt-4o-mini: Input $0.15/1M, Output $0.60/1M
        // gpt-5系: Input $0.50/1M, Output $2.00/1M (概算)
        // o3系: Input $1.00/1M, Output $4.00/1M (概算)
        // o3-pro: Input $5.00/1M, Output $20.00/1M (概算)

        // 簡略化のため平均的なコストで計算
        var avgInputRate = 0.001m; // $1.00/1M
        var avgOutputRate = 0.004m; // $4.00/1M

        return (result.TotalInputTokens * avgInputRate / 1000) + (result.TotalOutputTokens * avgOutputRate / 1000);
    }

    #region Judge/Meta-Judge プロンプト

    private string BuildJudgeSystemPrompt(string? specialty)
    {
        var basePrompt = @"あなたは技術記事を評価する専門家です。
各項目を0-25点で採点し、必ず採点理由を具体的に説明してください。
回答はJSON形式で出力してください。";

        var specialtyPrompt = specialty switch
        {
            "technical" => @"
あなたは特に技術的正確性と実装の深さを評価する専門家です。
コードの品質、アーキテクチャの妥当性、技術選定の適切さに注目してください。",
            "reasoning" => @"
あなたは特に論理的一貫性と推論の質を評価する専門家です。
主張の根拠、論理展開、結論の妥当性に注目してください。",
            _ => ""
        };

        return basePrompt + specialtyPrompt;
    }

    private string BuildJudgeUserPrompt(Article article, List<string> keywords)
    {
        return $@"以下の技術記事を評価してください。

関連キーワード: {string.Join(", ", keywords)}

【記事情報】
タイトル: {article.Title}
ソース: {article.Source?.Name ?? "Unknown"}
URL: {article.Url}
概要: {TruncateText(article.Summary, 1000)}

以下のJSON形式で回答してください:
{{
  ""technical"": 0-25の整数,
  ""technical_reason"": ""技術的深さの評価理由"",
  ""novelty"": 0-25の整数,
  ""novelty_reason"": ""新規性の評価理由"",
  ""impact"": 0-25の整数,
  ""impact_reason"": ""影響度の評価理由"",
  ""quality"": 0-25の整数,
  ""quality_reason"": ""品質の評価理由"",
  ""total"": 0-100の整数（各項目の合計）,
  ""summary_ja"": ""記事の要約（日本語、200字程度）""
}}";
    }

    private string BuildMetaJudgeSystemPrompt()
    {
        return @"あなたは複数の評価者の判断を統合する総括評価者です。

タスク:
1. 各評価者のスコアと理由を分析
2. 矛盾点を特定し、妥当な解決策を提示
3. 各評価者の専門性と重みを考慮した最終スコアを決定
4. 信頼度（0.0-1.0）を算出
5. 要約を統合・改善

回答はJSON形式で出力してください。";
    }

    private string BuildMetaJudgeUserPrompt(Article article, List<string> keywords, List<JudgeEvaluation> evaluations)
    {
        var evaluationsSummary = string.Join("\n", evaluations.Select(e =>
            $@"【{e.JudgeDisplayName}】(重み: {e.Weight})
  Technical: {e.Technical} - {e.TechnicalReason}
  Novelty: {e.Novelty} - {e.NoveltyReason}
  Impact: {e.Impact} - {e.ImpactReason}
  Quality: {e.Quality} - {e.QualityReason}
  要約: {e.SummaryJa}"));

        return $@"以下の記事に対する複数評価者の判断を統合してください。

【記事】
タイトル: {article.Title}
ソース: {article.Source?.Name ?? "Unknown"}
キーワード: {string.Join(", ", keywords)}

【各評価者の判断】
{evaluationsSummary}

以下のJSON形式で最終判断を出力してください:
{{
  ""final_technical"": 0-25の整数,
  ""final_novelty"": 0-25の整数,
  ""final_impact"": 0-25の整数,
  ""final_quality"": 0-25の整数,
  ""final_total"": 0-100の整数,
  ""confidence"": 0.0-1.0の小数（判断の信頼度）,
  ""rationale"": ""最終判断の根拠（評価者間の差異がある場合はその解決理由も含む）"",
  ""consolidated_summary"": ""統合された要約（日本語、300字程度）""
}}";
    }

    private JudgeEvaluation? ParseJudgeEvaluation(string content, JudgeModelConfiguration judgeConfig)
    {
        try
        {
            var cleaned = CleanJsonResponse(content);
            _logger.LogDebug("Judge {JudgeId} raw response: {Content}", judgeConfig.JudgeId, content.Substring(0, Math.Min(500, content.Length)));
            _logger.LogDebug("Judge {JudgeId} cleaned JSON: {Cleaned}", judgeConfig.JudgeId, cleaned.Substring(0, Math.Min(500, cleaned.Length)));

            var json = JsonDocument.Parse(cleaned);
            var root = json.RootElement;

            // スコアを取得（数値/文字列両対応）
            int GetScoreValue(JsonElement element, string propName)
            {
                if (!element.TryGetProperty(propName, out var prop)) return 0;
                if (prop.ValueKind == JsonValueKind.Number) return prop.GetInt32();
                if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var val)) return val;
                return 0;
            }

            var evaluation = new JudgeEvaluation
            {
                JudgeId = judgeConfig.JudgeId,
                JudgeDisplayName = judgeConfig.EffectiveDisplayName,
                Weight = judgeConfig.Weight,
                Technical = GetScoreValue(root, "technical"),
                TechnicalReason = root.TryGetProperty("technical_reason", out var tr) ? tr.GetString() ?? "" : "",
                Novelty = GetScoreValue(root, "novelty"),
                NoveltyReason = root.TryGetProperty("novelty_reason", out var nr) ? nr.GetString() ?? "" : "",
                Impact = GetScoreValue(root, "impact"),
                ImpactReason = root.TryGetProperty("impact_reason", out var ir) ? ir.GetString() ?? "" : "",
                Quality = GetScoreValue(root, "quality"),
                QualityReason = root.TryGetProperty("quality_reason", out var qr) ? qr.GetString() ?? "" : "",
                Total = GetScoreValue(root, "total"),
                SummaryJa = root.TryGetProperty("summary_ja", out var s) ? s.GetString() ?? "" : ""
            };

            // スコアが全て0の場合は警告
            if (evaluation.Technical == 0 && evaluation.Novelty == 0 && evaluation.Impact == 0 && evaluation.Quality == 0)
            {
                _logger.LogWarning("Judge {JudgeId} returned all zero scores. Raw response: {Content}",
                    judgeConfig.JudgeId, content.Substring(0, Math.Min(1000, content.Length)));
            }
            else
            {
                _logger.LogInformation("Judge {JudgeId} scores: T={Technical}, N={Novelty}, I={Impact}, Q={Quality}, Total={Total}",
                    judgeConfig.JudgeId, evaluation.Technical, evaluation.Novelty, evaluation.Impact, evaluation.Quality, evaluation.Total);
            }

            return evaluation;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse judge {JudgeId} evaluation response. Raw content: {Content}",
                judgeConfig.JudgeId, content.Substring(0, Math.Min(1000, content.Length)));
            return null;
        }
    }

    private MetaJudgeResult? ParseMetaJudgeResult(string content)
    {
        try
        {
            var cleaned = CleanJsonResponse(content);
            var json = JsonDocument.Parse(cleaned);
            var root = json.RootElement;

            return new MetaJudgeResult
            {
                FinalTechnical = root.TryGetProperty("final_technical", out var t) ? t.GetInt32() : 0,
                FinalNovelty = root.TryGetProperty("final_novelty", out var n) ? n.GetInt32() : 0,
                FinalImpact = root.TryGetProperty("final_impact", out var i) ? i.GetInt32() : 0,
                FinalQuality = root.TryGetProperty("final_quality", out var q) ? q.GetInt32() : 0,
                FinalTotal = root.TryGetProperty("final_total", out var total) ? total.GetInt32() : 0,
                Confidence = root.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0.5,
                Rationale = root.TryGetProperty("rationale", out var r) ? r.GetString() ?? "" : "",
                ConsolidatedSummary = root.TryGetProperty("consolidated_summary", out var s) ? s.GetString() ?? "" : ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse meta-judge response");
            return null;
        }
    }

    #endregion

    #endregion
}

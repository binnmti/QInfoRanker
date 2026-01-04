using System.ClientModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using QInfoRanker.Core.Entities;
using QInfoRanker.Core.Interfaces.Services;

namespace QInfoRanker.Infrastructure.Scoring;

public class ScoringService : IScoringService
{
    private readonly AzureOpenAIOptions _openAIOptions;
    private readonly ScoringOptions _scoringOptions;
    private readonly BatchScoringOptions _batchOptions;
    private readonly ILogger<ScoringService> _logger;
    private readonly AzureOpenAIClient? _client;
    private readonly ChatClient? _chatClient;

    public ScoringService(
        IOptions<AzureOpenAIOptions> openAIOptions,
        IOptions<ScoringOptions> scoringOptions,
        IOptions<BatchScoringOptions> batchOptions,
        ILogger<ScoringService> logger)
    {
        _openAIOptions = openAIOptions.Value;
        _scoringOptions = scoringOptions.Value;
        _batchOptions = batchOptions.Value;
        _logger = logger;

        if (!string.IsNullOrEmpty(_openAIOptions.Endpoint) && !string.IsNullOrEmpty(_openAIOptions.ApiKey))
        {
            _client = new AzureOpenAIClient(
                new Uri(_openAIOptions.Endpoint),
                new ApiKeyCredential(_openAIOptions.ApiKey));
            _chatClient = _client.GetChatClient(_openAIOptions.DeploymentName);
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

            var options = new ChatCompletionOptions
            {
                MaxOutputTokenCount = _openAIOptions.MaxTokens,
                Temperature = _openAIOptions.Temperature
            };

            var response = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
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
        double baseScore;

        if (source.HasNativeScore && article.NativeScore.HasValue)
        {
            // Hybrid scoring: combine native and LLM scores
            var normalizedNative = NormalizeNativeScore(article.NativeScore, source.Name);
            var llmScore = article.LlmScore ?? 0;

            baseScore = (normalizedNative * _scoringOptions.EffectiveNativeScoreWeight) +
                        (llmScore * _scoringOptions.EffectiveLlmScoreWeight);
        }
        else
        {
            // LLM-only scoring with authority bonus
            var llmScore = article.LlmScore ?? 0;
            baseScore = llmScore * _scoringOptions.LlmOnlyWeight;
        }

        // Add authority bonus
        baseScore += source.AuthorityWeight * _scoringOptions.AuthorityBonusMultiplier;

        // Apply relevance multiplier - 関連性スコアを最終スコアに大きく反映
        // RelevanceScore 10 → 100%, 5 → 50%, 0 → 0%
        var relevanceMultiplier = (article.RelevanceScore ?? 10.0) / 10.0;
        var finalScore = baseScore * relevanceMultiplier;

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

            var options = new ChatCompletionOptions
            {
                MaxOutputTokenCount = 10,
                Temperature = 0
            };

            var response = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
            return response.Value.Content.Count > 0;
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
            Evaluate the following article and provide scores (0-25 each) for:
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
            Where total is the sum of all scores (0-100).
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

    public async Task<TwoStageResult> EvaluateTwoStageAsync(
        IEnumerable<Article> articles,
        IEnumerable<string> keywords,
        bool skipRelevanceFilter = false, // 後方互換性のため残すが、常にStage 1を実行
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
        result.RelevanceResult = await EvaluateRelevanceBatchAsync(articleList, keywordList, cancellationToken);

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

        if (relevantArticles.Any())
        {
            result.QualityResult = await EvaluateQualityBatchAsync(relevantArticles, keywordList, cancellationToken);

            // 記事に品質スコアを反映
            var evaluatedArticleIds = new HashSet<int>();
            foreach (var eval in result.QualityResult.Evaluations)
            {
                var article = relevantArticles.FirstOrDefault(a => a.Id == eval.ArticleId);
                if (article != null)
                {
                    article.TechnicalScore = eval.Technical;
                    article.NoveltyScore = eval.Novelty;
                    article.ImpactScore = eval.Impact;
                    article.QualityScore = eval.Quality;
                    article.LlmScore = eval.Total;
                    article.SummaryJa = eval.SummaryJa;
                    evaluatedArticleIds.Add(article.Id);
                }
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

        _logger.LogInformation(
            "2段階評価完了: {Total}件中{Relevant}件が関連あり, API呼び出し{ApiCalls}回, 処理時間{Duration}ms",
            articleList.Count, relevantArticles.Count, result.TotalApiCalls, result.TotalDuration.TotalMilliseconds);

        return result;
    }

    private async Task<BatchRelevanceResult> EvaluateRelevanceBatchAsync(
        List<Article> articles,
        List<string> keywords,
        CancellationToken cancellationToken)
    {
        var result = new BatchRelevanceResult { TotalProcessed = articles.Count };

        var batches = articles
            .Select((article, index) => new { article, index })
            .GroupBy(x => x.index / _batchOptions.RelevanceBatchSize)
            .Select(g => g.Select(x => x.article).ToList())
            .ToList();

        foreach (var batch in batches)
        {
            result.ApiCallCount++;
            try
            {
                var batchResult = await ProcessRelevanceBatchAsync(batch, keywords, cancellationToken);
                result.Evaluations.AddRange(batchResult);

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
            }
        }

        result.RelevantCount = result.Evaluations.Count(e => e.IsRelevant);
        result.FilteredCount = result.TotalProcessed - result.RelevantCount;
        return result;
    }

    private async Task<List<ArticleRelevance>> ProcessRelevanceBatchAsync(
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

            【重要】キーワードが記事内に単に「含まれている」だけでは高評価にしないでください。
            記事の主題・内容がキーワードのトピックについて実際に書かれているかを判断してください。

            評価基準:
            - 9-10: 記事の主題がキーワードのトピックそのもの（技術解説、入門記事、実装例など）
            - 7-8: キーワードのトピックについて実質的・技術的に扱っている
            - 5-6: キーワードのトピックに部分的に関連（関連技術、応用例など）
            - 3-4: キーワードに触れているが主題は別のトピック
            - 0-2: キーワードと無関係、または単に言葉として出てくるだけ

            例（キーワードが「量子コンピュータ」の場合）:
            - 「量子コンピュータの仕組み解説」→ 9-10点
            - 「量子アルゴリズムの実装」→ 8-9点
            - 「AIと量子コンピュータの未来」→ 5-6点（量子は副題）
            - 「ゲームの乱数生成について（量子の話を1行だけ言及）」→ 1-2点

            記事一覧:
            {{articlesJsonStr}}

            JSON形式で回答（理由は日本語で）:
            {"evaluations": [{"id": 1, "relevance": 8, "reason": "直接的なチュートリアル"}, ...]}
            """;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("あなたは技術記事の関連性を評価する専門家です。JSON形式でのみ回答してください。"),
            new UserChatMessage(prompt)
        };

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = _batchOptions.RelevanceMaxTokens,
            Temperature = 0.2f
        };

        var response = await _chatClient!.CompleteChatAsync(messages, options, cancellationToken);
        var content = response.Value.Content[0].Text;

        return ParseRelevanceResponse(content, batch);
    }

    private async Task<BatchQualityResult> EvaluateQualityBatchAsync(
        List<Article> articles,
        List<string> keywords,
        CancellationToken cancellationToken)
    {
        var result = new BatchQualityResult { TotalProcessed = articles.Count };

        var batches = articles
            .Select((article, index) => new { article, index })
            .GroupBy(x => x.index / _batchOptions.QualityBatchSize)
            .Select(g => g.Select(x => x.article).ToList())
            .ToList();

        foreach (var batch in batches)
        {
            result.ApiCallCount++;
            try
            {
                var batchResult = await ProcessQualityBatchAsync(batch, keywords, cancellationToken);
                result.Evaluations.AddRange(batchResult);

                if (_batchOptions.DelayBetweenBatchesMs > 0)
                    await Task.Delay(_batchOptions.DelayBetweenBatchesMs, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "品質バッチ評価に失敗。");
                // 品質評価失敗時はスコアなしのままにする
            }
        }

        return result;
    }

    private async Task<List<ArticleQuality>> ProcessQualityBatchAsync(
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
            以下の技術記事（キーワード: {{keywordsStr}}）を評価し、日本語で詳細に要約してください。

            評価項目（各0-25点）:
            - technical: 技術的深さと重要性
            - novelty: 新規性・独自性
            - impact: 実用的な影響度・有用性
            - quality: 情報の質と信頼性

            記事一覧:
            {{articlesJsonStr}}

            JSON形式で回答:
            {
              "evaluations": [
                {
                  "id": 1,
                  "technical": 20,
                  "novelty": 15,
                  "impact": 18,
                  "quality": 17,
                  "total": 70,
                  "summary_ja": "詳細な要約をここに記載..."
                }
              ]
            }

            summary_ja: 記事の内容を詳細に日本語で要約（250-400文字程度）。以下の観点を含めること:
            - 記事の主題と目的
            - 主要な技術的内容・手法
            - 重要なポイントや発見
            - 実用的な意義や応用可能性
            技術者がこの要約だけで記事を読むべきか判断できる情報量を提供すること。
            """;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("あなたは技術記事を評価する専門家です。JSON形式でのみ回答してください。"),
            new UserChatMessage(prompt)
        };

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = _batchOptions.QualityMaxTokens,
            Temperature = 0.3f
        };

        var response = await _chatClient!.CompleteChatAsync(messages, options, cancellationToken);
        var content = response.Value.Content[0].Text;

        return ParseQualityResponse(content, batch);
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
                            Technical = Math.Clamp(eval.Technical, 0, 25),
                            Novelty = Math.Clamp(eval.Novelty, 0, 25),
                            Impact = Math.Clamp(eval.Impact, 0, 25),
                            Quality = Math.Clamp(eval.Quality, 0, 25),
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
        public int Technical { get; set; }
        public int Novelty { get; set; }
        public int Impact { get; set; }
        public int Quality { get; set; }
        public int Total { get; set; }

        [JsonPropertyName("summary_ja")]
        public string? SummaryJa { get; set; }
    }

    #endregion
}

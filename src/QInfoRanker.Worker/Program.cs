using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QInfoRanker.Core.Interfaces.Services;
using QInfoRanker.Infrastructure;
using QInfoRanker.Infrastructure.Data;

namespace QInfoRanker.Worker;

/// <summary>
/// 定期収集Workerのエントリーポイント
/// Container Apps Jobからスケジュール実行される
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("QInfoRanker Worker - 定期収集ジョブ");
        Console.WriteLine($"開始時刻: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine("========================================");

        try
        {
            // 設定の読み込み
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            // DIコンテナの構築
            var services = new ServiceCollection();

            // ロギング
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Infrastructure層のサービス登録
            services.AddInfrastructure(configuration);

            // ICollectionProgressNotifierのダミー実装（Worker用）
            services.AddScoped<ICollectionProgressNotifier, ConsoleProgressNotifier>();

            var serviceProvider = services.BuildServiceProvider();

            // データベース接続確認（マイグレーションはWeb Appで実行済み）
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Console.WriteLine("データベース接続を確認中...");
                // 接続テスト（マイグレーションは実行しない - Web Appで管理）
                var canConnect = await dbContext.Database.CanConnectAsync();
                if (!canConnect)
                {
                    Console.WriteLine("データベースに接続できません");
                    return 1;
                }
                Console.WriteLine("データベース接続OK");
            }

            // 収集実行
            using (var scope = serviceProvider.CreateScope())
            {
                var collectionService = scope.ServiceProvider.GetRequiredService<ICollectionService>();
                var keywordService = scope.ServiceProvider.GetRequiredService<IKeywordService>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                // 有効なキーワードを取得
                var keywords = await keywordService.GetActiveAsync();
                var keywordList = keywords.ToList();

                if (!keywordList.Any())
                {
                    Console.WriteLine("有効なキーワードがありません。終了します。");
                    return 0;
                }

                Console.WriteLine($"収集対象: {keywordList.Count} キーワード");
                foreach (var keyword in keywordList)
                {
                    Console.WriteLine($"  - {keyword.Term} (ID: {keyword.Id})");
                }
                Console.WriteLine();

                // 各キーワードを順次収集
                var successCount = 0;
                var failCount = 0;

                foreach (var keyword in keywordList)
                {
                    try
                    {
                        Console.WriteLine($"========================================");
                        Console.WriteLine($"収集開始: {keyword.Term}");
                        Console.WriteLine($"========================================");

                        await collectionService.CollectForKeywordAsync(keyword.Id);

                        successCount++;
                        Console.WriteLine($"収集完了: {keyword.Term}");
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        logger.LogError(ex, "収集エラー: {Keyword}", keyword.Term);
                        Console.WriteLine($"収集エラー: {keyword.Term} - {ex.Message}");
                    }
                }

                Console.WriteLine();
                Console.WriteLine("========================================");
                Console.WriteLine($"全収集完了: 成功 {successCount}, 失敗 {failCount}");
                Console.WriteLine($"終了時刻: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine("========================================");

                return failCount > 0 ? 1 : 0;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"致命的エラー: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }
}

/// <summary>
/// コンソール出力用の進捗通知（Worker用）
/// WebアプリのSignalR通知の代わりにコンソール出力する
/// </summary>
public class ConsoleProgressNotifier : ICollectionProgressNotifier
{
    public Task NotifyProgressAsync(QInfoRanker.Core.Events.CollectionProgressEvent progressEvent)
    {
        Console.WriteLine($"[進捗] {progressEvent.Message}");
        return Task.CompletedTask;
    }

    public Task NotifyCompletedAsync(QInfoRanker.Core.Events.CollectionCompletedEvent completedEvent)
    {
        Console.WriteLine($"[完了] {completedEvent.KeywordTerm}: {completedEvent.TotalArticles}件収集, {completedEvent.ScoredArticles}件スコアリング, {completedEvent.DurationSeconds:F1}秒");
        return Task.CompletedTask;
    }

    public Task NotifyErrorAsync(QInfoRanker.Core.Events.CollectionErrorEvent errorEvent)
    {
        Console.WriteLine($"[エラー] {errorEvent.Source}: {errorEvent.Message}");
        return Task.CompletedTask;
    }

    public Task NotifyArticlesFetchedAsync(QInfoRanker.Core.Events.ArticlesFetchedEvent fetchedEvent)
    {
        Console.WriteLine($"[取得] {fetchedEvent.SourceName}: {fetchedEvent.Articles.Count}件");
        return Task.CompletedTask;
    }

    public Task NotifySourceCompletedAsync(QInfoRanker.Core.Events.SourceCompletedEvent sourceCompletedEvent)
    {
        var status = sourceCompletedEvent.HasError ? "エラーあり" : "成功";
        Console.WriteLine($"[ソース完了] {sourceCompletedEvent.SourceName}: {sourceCompletedEvent.ArticleCount}件収集, {sourceCompletedEvent.ScoredCount}件スコア ({status})");
        return Task.CompletedTask;
    }

    public Task NotifyArticlesPassedFilterAsync(QInfoRanker.Core.Events.ArticlesPassedFilterEvent passedFilterEvent)
    {
        Console.WriteLine($"[フィルタ通過] {passedFilterEvent.SourceName}: {passedFilterEvent.Articles.Count}件");
        return Task.CompletedTask;
    }

    public Task NotifyArticlesScoredAsync(QInfoRanker.Core.Events.ArticlesScoredEvent scoredEvent)
    {
        Console.WriteLine($"[スコアリング] {scoredEvent.SourceName}: {scoredEvent.ScoredCount}件, 平均スコア {scoredEvent.AverageScore:F1}");
        return Task.CompletedTask;
    }

    public Task NotifyArticlesQualityScoredAsync(int keywordId, IEnumerable<ScoredArticlePreview> scoredPreviews)
    {
        var previews = scoredPreviews.ToList();
        Console.WriteLine($"[品質評価完了] {previews.Count}件");
        return Task.CompletedTask;
    }

    public Task NotifyTokenUsageAsync(int keywordId, int inputTokens, int outputTokens)
    {
        Console.WriteLine($"[トークン使用量] 入力: {inputTokens}, 出力: {outputTokens}");
        return Task.CompletedTask;
    }
}

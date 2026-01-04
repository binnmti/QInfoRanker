using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QInfoRanker.Core.Exceptions;
using QInfoRanker.Core.Interfaces.Services;

namespace QInfoRanker.Infrastructure.Services;

public class CollectionBackgroundService : BackgroundService
{
    private readonly ICollectionQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CollectionBackgroundService> _logger;

    public CollectionBackgroundService(
        ICollectionQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<CollectionBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("収集バックグラウンドサービスを開始しました");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _queue.DequeueAsync(stoppingToken);
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "収集ジョブ処理中にエラーが発生しました");
            }
        }

        _logger.LogInformation("収集バックグラウンドサービスを停止しました");
    }

    private async Task ProcessJobAsync(CollectionJob job, CancellationToken cancellationToken)
    {
        var debugModeLabel = job.DebugMode ? " [DEBUG]" : "";
        _logger.LogInformation("収集を開始: {Keyword} (ID: {KeywordId}){DebugMode}",
            job.KeywordTerm, job.KeywordId, debugModeLabel);

        var status = new CollectionStatus
        {
            KeywordId = job.KeywordId,
            KeywordTerm = job.KeywordTerm,
            State = CollectionState.Collecting,
            StartedAt = DateTime.UtcNow,
            Message = job.DebugMode ? "デバッグモードで収集中..." : "記事を収集中...",
            SourceIndex = 0,
            TotalSources = 0,
            ArticlesCollected = 0,
            ArticlesScored = 0
        };
        _queue.UpdateStatus(job.KeywordId, status);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var collectionService = scope.ServiceProvider.GetRequiredService<ICollectionService>();

            // デバッグモードフラグを渡す
            await collectionService.CollectForKeywordAsync(
                job.KeywordId,
                job.DebugMode,
                job.DebugArticleLimit,
                cancellationToken);

            // 完了ステータスは CollectionService 内で更新されるが、念のため
            var currentStatus = _queue.GetStatus(job.KeywordId);
            if (currentStatus != null && currentStatus.State != CollectionState.Completed)
            {
                currentStatus.State = CollectionState.Completed;
                currentStatus.CompletedAt = DateTime.UtcNow;
                currentStatus.Message = "収集が完了しました";
                _queue.UpdateStatus(job.KeywordId, currentStatus);
            }

            _logger.LogInformation("収集が完了: {Keyword}{DebugMode}", job.KeywordTerm, debugModeLabel);

            // 5分後にステータスをクリア
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(5), CancellationToken.None);
                _queue.ClearStatus(job.KeywordId);
            });
        }
        catch (ScoringServiceUnavailableException ex)
        {
            // 致命的エラー: AIサービス使用不可
            _logger.LogError(ex, "致命的エラー: AIサービス使用不可 - {Keyword}", job.KeywordTerm);

            var currentStatus = _queue.GetStatus(job.KeywordId) ?? status;
            currentStatus.State = CollectionState.Failed;
            currentStatus.CompletedAt = DateTime.UtcNow;
            currentStatus.HasFatalError = true;
            currentStatus.FatalErrorMessage = $"AIサービスに接続できません: {ex.Message}";
            currentStatus.Message = "致命的エラー: AIサービス使用不可";
            _queue.UpdateStatus(job.KeywordId, currentStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "収集エラー: {Keyword}", job.KeywordTerm);

            var currentStatus = _queue.GetStatus(job.KeywordId) ?? status;
            currentStatus.State = CollectionState.Failed;
            currentStatus.CompletedAt = DateTime.UtcNow;
            currentStatus.Message = $"エラー: {ex.Message}";
            _queue.UpdateStatus(job.KeywordId, currentStatus);
        }
    }
}

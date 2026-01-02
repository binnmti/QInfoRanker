using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
        _logger.LogInformation("収集を開始: {Keyword} (ID: {KeywordId})", job.KeywordTerm, job.KeywordId);

        var status = new CollectionStatus
        {
            KeywordId = job.KeywordId,
            KeywordTerm = job.KeywordTerm,
            State = CollectionState.Collecting,
            StartedAt = DateTime.UtcNow,
            Message = "記事を収集中..."
        };
        _queue.UpdateStatus(job.KeywordId, status);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var collectionService = scope.ServiceProvider.GetRequiredService<ICollectionService>();

            await collectionService.CollectForKeywordAsync(job.KeywordId, cancellationToken);

            status.State = CollectionState.Completed;
            status.CompletedAt = DateTime.UtcNow;
            status.Message = "収集が完了しました";
            _queue.UpdateStatus(job.KeywordId, status);

            _logger.LogInformation("収集が完了: {Keyword}", job.KeywordTerm);

            // 5分後にステータスをクリア
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(5), CancellationToken.None);
                _queue.ClearStatus(job.KeywordId);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "収集エラー: {Keyword}", job.KeywordTerm);

            status.State = CollectionState.Failed;
            status.CompletedAt = DateTime.UtcNow;
            status.Message = $"エラー: {ex.Message}";
            _queue.UpdateStatus(job.KeywordId, status);
        }
    }
}

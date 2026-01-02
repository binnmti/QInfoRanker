using System.Collections.Concurrent;
using System.Threading.Channels;
using QInfoRanker.Core.Interfaces.Services;

namespace QInfoRanker.Infrastructure.Services;

public class CollectionQueue : ICollectionQueue
{
    private readonly Channel<CollectionJob> _queue;
    private readonly ConcurrentDictionary<int, CollectionStatus> _statuses = new();

    public CollectionQueue()
    {
        _queue = Channel.CreateUnbounded<CollectionJob>(new UnboundedChannelOptions
        {
            SingleReader = true
        });
    }

    public async ValueTask EnqueueAsync(CollectionJob job, CancellationToken cancellationToken = default)
    {
        _statuses[job.KeywordId] = new CollectionStatus
        {
            KeywordId = job.KeywordId,
            KeywordTerm = job.KeywordTerm,
            State = CollectionState.Queued,
            StartedAt = DateTime.UtcNow,
            Message = "キューに追加されました"
        };

        await _queue.Writer.WriteAsync(job, cancellationToken);
    }

    public async ValueTask<CollectionJob> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }

    public CollectionStatus? GetStatus(int keywordId)
    {
        return _statuses.TryGetValue(keywordId, out var status) ? status : null;
    }

    public IEnumerable<CollectionStatus> GetAllStatuses()
    {
        return _statuses.Values.OrderByDescending(s => s.StartedAt);
    }

    public void UpdateStatus(int keywordId, CollectionStatus status)
    {
        _statuses[keywordId] = status;
    }

    public void ClearStatus(int keywordId)
    {
        _statuses.TryRemove(keywordId, out _);
    }
}

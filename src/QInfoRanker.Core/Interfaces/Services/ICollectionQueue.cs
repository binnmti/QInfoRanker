namespace QInfoRanker.Core.Interfaces.Services;

public interface ICollectionQueue
{
    ValueTask EnqueueAsync(CollectionJob job, CancellationToken cancellationToken = default);
    ValueTask<CollectionJob> DequeueAsync(CancellationToken cancellationToken);
    CollectionStatus? GetStatus(int keywordId);
    IEnumerable<CollectionStatus> GetAllStatuses();
    void UpdateStatus(int keywordId, CollectionStatus status);
    void ClearStatus(int keywordId);
}

public class CollectionJob
{
    public int KeywordId { get; set; }
    public string KeywordTerm { get; set; } = string.Empty;
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
}

public class CollectionStatus
{
    public int KeywordId { get; set; }
    public string KeywordTerm { get; set; } = string.Empty;
    public CollectionState State { get; set; }
    public string? CurrentSource { get; set; }
    public int ArticlesCollected { get; set; }
    public int ArticlesScored { get; set; }
    public string? Message { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum CollectionState
{
    Queued,
    Collecting,
    Scoring,
    Completed,
    Failed
}

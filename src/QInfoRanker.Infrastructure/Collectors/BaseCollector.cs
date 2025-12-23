using QInfoRanker.Core.Entities;
using QInfoRanker.Core.Interfaces;

namespace QInfoRanker.Infrastructure.Collectors;

/// <summary>
/// Base abstract class for article collectors
/// </summary>
public abstract class BaseCollector : ICollector
{
    protected readonly HttpClient _httpClient;

    protected BaseCollector(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public abstract Task<IEnumerable<Article>> CollectAsync(Source source, string keyword, DateTime? since = null);

    public abstract bool CanHandle(SourceType sourceType);

    /// <summary>
    /// Normalize native scores to 0-100 scale
    /// </summary>
    protected double NormalizeScore(int score, int maxScore = 100)
    {
        if (maxScore == 0) return 0;
        return Math.Min(100, (score / (double)maxScore) * 100);
    }

    /// <summary>
    /// Replace keyword placeholder in URL template
    /// </summary>
    protected string BuildSearchUrl(Source source, string keyword)
    {
        if (string.IsNullOrEmpty(source.SearchUrlTemplate))
            return source.Url;

        return source.SearchUrlTemplate.Replace("{keyword}", Uri.EscapeDataString(keyword));
    }
}

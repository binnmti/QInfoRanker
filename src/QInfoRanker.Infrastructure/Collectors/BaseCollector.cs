using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using QInfoRanker.Core.Entities;
using QInfoRanker.Core.Interfaces.Collectors;

namespace QInfoRanker.Infrastructure.Collectors;

public abstract class BaseCollector : ICollector
{
    protected readonly HttpClient HttpClient;
    protected readonly ILogger Logger;

    protected BaseCollector(HttpClient httpClient, ILogger logger)
    {
        HttpClient = httpClient;
        Logger = logger;
    }

    public abstract string SourceName { get; }

    public abstract bool CanHandle(Source source);

    public abstract Task<IEnumerable<Article>> CollectAsync(
        Source source,
        string keyword,
        DateTime? since = null,
        CancellationToken cancellationToken = default);

    protected string BuildSearchUrl(Source source, string keyword)
    {
        if (string.IsNullOrEmpty(source.SearchUrlTemplate))
        {
            return source.Url;
        }

        return source.SearchUrlTemplate.Replace("{keyword}", Uri.EscapeDataString(keyword));
    }

    protected async Task<T?> GetJsonAsync<T>(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            return await HttpClient.GetFromJsonAsync<T>(url, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to fetch JSON from {Url}", url);
            return default;
        }
    }

    protected async Task<string?> GetStringAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            return await HttpClient.GetStringAsync(url, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to fetch content from {Url}", url);
            return null;
        }
    }
}

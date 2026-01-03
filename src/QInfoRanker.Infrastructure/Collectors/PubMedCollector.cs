using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using QInfoRanker.Core.Entities;

namespace QInfoRanker.Infrastructure.Collectors;

public class PubMedCollector : BaseCollector
{
    private const string ESearchBaseUrl = "https://eutils.ncbi.nlm.nih.gov/entrez/eutils/esearch.fcgi";
    private const string EFetchBaseUrl = "https://eutils.ncbi.nlm.nih.gov/entrez/eutils/efetch.fcgi";

    public PubMedCollector(HttpClient httpClient, ILogger<PubMedCollector> logger)
        : base(httpClient, logger)
    {
    }

    public override string SourceName => "PubMed";

    public override bool CanHandle(Source source)
    {
        return source.Name.Contains("PubMed", StringComparison.OrdinalIgnoreCase) ||
               source.Url.Contains("pubmed", StringComparison.OrdinalIgnoreCase) ||
               source.Url.Contains("ncbi.nlm.nih.gov", StringComparison.OrdinalIgnoreCase);
    }

    public override async Task<IEnumerable<Article>> CollectAsync(
        Source source,
        string keyword,
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        var articles = new List<Article>();

        try
        {
            Logger.LogInformation("Collecting from PubMed for keyword: {Keyword}", keyword);

            // Step 1: Search for PMIDs
            var pmids = await SearchPmidsAsync(keyword, since, cancellationToken);

            if (pmids.Count == 0)
            {
                Logger.LogWarning("No results from PubMed for keyword: {Keyword}", keyword);
                return articles;
            }

            Logger.LogInformation("Found {Count} PMIDs from PubMed", pmids.Count);

            // Step 2: Fetch article details
            articles = await FetchArticleDetailsAsync(source, pmids, cancellationToken);

            Logger.LogInformation("Collected {Count} articles from PubMed", articles.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error collecting from PubMed");
        }

        return articles;
    }

    private async Task<List<string>> SearchPmidsAsync(
        string keyword,
        DateTime? since,
        CancellationToken cancellationToken)
    {
        var pmids = new List<string>();

        var searchUrl = $"{ESearchBaseUrl}?db=pubmed&term={Uri.EscapeDataString(keyword)}&retmax=50&retmode=json&sort=date";

        // Add date filter if since is provided
        if (since.HasValue)
        {
            var minDate = since.Value.ToString("yyyy/MM/dd");
            searchUrl += $"&mindate={minDate}&datetype=pdat";
        }

        var jsonContent = await GetStringAsync(searchUrl, cancellationToken);

        if (string.IsNullOrEmpty(jsonContent))
            return pmids;

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            if (root.TryGetProperty("esearchresult", out var esearchResult) &&
                esearchResult.TryGetProperty("idlist", out var idList))
            {
                foreach (var id in idList.EnumerateArray())
                {
                    var pmid = id.GetString();
                    if (!string.IsNullOrEmpty(pmid))
                    {
                        pmids.Add(pmid);
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Failed to parse PubMed search results");
        }

        return pmids;
    }

    private async Task<List<Article>> FetchArticleDetailsAsync(
        Source source,
        List<string> pmids,
        CancellationToken cancellationToken)
    {
        var articles = new List<Article>();

        if (pmids.Count == 0)
            return articles;

        var idsParam = string.Join(",", pmids);
        var fetchUrl = $"{EFetchBaseUrl}?db=pubmed&id={idsParam}&retmode=xml";

        var xmlContent = await GetStringAsync(fetchUrl, cancellationToken);

        if (string.IsNullOrEmpty(xmlContent))
            return articles;

        try
        {
            var doc = XDocument.Parse(xmlContent);
            var pubmedArticles = doc.Descendants("PubmedArticle");

            foreach (var pubmedArticle in pubmedArticles)
            {
                var medlineCitation = pubmedArticle.Element("MedlineCitation");
                var articleElement = medlineCitation?.Element("Article");

                if (articleElement == null)
                    continue;

                var pmid = medlineCitation?.Element("PMID")?.Value;
                var title = articleElement.Element("ArticleTitle")?.Value?.Trim();
                var abstractElement = articleElement.Element("Abstract");
                var abstractText = abstractElement?.Element("AbstractText")?.Value?.Trim();

                // Get publication date
                DateTime? publishedDate = null;
                var pubDate = articleElement.Element("Journal")?.Element("JournalIssue")?.Element("PubDate");
                if (pubDate != null)
                {
                    var year = pubDate.Element("Year")?.Value;
                    var month = pubDate.Element("Month")?.Value ?? "01";
                    var day = pubDate.Element("Day")?.Value ?? "01";

                    if (!string.IsNullOrEmpty(year))
                    {
                        // Convert month name to number if needed
                        if (!int.TryParse(month, out _))
                        {
                            month = ConvertMonthNameToNumber(month);
                        }

                        if (DateTime.TryParse($"{year}-{month}-{day}", out var date))
                        {
                            publishedDate = date;
                        }
                    }
                }

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(pmid))
                    continue;

                var article = new Article
                {
                    SourceId = source.Id,
                    KeywordId = source.KeywordId ?? 0,
                    Title = title,
                    Url = $"https://pubmed.ncbi.nlm.nih.gov/{pmid}/",
                    Summary = abstractText?.Length > 500 ? abstractText[..500] + "..." : abstractText,
                    NativeScore = null, // PubMed doesn't provide a direct score
                    PublishedAt = publishedDate,
                    CollectedAt = DateTime.UtcNow
                };

                articles.Add(article);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to parse PubMed article details");
        }

        return articles;
    }

    private static string ConvertMonthNameToNumber(string monthName)
    {
        return monthName.ToLowerInvariant() switch
        {
            "jan" or "january" => "01",
            "feb" or "february" => "02",
            "mar" or "march" => "03",
            "apr" or "april" => "04",
            "may" => "05",
            "jun" or "june" => "06",
            "jul" or "july" => "07",
            "aug" or "august" => "08",
            "sep" or "september" => "09",
            "oct" or "october" => "10",
            "nov" or "november" => "11",
            "dec" or "december" => "12",
            _ => "01"
        };
    }
}

using Microsoft.EntityFrameworkCore;
using QInfoRanker.Core.Entities;
using QInfoRanker.Core.Enums;

namespace QInfoRanker.Infrastructure.Data;

public static class DbSeeder
{
    /// <summary>
    /// データベースの初期化とシードデータの投入
    /// </summary>
    /// <param name="context">DBコンテキスト</param>
    /// <param name="seedSampleData">サンプルキーワードを作成するか（開発用、本番ではfalse推奨）</param>
    public static async Task SeedAsync(AppDbContext context, bool seedSampleData = false)
    {
        // データベースが存在しない場合のみ作成（既存データは保持）
        await context.Database.EnsureCreatedAsync();

        // テンプレートソースは常に作成（キーワード作成時のベースとして必要）
        if (!await context.Sources.AnyAsync(s => s.IsTemplate))
        {
            await SeedTemplateSourcesAsync(context);
        }

        // サンプルデータは明示的に有効化された場合のみ作成（本番環境では不要）
        if (seedSampleData && !await context.Keywords.AnyAsync())
        {
            await SeedInitialKeywordAsync(context);
        }
    }

    private static async Task SeedTemplateSourcesAsync(AppDbContext context)
    {
        var templateSources = new List<Source>
        {
            new()
            {
                Name = "Hatena Bookmark",
                Url = "https://b.hatena.ne.jp",
                SearchUrlTemplate = "https://b.hatena.ne.jp/search/text?q={keyword}&mode=rss",
                Type = SourceType.Rss,
                HasNativeScore = true,
                AuthorityWeight = 0.7,
                IsTemplate = true,
                IsActive = true,
                Language = Language.Japanese,
                Category = SourceCategory.Technology
            },
            new()
            {
                Name = "Qiita",
                Url = "https://qiita.com",
                SearchUrlTemplate = "https://qiita.com/api/v2/items?query={keyword}&per_page=50",
                Type = SourceType.Api,
                HasNativeScore = true,
                AuthorityWeight = 0.7,
                IsTemplate = true,
                IsActive = true,
                Language = Language.Japanese,
                Category = SourceCategory.Technology
            },
            new()
            {
                Name = "Zenn",
                Url = "https://zenn.dev",
                SearchUrlTemplate = "https://zenn.dev/api/search?q={keyword}&source=articles",
                Type = SourceType.Api,
                HasNativeScore = true,
                AuthorityWeight = 0.7,
                IsTemplate = true,
                IsActive = true,
                Language = Language.Japanese,
                Category = SourceCategory.Technology
            },
            new()
            {
                Name = "arXiv",
                Url = "https://arxiv.org",
                SearchUrlTemplate = "https://export.arxiv.org/api/query?search_query=all:{keyword}&sortBy=submittedDate&sortOrder=descending&max_results=50",
                Type = SourceType.Api,
                HasNativeScore = false,
                AuthorityWeight = 0.9,
                IsTemplate = true,
                IsActive = true,
                Language = Language.English,
                Category = SourceCategory.Academic
            },
            new()
            {
                Name = "Hacker News",
                Url = "https://news.ycombinator.com",
                SearchUrlTemplate = "https://hn.algolia.com/api/v1/search?query={keyword}&tags=story",
                Type = SourceType.Api,
                HasNativeScore = true,
                AuthorityWeight = 0.8,
                IsTemplate = true,
                IsActive = true,
                Language = Language.English,
                Category = SourceCategory.Technology
            },
            new()
            {
                Name = "Reddit",
                Url = "https://www.reddit.com",
                SearchUrlTemplate = "https://www.reddit.com/search.json?q={keyword}&sort=relevance&t=week",
                Type = SourceType.Api,
                HasNativeScore = true,
                AuthorityWeight = 0.6,
                IsTemplate = true,
                IsActive = true,
                Language = Language.English,
                Category = SourceCategory.Social
            },
            // 新規追加ソース: ニュース系
            new()
            {
                Name = "Google News JP",
                Url = "https://news.google.com",
                SearchUrlTemplate = "https://news.google.com/rss/search?q={keyword}&hl=ja&gl=JP&ceid=JP:ja",
                Type = SourceType.Rss,
                HasNativeScore = false,
                HasServerSideFiltering = true,
                AuthorityWeight = 0.7,
                IsTemplate = true,
                IsActive = true,
                Language = Language.Japanese,
                Category = SourceCategory.News
            },
            new()
            {
                Name = "Google News EN",
                Url = "https://news.google.com",
                SearchUrlTemplate = "https://news.google.com/rss/search?q={keyword}&hl=en&gl=US&ceid=US:en",
                Type = SourceType.Rss,
                HasNativeScore = false,
                HasServerSideFiltering = true,
                AuthorityWeight = 0.7,
                IsTemplate = true,
                IsActive = true,
                Language = Language.English,
                Category = SourceCategory.News
            },
            new()
            {
                Name = "Yahoo! News Japan",
                Url = "https://news.yahoo.co.jp",
                SearchUrlTemplate = "https://news.yahoo.co.jp/rss/topics/top-picks.xml",
                Type = SourceType.Rss,
                HasNativeScore = false,
                HasServerSideFiltering = false,
                AuthorityWeight = 0.7,
                IsTemplate = true,
                IsActive = true,
                Language = Language.Japanese,
                Category = SourceCategory.News
            },
            new()
            {
                Name = "BBC News",
                Url = "https://www.bbc.com/news",
                SearchUrlTemplate = "https://feeds.bbci.co.uk/news/rss.xml",
                Type = SourceType.Rss,
                HasNativeScore = false,
                HasServerSideFiltering = false,
                AuthorityWeight = 0.8,
                IsTemplate = true,
                IsActive = true,
                Language = Language.English,
                Category = SourceCategory.News
            },
            // 新規追加ソース: 学術系
            new()
            {
                Name = "PubMed",
                Url = "https://pubmed.ncbi.nlm.nih.gov",
                SearchUrlTemplate = "https://eutils.ncbi.nlm.nih.gov/entrez/eutils/esearch.fcgi?db=pubmed&term={keyword}&retmax=50&retmode=json",
                Type = SourceType.Api,
                HasNativeScore = false,
                HasServerSideFiltering = true,
                AuthorityWeight = 0.9,
                IsTemplate = true,
                IsActive = true,
                Language = Language.English,
                Category = SourceCategory.Medical
            },
            new()
            {
                Name = "Semantic Scholar",
                Url = "https://www.semanticscholar.org",
                SearchUrlTemplate = "https://api.semanticscholar.org/graph/v1/paper/search?query={keyword}&limit=50&fields=paperId,title,abstract,citationCount,year,url",
                Type = SourceType.Api,
                HasNativeScore = true,
                HasServerSideFiltering = true,
                AuthorityWeight = 0.85,
                IsTemplate = true,
                IsActive = true,
                Language = Language.English,
                Category = SourceCategory.Academic
            },
            // 新規追加ソース: エンタメ・趣味
            new()
            {
                Name = "Note.com",
                Url = "https://note.com",
                SearchUrlTemplate = "https://note.com/api/v2/searches?q={keyword}&size=50",
                Type = SourceType.Api,
                HasNativeScore = true,
                HasServerSideFiltering = true,
                AuthorityWeight = 0.6,
                IsTemplate = true,
                IsActive = true,
                Language = Language.Japanese,
                Category = SourceCategory.Entertainment
            }
        };

        context.Sources.AddRange(templateSources);
        await context.SaveChangesAsync();
    }

    private static async Task SeedInitialKeywordAsync(AppDbContext context)
    {
        var keyword = new Keyword
        {
            Term = "量子コンピュータ",
            Aliases = "quantum computer, quantum computing",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Keywords.Add(keyword);
        await context.SaveChangesAsync();

        // Create sources from templates for initial keyword
        var templateSources = await context.Sources.Where(s => s.IsTemplate).ToListAsync();
        foreach (var template in templateSources)
        {
            var source = new Source
            {
                KeywordId = keyword.Id,
                Name = template.Name,
                Url = template.Url,
                SearchUrlTemplate = template.SearchUrlTemplate,
                Type = template.Type,
                HasNativeScore = template.HasNativeScore,
                AuthorityWeight = template.AuthorityWeight,
                IsTemplate = false,
                IsActive = true,
                Language = template.Language,
                Category = template.Category
            };
            context.Sources.Add(source);
        }
        await context.SaveChangesAsync();
    }
}

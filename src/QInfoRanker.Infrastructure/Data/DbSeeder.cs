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

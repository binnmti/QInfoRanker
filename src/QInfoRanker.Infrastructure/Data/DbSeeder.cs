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
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    public static async Task SeedAsync(AppDbContext context, bool seedSampleData = false, CancellationToken cancellationToken = default)
    {
        // マイグレーションを適用（既存データは保持、新しいカラム等を追加）
        await context.Database.MigrateAsync(cancellationToken);

        // グローバルソースを初期化（全キーワード共通）
        if (!await context.Sources.AnyAsync(cancellationToken))
        {
            await SeedSourcesAsync(context, cancellationToken);
        }
        else
        {
            // 既存SourceのCategoryを更新（マイグレーションで追加されたカラム）
            await UpdateSourceCategoriesAsync(context, cancellationToken);
        }

        // サンプルデータは明示的に有効化された場合のみ作成（本番環境では不要）
        if (seedSampleData && !await context.Keywords.AnyAsync(cancellationToken))
        {
            await SeedInitialKeywordAsync(context, cancellationToken);
        }

        // 既存キーワードのSlugを自動生成（未設定のもののみ）
        await GenerateMissingSlugsAsync(context, cancellationToken);
    }

    /// <summary>
    /// グローバルソースのシード（全キーワード共通で使用）
    /// </summary>
    private static async Task SeedSourcesAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var sources = new List<Source>
        {
            new()
            {
                Name = "Hatena Bookmark",
                Url = "https://b.hatena.ne.jp",
                SearchUrlTemplate = "https://b.hatena.ne.jp/search/text?q={keyword}&mode=rss",
                Type = SourceType.Rss,
                HasNativeScore = true,
                AuthorityWeight = 0.7,
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
                IsActive = true,
                Language = Language.English,
                Category = SourceCategory.Social
            },
            // ニュース系
            new()
            {
                Name = "Google News JP",
                Url = "https://news.google.com",
                SearchUrlTemplate = "https://news.google.com/rss/search?q={keyword}&hl=ja&gl=JP&ceid=JP:ja",
                Type = SourceType.Rss,
                HasNativeScore = false,
                HasServerSideFiltering = true,
                AuthorityWeight = 0.7,
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
                IsActive = true,
                Language = Language.English,
                Category = SourceCategory.News
            },
            // 学術系
            new()
            {
                Name = "PubMed",
                Url = "https://pubmed.ncbi.nlm.nih.gov",
                SearchUrlTemplate = "https://eutils.ncbi.nlm.nih.gov/entrez/eutils/esearch.fcgi?db=pubmed&term={keyword}&retmax=50&retmode=json",
                Type = SourceType.Api,
                HasNativeScore = false,
                HasServerSideFiltering = true,
                AuthorityWeight = 0.9,
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
                IsActive = true,
                Language = Language.English,
                Category = SourceCategory.Academic
            },
            // エンタメ・趣味
            new()
            {
                Name = "Note.com",
                Url = "https://note.com",
                SearchUrlTemplate = "https://note.com/api/v2/searches?q={keyword}&size=50",
                Type = SourceType.Api,
                HasNativeScore = true,
                HasServerSideFiltering = true,
                AuthorityWeight = 0.6,
                IsActive = true,
                Language = Language.Japanese,
                Category = SourceCategory.Entertainment
            }
        };

        context.Sources.AddRange(sources);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedInitialKeywordAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var keyword = new Keyword
        {
            Term = "量子コンピュータ",
            Aliases = "quantum computer, quantum computing",
            Slug = "quantum-computer",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Keywords.Add(keyword);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 既存キーワードのうち、Slugが未設定のものに対して自動生成する
    /// </summary>
    /// <remarks>
    /// マイグレーションが適用されていない場合（Slugカラムが存在しない場合）は
    /// 例外をキャッチしてスキップする。次回マイグレーション適用後に実行される。
    /// </remarks>
    private static async Task GenerateMissingSlugsAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        try
        {
            var keywordsWithoutSlug = await context.Keywords
                .Where(k => k.Slug == null && k.Aliases != null)
                .ToListAsync(cancellationToken);

            if (!keywordsWithoutSlug.Any())
                return;

            var existingSlugsQuery = await context.Keywords
                .Where(k => k.Slug != null)
                .Select(k => k.Slug!)
                .ToListAsync(cancellationToken);
            var existingSlugs = existingSlugsQuery.ToHashSet();

            foreach (var keyword in keywordsWithoutSlug)
            {
                var slug = keyword.GenerateSlugFromAliases();
                if (string.IsNullOrEmpty(slug))
                    continue;

                // 重複チェック
                if (existingSlugs.Contains(slug))
                {
                    // 重複する場合はIDを付与
                    slug = $"{slug}-{keyword.Id}";
                }

                keyword.Slug = slug;
                existingSlugs.Add(slug);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // Slugカラムが存在しない場合（マイグレーション未適用）はスキップ
            // 次回マイグレーション適用後に実行される
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            // DB更新エラーもスキップ
        }
    }

    /// <summary>
    /// 既存SourceのCategoryを更新（マイグレーションで追加されたカラム用）
    /// </summary>
    private static async Task UpdateSourceCategoriesAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        // ソース名とカテゴリの対応辞書
        var categoryMap = new Dictionary<string, SourceCategory>
        {
            // Technology
            { "Hatena Bookmark", SourceCategory.Technology },
            { "Qiita", SourceCategory.Technology },
            { "Zenn", SourceCategory.Technology },
            { "Hacker News", SourceCategory.Technology },
            { "Dev.to", SourceCategory.Technology },
            { "Medium", SourceCategory.Technology },
            { "GitHub", SourceCategory.Technology },
            // Academic
            { "arXiv", SourceCategory.Academic },
            { "Semantic Scholar", SourceCategory.Academic },
            // Medical
            { "PubMed", SourceCategory.Medical },
            // News
            { "Google News JP", SourceCategory.News },
            { "Google News EN", SourceCategory.News },
            { "Yahoo! News Japan", SourceCategory.News },
            { "BBC News", SourceCategory.News },
            { "MarketBeat", SourceCategory.News },
            // Social
            { "Reddit", SourceCategory.Social },
            { "X (Twitter)", SourceCategory.Social },
            // Entertainment
            { "Note.com", SourceCategory.Entertainment },
        };

        var sources = await context.Sources.ToListAsync(cancellationToken);
        var updated = false;

        foreach (var source in sources)
        {
            if (categoryMap.TryGetValue(source.Name, out var category))
            {
                if (source.Category != category)
                {
                    source.Category = category;
                    updated = true;
                }
            }
        }

        if (updated)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}

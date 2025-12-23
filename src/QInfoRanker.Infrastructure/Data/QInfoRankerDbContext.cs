using Microsoft.EntityFrameworkCore;
using QInfoRanker.Core.Entities;

namespace QInfoRanker.Infrastructure.Data;

/// <summary>
/// Database context for QInfoRanker
/// </summary>
public class QInfoRankerDbContext : DbContext
{
    public QInfoRankerDbContext(DbContextOptions<QInfoRankerDbContext> options)
        : base(options)
    {
    }

    public DbSet<Keyword> Keywords { get; set; } = null!;
    public DbSet<Source> Sources { get; set; } = null!;
    public DbSet<Article> Articles { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Keyword configuration
        modelBuilder.Entity<Keyword>(entity =>
        {
            entity.HasKey(k => k.Id);
            entity.Property(k => k.Term)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(k => k.CreatedAt)
                .IsRequired();
            entity.HasIndex(k => k.Term);
        });

        // Source configuration
        modelBuilder.Entity<Source>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Name)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(s => s.Url)
                .IsRequired()
                .HasMaxLength(500);
            entity.Property(s => s.SearchUrlTemplate)
                .HasMaxLength(500);
            entity.Property(s => s.Type)
                .IsRequired()
                .HasConversion<string>();
            entity.Property(s => s.AuthorityWeight)
                .IsRequired();
            
            // Relationship with Keyword
            entity.HasOne(s => s.Keyword)
                .WithMany(k => k.Sources)
                .HasForeignKey(s => s.KeywordId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Article configuration
        modelBuilder.Entity<Article>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Title)
                .IsRequired()
                .HasMaxLength(500);
            entity.Property(a => a.Url)
                .IsRequired()
                .HasMaxLength(1000);
            entity.Property(a => a.Summary)
                .HasMaxLength(2000);
            entity.Property(a => a.PublishedAt)
                .IsRequired();
            entity.Property(a => a.CollectedAt)
                .IsRequired();
            
            // Relationship with Source
            entity.HasOne(a => a.Source)
                .WithMany(s => s.Articles)
                .HasForeignKey(a => a.SourceId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Indexes for common queries
            entity.HasIndex(a => a.Url);
            entity.HasIndex(a => a.PublishedAt);
            entity.HasIndex(a => a.FinalScore);
        });

        // Seed data for template sources
        SeedTemplateSources(modelBuilder);
    }

    private void SeedTemplateSources(ModelBuilder modelBuilder)
    {
        var templateSources = new[]
        {
            new Source
            {
                Id = 1,
                Name = "はてなブックマーク",
                Url = "https://b.hatena.ne.jp/",
                SearchUrlTemplate = "https://b.hatena.ne.jp/search/tag?q={keyword}",
                Type = SourceType.Scraping,
                HasNativeScore = true,
                AuthorityWeight = 0.7,
                IsActive = true,
                IsAutoDiscovered = false
            },
            new Source
            {
                Id = 2,
                Name = "Qiita",
                Url = "https://qiita.com/",
                SearchUrlTemplate = "https://qiita.com/tags/{keyword}",
                Type = SourceType.API,
                HasNativeScore = true,
                AuthorityWeight = 0.7,
                IsActive = true,
                IsAutoDiscovered = false
            },
            new Source
            {
                Id = 3,
                Name = "Zenn",
                Url = "https://zenn.dev/",
                SearchUrlTemplate = "https://zenn.dev/search?q={keyword}",
                Type = SourceType.Scraping,
                HasNativeScore = true,
                AuthorityWeight = 0.7,
                IsActive = true,
                IsAutoDiscovered = false
            },
            new Source
            {
                Id = 4,
                Name = "arXiv",
                Url = "https://export.arxiv.org/",
                SearchUrlTemplate = "https://export.arxiv.org/api/query?search_query={keyword}",
                Type = SourceType.API,
                HasNativeScore = false,
                AuthorityWeight = 0.9,
                IsActive = true,
                IsAutoDiscovered = false
            },
            new Source
            {
                Id = 5,
                Name = "Hacker News",
                Url = "https://news.ycombinator.com/",
                SearchUrlTemplate = "https://hn.algolia.com/api/v1/search?query={keyword}",
                Type = SourceType.API,
                HasNativeScore = true,
                AuthorityWeight = 0.8,
                IsActive = true,
                IsAutoDiscovered = false
            },
            new Source
            {
                Id = 6,
                Name = "Reddit",
                Url = "https://www.reddit.com/",
                SearchUrlTemplate = "https://www.reddit.com/search/?q={keyword}",
                Type = SourceType.Scraping,
                HasNativeScore = true,
                AuthorityWeight = 0.6,
                IsActive = true,
                IsAutoDiscovered = false
            }
        };

        modelBuilder.Entity<Source>().HasData(templateSources);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QInfoRanker.Core.Entities;

namespace QInfoRanker.Infrastructure.Data.Configurations;

public class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.Url)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(a => a.Summary)
            .HasMaxLength(2000);

        builder.HasIndex(a => a.Url);
        builder.HasIndex(a => a.FinalScore);
        builder.HasIndex(a => a.CollectedAt);
        builder.HasIndex(a => new { a.KeywordId, a.FinalScore });
    }
}

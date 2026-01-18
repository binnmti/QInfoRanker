using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QInfoRanker.Core.Entities;

namespace QInfoRanker.Infrastructure.Data.Configurations;

public class KeywordConfiguration : IEntityTypeConfiguration<Keyword>
{
    public void Configure(EntityTypeBuilder<Keyword> builder)
    {
        builder.HasKey(k => k.Id);

        builder.Property(k => k.Term)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(k => k.Slug)
            .HasMaxLength(200);

        builder.HasIndex(k => k.Term)
            .IsUnique();

        // Slug should be unique when not null (filter index)
        builder.HasIndex(k => k.Slug)
            .IsUnique()
            .HasFilter("[Slug] IS NOT NULL");

        builder.HasMany(k => k.Articles)
            .WithOne(a => a.Keyword)
            .HasForeignKey(a => a.KeywordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

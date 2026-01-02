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

        builder.HasIndex(k => k.Term)
            .IsUnique();

        builder.HasMany(k => k.Sources)
            .WithOne(s => s.Keyword)
            .HasForeignKey(s => s.KeywordId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(k => k.Articles)
            .WithOne(a => a.Keyword)
            .HasForeignKey(a => a.KeywordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QInfoRanker.Core.Entities;

namespace QInfoRanker.Infrastructure.Data.Configurations;

public class WeeklySummaryConfiguration : IEntityTypeConfiguration<WeeklySummary>
{
    public void Configure(EntityTypeBuilder<WeeklySummary> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(w => w.Content)
            .IsRequired();

        builder.HasIndex(w => new { w.KeywordId, w.WeekStart })
            .IsUnique();

        builder.HasOne(w => w.Keyword)
            .WithMany()
            .HasForeignKey(w => w.KeywordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QInfoRanker.Core.Entities;

namespace QInfoRanker.Infrastructure.Data.Configurations;

public class SourceConfiguration : IEntityTypeConfiguration<Source>
{
    public void Configure(EntityTypeBuilder<Source> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Url)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(s => s.SearchUrlTemplate)
            .HasMaxLength(500);

        builder.Property(s => s.AuthorityWeight)
            .HasDefaultValue(0.5);

        builder.HasMany(s => s.Articles)
            .WithOne(a => a.Source)
            .HasForeignKey(a => a.SourceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

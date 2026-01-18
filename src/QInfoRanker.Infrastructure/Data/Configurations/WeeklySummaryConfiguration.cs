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

        // 同一週に複数の要約を保持できるようにするため、ユニーク制約は設定しない
        // 代わりにKeywordIdとWeekStartで効率的に検索できるようインデックスを作成
        builder.HasIndex(w => new { w.KeywordId, w.WeekStart });

        // 生成日時による検索用インデックス
        builder.HasIndex(w => new { w.KeywordId, w.GeneratedAt });

        builder.HasOne(w => w.Keyword)
            .WithMany()
            .HasForeignKey(w => w.KeywordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

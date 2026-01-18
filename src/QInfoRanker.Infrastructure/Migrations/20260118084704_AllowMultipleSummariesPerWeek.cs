using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QInfoRanker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleSummariesPerWeek : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WeeklySummaries_KeywordId_WeekStart",
                table: "WeeklySummaries");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklySummaries_KeywordId_GeneratedAt",
                table: "WeeklySummaries",
                columns: new[] { "KeywordId", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WeeklySummaries_KeywordId_WeekStart",
                table: "WeeklySummaries",
                columns: new[] { "KeywordId", "WeekStart" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WeeklySummaries_KeywordId_GeneratedAt",
                table: "WeeklySummaries");

            migrationBuilder.DropIndex(
                name: "IX_WeeklySummaries_KeywordId_WeekStart",
                table: "WeeklySummaries");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklySummaries_KeywordId_WeekStart",
                table: "WeeklySummaries",
                columns: new[] { "KeywordId", "WeekStart" },
                unique: true);
        }
    }
}

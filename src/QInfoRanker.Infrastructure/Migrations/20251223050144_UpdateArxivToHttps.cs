using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QInfoRanker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateArxivToHttps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "SearchUrlTemplate", "Url" },
                values: new object[] { "https://export.arxiv.org/api/query?search_query={keyword}", "https://export.arxiv.org/" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Sources",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "SearchUrlTemplate", "Url" },
                values: new object[] { "http://export.arxiv.org/api/query?search_query={keyword}", "http://export.arxiv.org/" });
        }
    }
}

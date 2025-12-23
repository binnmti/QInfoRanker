using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace QInfoRanker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Keywords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Term = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Keywords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KeywordId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SearchUrlTemplate = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    HasNativeScore = table.Column<bool>(type: "INTEGER", nullable: false),
                    AuthorityWeight = table.Column<double>(type: "REAL", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsAutoDiscovered = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sources_Keywords_KeywordId",
                        column: x => x.KeywordId,
                        principalTable: "Keywords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CollectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NativeScore = table.Column<int>(type: "INTEGER", nullable: true),
                    LlmScore = table.Column<double>(type: "REAL", nullable: true),
                    FinalScore = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Articles_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Sources",
                columns: new[] { "Id", "AuthorityWeight", "HasNativeScore", "IsActive", "IsAutoDiscovered", "KeywordId", "Name", "SearchUrlTemplate", "Type", "Url" },
                values: new object[,]
                {
                    { 1, 0.69999999999999996, true, true, false, null, "はてなブックマーク", "https://b.hatena.ne.jp/search/tag?q={keyword}", "Scraping", "https://b.hatena.ne.jp/" },
                    { 2, 0.69999999999999996, true, true, false, null, "Qiita", "https://qiita.com/tags/{keyword}", "API", "https://qiita.com/" },
                    { 3, 0.69999999999999996, true, true, false, null, "Zenn", "https://zenn.dev/search?q={keyword}", "Scraping", "https://zenn.dev/" },
                    { 4, 0.90000000000000002, false, true, false, null, "arXiv", "http://export.arxiv.org/api/query?search_query={keyword}", "API", "http://export.arxiv.org/" },
                    { 5, 0.80000000000000004, true, true, false, null, "Hacker News", "https://hn.algolia.com/api/v1/search?query={keyword}", "API", "https://news.ycombinator.com/" },
                    { 6, 0.59999999999999998, true, true, false, null, "Reddit", "https://www.reddit.com/search/?q={keyword}", "Scraping", "https://www.reddit.com/" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Articles_FinalScore",
                table: "Articles",
                column: "FinalScore");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_PublishedAt",
                table: "Articles",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_SourceId",
                table: "Articles",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_Url",
                table: "Articles",
                column: "Url");

            migrationBuilder.CreateIndex(
                name: "IX_Keywords_Term",
                table: "Keywords",
                column: "Term");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_KeywordId",
                table: "Sources",
                column: "KeywordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Articles");

            migrationBuilder.DropTable(
                name: "Sources");

            migrationBuilder.DropTable(
                name: "Keywords");
        }
    }
}

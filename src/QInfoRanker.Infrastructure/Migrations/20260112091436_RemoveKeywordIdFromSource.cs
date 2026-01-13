using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QInfoRanker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveKeywordIdFromSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 重複ソースの削除（スキーマ変更前に実行）
            // 1. 重複ソース（KeywordId IS NOT NULL）を参照するArticleのSourceIdを
            //    対応するテンプレートソース（KeywordId IS NULL、同名）に更新
            //    対応するテンプレートが存在する場合のみ更新（NULLリスク防止）
            migrationBuilder.Sql(@"
                UPDATE Articles
                SET SourceId = (
                    SELECT t.Id
                    FROM Sources t
                    INNER JOIN Sources d ON t.Name = d.Name
                    WHERE d.Id = Articles.SourceId
                      AND d.KeywordId IS NOT NULL
                      AND t.KeywordId IS NULL
                )
                WHERE SourceId IN (
                    SELECT Id FROM Sources WHERE KeywordId IS NOT NULL
                )
                AND EXISTS (
                    SELECT 1 FROM Sources t
                    INNER JOIN Sources d ON t.Name = d.Name
                    WHERE d.Id = Articles.SourceId
                      AND d.KeywordId IS NOT NULL
                      AND t.KeywordId IS NULL
                )
            ");

            // 2. 重複ソース（KeywordId IS NOT NULL）を削除
            //    Articleから参照されていないソースのみ削除（FK違反防止）
            migrationBuilder.Sql(@"
                DELETE FROM Sources
                WHERE KeywordId IS NOT NULL
                  AND Id NOT IN (SELECT DISTINCT SourceId FROM Articles WHERE SourceId IS NOT NULL)
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_Sources_Keywords_KeywordId",
                table: "Sources");

            migrationBuilder.DropIndex(
                name: "IX_Sources_KeywordId",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "IsTemplate",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "KeywordId",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "RecommendationReason",
                table: "Sources");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTemplate",
                table: "Sources",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "KeywordId",
                table: "Sources",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecommendationReason",
                table: "Sources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sources_KeywordId",
                table: "Sources",
                column: "KeywordId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sources_Keywords_KeywordId",
                table: "Sources",
                column: "KeywordId",
                principalTable: "Keywords",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}

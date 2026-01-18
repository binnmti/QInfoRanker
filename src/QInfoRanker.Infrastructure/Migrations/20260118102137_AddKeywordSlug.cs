using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QInfoRanker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKeywordSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Keywords",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Keywords_Slug",
                table: "Keywords",
                column: "Slug",
                unique: true,
                filter: "[Slug] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Keywords_Slug",
                table: "Keywords");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Keywords");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QInfoRanker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklySummaryImageUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "WeeklySummaries",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "WeeklySummaries");
        }
    }
}

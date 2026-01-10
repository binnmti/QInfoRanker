using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QInfoRanker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecommendScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EnsembleRelevanceScore",
                table: "Articles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecommendScore",
                table: "Articles",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnsembleRelevanceScore",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "RecommendScore",
                table: "Articles");
        }
    }
}

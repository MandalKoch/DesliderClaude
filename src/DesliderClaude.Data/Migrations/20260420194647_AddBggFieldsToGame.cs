using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesliderClaude.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBggFieldsToGame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BggGameId",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Games",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailUrl",
                table: "Games",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_BggGameId",
                table: "Games",
                column: "BggGameId");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_BggGames_BggGameId",
                table: "Games",
                column: "BggGameId",
                principalTable: "BggGames",
                principalColumn: "BggGameId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_BggGames_BggGameId",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_Games_BggGameId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "BggGameId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "ThumbnailUrl",
                table: "Games");
        }
    }
}

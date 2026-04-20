using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesliderClaude.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBggImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BggGames",
                columns: table => new
                {
                    BggGameId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    MinPlayers = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxPlayers = table.Column<int>(type: "INTEGER", nullable: true),
                    MinPlayTimeMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxPlayTimeMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    RecommendedPlayerCountsJson = table.Column<string>(type: "TEXT", nullable: true),
                    LastFetchedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BggGames", x => x.BggGameId);
                });

            migrationBuilder.CreateTable(
                name: "BggImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceRef = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastRefreshedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BggImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BggImports_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BggImportItems",
                columns: table => new
                {
                    BggImportId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BggGameId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BggImportItems", x => new { x.BggImportId, x.BggGameId });
                    table.ForeignKey(
                        name: "FK_BggImportItems_BggGames_BggGameId",
                        column: x => x.BggGameId,
                        principalTable: "BggGames",
                        principalColumn: "BggGameId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BggImportItems_BggImports_BggImportId",
                        column: x => x.BggImportId,
                        principalTable: "BggImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BggImportItems_BggGameId",
                table: "BggImportItems",
                column: "BggGameId");

            migrationBuilder.CreateIndex(
                name: "IX_BggImports_UserId_SourceType_SourceRef",
                table: "BggImports",
                columns: new[] { "UserId", "SourceType", "SourceRef" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BggImportItems");

            migrationBuilder.DropTable(
                name: "BggGames");

            migrationBuilder.DropTable(
                name: "BggImports");
        }
    }
}

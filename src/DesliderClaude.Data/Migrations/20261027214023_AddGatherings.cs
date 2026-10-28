using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesliderClaude.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGatherings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GatheringGameId",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GatheringId",
                table: "GameNights",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Gatherings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ShareCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gatherings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Gatherings_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GatheringMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GatheringId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Token = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatheringMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GatheringMembers_Gatherings_GatheringId",
                        column: x => x.GatheringId,
                        principalTable: "Gatherings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GatheringMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GatheringGames",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GatheringId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    BggGameId = table.Column<int>(type: "INTEGER", nullable: true),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    SuggestedByMemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    BringerMemberId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatheringGames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GatheringGames_GatheringMembers_BringerMemberId",
                        column: x => x.BringerMemberId,
                        principalTable: "GatheringMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GatheringGames_GatheringMembers_SuggestedByMemberId",
                        column: x => x.SuggestedByMemberId,
                        principalTable: "GatheringMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GatheringGames_Gatherings_GatheringId",
                        column: x => x.GatheringId,
                        principalTable: "Gatherings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GatheringInterests",
                columns: table => new
                {
                    GatheringGameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Interested = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatheringInterests", x => new { x.GatheringGameId, x.MemberId });
                    table.ForeignKey(
                        name: "FK_GatheringInterests_GatheringGames_GatheringGameId",
                        column: x => x.GatheringGameId,
                        principalTable: "GatheringGames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GatheringInterests_GatheringMembers_MemberId",
                        column: x => x.MemberId,
                        principalTable: "GatheringMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GatheringPlayedGames",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GatheringId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GatheringGameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatheringPlayedGames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GatheringPlayedGames_GatheringGames_GatheringGameId",
                        column: x => x.GatheringGameId,
                        principalTable: "GatheringGames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GatheringPlayedGames_Gatherings_GatheringId",
                        column: x => x.GatheringId,
                        principalTable: "Gatherings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Games_GatheringGameId",
                table: "Games",
                column: "GatheringGameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameNights_GatheringId",
                table: "GameNights",
                column: "GatheringId");

            migrationBuilder.CreateIndex(
                name: "IX_GatheringGames_BringerMemberId",
                table: "GatheringGames",
                column: "BringerMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_GatheringGames_GatheringId",
                table: "GatheringGames",
                column: "GatheringId");

            migrationBuilder.CreateIndex(
                name: "IX_GatheringGames_SuggestedByMemberId",
                table: "GatheringGames",
                column: "SuggestedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_GatheringInterests_MemberId",
                table: "GatheringInterests",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_GatheringMembers_GatheringId_Token",
                table: "GatheringMembers",
                columns: new[] { "GatheringId", "Token" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GatheringMembers_GatheringId_UserId",
                table: "GatheringMembers",
                columns: new[] { "GatheringId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GatheringMembers_UserId",
                table: "GatheringMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GatheringPlayedGames_GatheringGameId",
                table: "GatheringPlayedGames",
                column: "GatheringGameId");

            migrationBuilder.CreateIndex(
                name: "IX_GatheringPlayedGames_GatheringId",
                table: "GatheringPlayedGames",
                column: "GatheringId");

            migrationBuilder.CreateIndex(
                name: "IX_Gatherings_CreatedByUserId",
                table: "Gatherings",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Gatherings_ShareCode",
                table: "Gatherings",
                column: "ShareCode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_GameNights_Gatherings_GatheringId",
                table: "GameNights",
                column: "GatheringId",
                principalTable: "Gatherings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_GatheringGames_GatheringGameId",
                table: "Games",
                column: "GatheringGameId",
                principalTable: "GatheringGames",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameNights_Gatherings_GatheringId",
                table: "GameNights");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_GatheringGames_GatheringGameId",
                table: "Games");

            migrationBuilder.DropTable(
                name: "GatheringInterests");

            migrationBuilder.DropTable(
                name: "GatheringPlayedGames");

            migrationBuilder.DropTable(
                name: "GatheringGames");

            migrationBuilder.DropTable(
                name: "GatheringMembers");

            migrationBuilder.DropTable(
                name: "Gatherings");

            migrationBuilder.DropIndex(
                name: "IX_Games_GatheringGameId",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_GameNights_GatheringId",
                table: "GameNights");

            migrationBuilder.DropColumn(
                name: "GatheringGameId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "GatheringId",
                table: "GameNights");
        }
    }
}

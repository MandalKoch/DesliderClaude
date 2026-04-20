using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesliderClaude.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkNightsToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Voters_GameNightId",
                table: "Voters");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Voters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "GameNights",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Voters_GameNightId_UserId",
                table: "Voters",
                columns: new[] { "GameNightId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Voters_UserId",
                table: "Voters",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameNights_CreatedByUserId",
                table: "GameNights",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_GameNights_Users_CreatedByUserId",
                table: "GameNights",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Voters_Users_UserId",
                table: "Voters",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameNights_Users_CreatedByUserId",
                table: "GameNights");

            migrationBuilder.DropForeignKey(
                name: "FK_Voters_Users_UserId",
                table: "Voters");

            migrationBuilder.DropIndex(
                name: "IX_Voters_GameNightId_UserId",
                table: "Voters");

            migrationBuilder.DropIndex(
                name: "IX_Voters_UserId",
                table: "Voters");

            migrationBuilder.DropIndex(
                name: "IX_GameNights_CreatedByUserId",
                table: "GameNights");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Voters");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "GameNights");

            migrationBuilder.CreateIndex(
                name: "IX_Voters_GameNightId",
                table: "Voters",
                column: "GameNightId");
        }
    }
}

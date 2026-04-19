using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesliderClaude.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VisitorId",
                table: "Voters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Visitors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Token = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Visitors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Voters_GameNightId_VisitorId",
                table: "Voters",
                columns: new[] { "GameNightId", "VisitorId" });

            migrationBuilder.CreateIndex(
                name: "IX_Voters_VisitorId",
                table: "Voters",
                column: "VisitorId");

            migrationBuilder.CreateIndex(
                name: "IX_Visitors_Token",
                table: "Visitors",
                column: "Token",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Voters_Visitors_VisitorId",
                table: "Voters",
                column: "VisitorId",
                principalTable: "Visitors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Voters_Visitors_VisitorId",
                table: "Voters");

            migrationBuilder.DropTable(
                name: "Visitors");

            migrationBuilder.DropIndex(
                name: "IX_Voters_GameNightId_VisitorId",
                table: "Voters");

            migrationBuilder.DropIndex(
                name: "IX_Voters_VisitorId",
                table: "Voters");

            migrationBuilder.DropColumn(
                name: "VisitorId",
                table: "Voters");
        }
    }
}

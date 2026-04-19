using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesliderClaude.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropHostToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HostToken",
                table: "GameNights");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HostToken",
                table: "GameNights",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");
        }
    }
}

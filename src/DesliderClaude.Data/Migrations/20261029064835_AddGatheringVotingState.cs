using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesliderClaude.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGatheringVotingState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVotingClosed",
                table: "Gatherings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsVotingClosed",
                table: "Gatherings");
        }
    }
}

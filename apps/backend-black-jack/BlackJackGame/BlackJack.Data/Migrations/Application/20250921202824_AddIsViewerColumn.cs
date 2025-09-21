using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlackJack.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddIsViewerColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsViewer",
                table: "RoomPlayers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsViewer",
                table: "RoomPlayers");
        }
    }
}

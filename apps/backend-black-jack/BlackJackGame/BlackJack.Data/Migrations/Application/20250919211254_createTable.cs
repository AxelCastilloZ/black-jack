using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlackJack.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class createTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RoomPlayers_PlayerId",
                table: "RoomPlayers",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_GameRooms_HostPlayerId",
                table: "GameRooms",
                column: "HostPlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RoomPlayers_PlayerId",
                table: "RoomPlayers");

            migrationBuilder.DropIndex(
                name: "IX_GameRooms_HostPlayerId",
                table: "GameRooms");
        }
    }
}

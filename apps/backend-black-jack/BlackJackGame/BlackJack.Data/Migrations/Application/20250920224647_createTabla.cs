using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlackJack.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class createTabla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RoomPlayers_GameRoomId",
                table: "RoomPlayers");

            migrationBuilder.AlterColumn<Guid>(
                name: "GameRoomId",
                table: "RoomPlayers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeatPosition",
                table: "RoomPlayers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomPlayers_GameRoomId_SeatPosition",
                table: "RoomPlayers",
                columns: new[] { "GameRoomId", "SeatPosition" });

            migrationBuilder.CreateIndex(
                name: "IX_RoomPlayers_SeatPosition",
                table: "RoomPlayers",
                column: "SeatPosition");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RoomPlayers_GameRoomId_SeatPosition",
                table: "RoomPlayers");

            migrationBuilder.DropIndex(
                name: "IX_RoomPlayers_SeatPosition",
                table: "RoomPlayers");

            migrationBuilder.DropColumn(
                name: "SeatPosition",
                table: "RoomPlayers");

            migrationBuilder.AlterColumn<Guid>(
                name: "GameRoomId",
                table: "RoomPlayers",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_RoomPlayers_GameRoomId",
                table: "RoomPlayers",
                column: "GameRoomId");
        }
    }
}

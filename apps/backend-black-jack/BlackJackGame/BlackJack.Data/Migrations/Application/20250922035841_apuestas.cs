using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlackJack.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class apuestas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GameRoomId1",
                table: "RoomPlayers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinBetPerRound",
                table: "GameRooms",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_RoomPlayers_GameRoomId1",
                table: "RoomPlayers",
                column: "GameRoomId1");

            migrationBuilder.CreateIndex(
                name: "IX_GameRooms_MinBetPerRound",
                table: "GameRooms",
                column: "MinBetPerRound");

            migrationBuilder.AddForeignKey(
                name: "FK_RoomPlayers_GameRooms_GameRoomId1",
                table: "RoomPlayers",
                column: "GameRoomId1",
                principalTable: "GameRooms",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoomPlayers_GameRooms_GameRoomId1",
                table: "RoomPlayers");

            migrationBuilder.DropIndex(
                name: "IX_RoomPlayers_GameRoomId1",
                table: "RoomPlayers");

            migrationBuilder.DropIndex(
                name: "IX_GameRooms_MinBetPerRound",
                table: "GameRooms");

            migrationBuilder.DropColumn(
                name: "GameRoomId1",
                table: "RoomPlayers");

            migrationBuilder.DropColumn(
                name: "MinBetPerRound",
                table: "GameRooms");
        }
    }
}

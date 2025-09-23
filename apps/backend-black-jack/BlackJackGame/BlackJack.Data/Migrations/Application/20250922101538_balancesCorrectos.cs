using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlackJack.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class balancesCorrectos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PlayerEntityId",
                schema: "dbo",
                table: "RoomPlayers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_RoomPlayers_PlayerEntityId",
                schema: "dbo",
                table: "RoomPlayers",
                column: "PlayerEntityId");

            migrationBuilder.AddForeignKey(
                name: "FK_RoomPlayers_Players_PlayerEntityId",
                schema: "dbo",
                table: "RoomPlayers",
                column: "PlayerEntityId",
                principalSchema: "dbo",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoomPlayers_Players_PlayerEntityId",
                schema: "dbo",
                table: "RoomPlayers");

            migrationBuilder.DropIndex(
                name: "IX_RoomPlayers_PlayerEntityId",
                schema: "dbo",
                table: "RoomPlayers");

            migrationBuilder.DropColumn(
                name: "PlayerEntityId",
                schema: "dbo",
                table: "RoomPlayers");
        }
    }
}

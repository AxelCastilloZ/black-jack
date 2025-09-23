using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlackJack.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class fixapuestasss : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Spectators_BlackjackTables_TableId",
                table: "Spectators");

            migrationBuilder.DropIndex(
                name: "IX_Spectators_TableId",
                table: "Spectators");

            migrationBuilder.DropColumn(
                name: "TableId",
                table: "Spectators");

            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.RenameTable(
                name: "UserProfiles",
                newName: "UserProfiles",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "Spectators",
                newName: "Spectators",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "Seats",
                newName: "Seats",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "RoomPlayers",
                newName: "RoomPlayers",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "Players",
                newName: "Players",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "Hands",
                newName: "Hands",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "GameRooms",
                newName: "GameRooms",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "BlackjackTables",
                newName: "BlackjackTables",
                newSchema: "dbo");

            migrationBuilder.AlterColumn<Guid>(
                name: "GameRoomId",
                schema: "dbo",
                table: "Spectators",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Spectators_PlayerId",
                schema: "dbo",
                table: "Spectators",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Spectators_PlayerId",
                schema: "dbo",
                table: "Spectators");

            migrationBuilder.RenameTable(
                name: "UserProfiles",
                schema: "dbo",
                newName: "UserProfiles");

            migrationBuilder.RenameTable(
                name: "Spectators",
                schema: "dbo",
                newName: "Spectators");

            migrationBuilder.RenameTable(
                name: "Seats",
                schema: "dbo",
                newName: "Seats");

            migrationBuilder.RenameTable(
                name: "RoomPlayers",
                schema: "dbo",
                newName: "RoomPlayers");

            migrationBuilder.RenameTable(
                name: "Players",
                schema: "dbo",
                newName: "Players");

            migrationBuilder.RenameTable(
                name: "Hands",
                schema: "dbo",
                newName: "Hands");

            migrationBuilder.RenameTable(
                name: "GameRooms",
                schema: "dbo",
                newName: "GameRooms");

            migrationBuilder.RenameTable(
                name: "BlackjackTables",
                schema: "dbo",
                newName: "BlackjackTables");

            migrationBuilder.AlterColumn<Guid>(
                name: "GameRoomId",
                table: "Spectators",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "TableId",
                table: "Spectators",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Spectators_TableId",
                table: "Spectators",
                column: "TableId");

            migrationBuilder.AddForeignKey(
                name: "FK_Spectators_BlackjackTables_TableId",
                table: "Spectators",
                column: "TableId",
                principalTable: "BlackjackTables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

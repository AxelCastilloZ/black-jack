using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlackJack.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class baseSolida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Players_Hands");

            migrationBuilder.DropIndex(
                name: "IX_Players_PlayerId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Spectators");

            migrationBuilder.DropColumn(
                name: "IsOccupied",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "CurrentBetAmount",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "DealerCards",
                table: "BlackjackTables");

            migrationBuilder.DropColumn(
                name: "DealerHandIsSoft",
                table: "BlackjackTables");

            migrationBuilder.DropColumn(
                name: "DealerHandStatus",
                table: "BlackjackTables");

            migrationBuilder.DropColumn(
                name: "DealerHandValue",
                table: "BlackjackTables");

            migrationBuilder.DropColumn(
                name: "DealerHand_CreatedAt",
                table: "BlackjackTables");

            migrationBuilder.DropColumn(
                name: "DealerHand_Id",
                table: "BlackjackTables");

            migrationBuilder.DropColumn(
                name: "DealerHand_UpdatedAt",
                table: "BlackjackTables");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Spectators",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "GameRoomId",
                table: "Spectators",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Players",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "HandIds",
                table: "Players",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DealerHandId",
                table: "BlackjackTables",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GameRooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HostPlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MaxPlayers = table.Column<int>(type: "int", nullable: false),
                    CurrentPlayerIndex = table.Column<int>(type: "int", nullable: false),
                    BlackjackTableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameRooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Hands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cards = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoomPlayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    IsReady = table.Column<bool>(type: "bit", nullable: false),
                    HasPlayedTurn = table.Column<bool>(type: "bit", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastActionAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GameRoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomPlayers_GameRooms_GameRoomId",
                        column: x => x.GameRoomId,
                        principalTable: "GameRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Spectators_GameRoomId",
                table: "Spectators",
                column: "GameRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Spectators_JoinedAt",
                table: "Spectators",
                column: "JoinedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Spectators_Name",
                table: "Spectators",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Seats_Position",
                table: "Seats",
                column: "Position");

            migrationBuilder.CreateIndex(
                name: "IX_Players_Name",
                table: "Players",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_GameRooms_Name",
                table: "GameRooms",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_GameRooms_RoomCode",
                table: "GameRooms",
                column: "RoomCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameRooms_Status",
                table: "GameRooms",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Hands_CreatedAt",
                table: "Hands",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Hands_Status",
                table: "Hands",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RoomPlayers_GameRoomId",
                table: "RoomPlayers",
                column: "GameRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomPlayers_Position",
                table: "RoomPlayers",
                column: "Position");

            migrationBuilder.AddForeignKey(
                name: "FK_Spectators_GameRooms_GameRoomId",
                table: "Spectators",
                column: "GameRoomId",
                principalTable: "GameRooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Spectators_GameRooms_GameRoomId",
                table: "Spectators");

            migrationBuilder.DropTable(
                name: "Hands");

            migrationBuilder.DropTable(
                name: "RoomPlayers");

            migrationBuilder.DropTable(
                name: "GameRooms");

            migrationBuilder.DropIndex(
                name: "IX_Spectators_GameRoomId",
                table: "Spectators");

            migrationBuilder.DropIndex(
                name: "IX_Spectators_JoinedAt",
                table: "Spectators");

            migrationBuilder.DropIndex(
                name: "IX_Spectators_Name",
                table: "Spectators");

            migrationBuilder.DropIndex(
                name: "IX_Seats_Position",
                table: "Seats");

            migrationBuilder.DropIndex(
                name: "IX_Players_Name",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "GameRoomId",
                table: "Spectators");

            migrationBuilder.DropColumn(
                name: "HandIds",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "DealerHandId",
                table: "BlackjackTables");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Spectators",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Spectators",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOccupied",
                table: "Seats",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Players",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentBetAmount",
                table: "Players",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DealerCards",
                table: "BlackjackTables",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "DealerHandIsSoft",
                table: "BlackjackTables",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DealerHandStatus",
                table: "BlackjackTables",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DealerHandValue",
                table: "BlackjackTables",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DealerHand_CreatedAt",
                table: "BlackjackTables",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "DealerHand_Id",
                table: "BlackjackTables",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DealerHand_UpdatedAt",
                table: "BlackjackTables",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "Players_Hands",
                columns: table => new
                {
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cards = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsSoft = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Value = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players_Hands", x => new { x.PlayerId, x.Id });
                    table.ForeignKey(
                        name: "FK_Players_Hands_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Players_PlayerId",
                table: "Players",
                column: "PlayerId");
        }
    }
}

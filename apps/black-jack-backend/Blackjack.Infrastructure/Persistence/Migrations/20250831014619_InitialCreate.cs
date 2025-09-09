using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blackjack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActionLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    RoundId = table.Column<int>(type: "int", nullable: true),
                    PlayerId = table.Column<int>(type: "int", nullable: true),
                    ActionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadJSON = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Hands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoundId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    CardsJSON = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BetAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsStanding = table.Column<bool>(type: "bit", nullable: false),
                    IsBusted = table.Column<bool>(type: "bit", nullable: false),
                    IsDoubled = table.Column<bool>(type: "bit", nullable: false),
                    IsBlackjack = table.Column<bool>(type: "bit", nullable: false),
                    Result = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Payout = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    Nickname = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SeatIndex = table.Column<int>(type: "int", nullable: false),
                    BalanceShadow = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SocketId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    NumDecks = table.Column<int>(type: "int", nullable: false),
                    MinBet = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxBet = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PayoutBlackjack = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SurrenderEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rounds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    Phase = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ShoePosition = table.Column<int>(type: "int", nullable: false),
                    DealerHandJSON = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rounds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionLogs");

            migrationBuilder.DropTable(
                name: "Hands");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "Rounds");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}

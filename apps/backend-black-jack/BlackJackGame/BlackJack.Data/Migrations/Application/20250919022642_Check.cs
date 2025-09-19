using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlackJack.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class Check : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Hands");

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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cards = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Value = table.Column<int>(type: "int", nullable: false),
                    IsSoft = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_BlackjackTables_Name",
                table: "BlackjackTables",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_BlackjackTables_Status",
                table: "BlackjackTables",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Players_Hands");

            migrationBuilder.DropIndex(
                name: "IX_Players_PlayerId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_BlackjackTables_Name",
                table: "BlackjackTables");

            migrationBuilder.DropIndex(
                name: "IX_BlackjackTables_Status",
                table: "BlackjackTables");

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

            migrationBuilder.CreateTable(
                name: "Hands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cards = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsSoft = table.Column<bool>(type: "bit", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Value = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hands_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hands_PlayerId",
                table: "Hands",
                column: "PlayerId");
        }
    }
}

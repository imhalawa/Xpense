using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xpense.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAtomicTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Transfers",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SourceAccountId = table.Column<int>(type: "int", nullable: false),
                    DestinationAccountId = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transfers_Accounts_DestinationAccountId",
                        column: x => x.DestinationAccountId,
                        principalSchema: "Xpense",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transfers_Accounts_SourceAccountId",
                        column: x => x.SourceAccountId,
                        principalSchema: "Xpense",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransferLegs",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransferId = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferLegs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransferLegs_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "Xpense",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferLegs_Transfers_TransferId",
                        column: x => x.TransferId,
                        principalSchema: "Xpense",
                        principalTable: "Transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransferLegs_AccountId",
                schema: "Xpense",
                table: "TransferLegs",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferLegs_TransferId_Direction",
                schema: "Xpense",
                table: "TransferLegs",
                columns: new[] { "TransferId", "Direction" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_DestinationAccountId",
                schema: "Xpense",
                table: "Transfers",
                column: "DestinationAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_SourceAccountId",
                schema: "Xpense",
                table: "Transfers",
                column: "SourceAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransferLegs",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "Transfers",
                schema: "Xpense");
        }
    }
}

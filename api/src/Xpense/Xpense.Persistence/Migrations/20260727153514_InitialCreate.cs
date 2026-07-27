using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Xpense.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Xpense");

            migrationBuilder.CreateTable(
                name: "Accounts",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccountNumber = table.Column<string>(type: "character(10)", fixedLength: true, maxLength: 10, nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsDefaultAccount = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Merchants",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Merchants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Priorities",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Priorities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BgColorHex = table.Column<string>(type: "character(6)", fixedLength: true, maxLength: 6, nullable: true),
                    FgColorHex = table.Column<string>(type: "character(6)", fixedLength: true, maxLength: 6, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transfers",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SourceAccountId = table.Column<int>(type: "integer", nullable: false),
                    DestinationAccountId = table.Column<int>(type: "integer", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
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
                name: "Categories",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PriorityId = table.Column<int>(type: "integer", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categories_Priorities_PriorityId",
                        column: x => x.PriorityId,
                        principalSchema: "Xpense",
                        principalTable: "Priorities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransferLegs",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransferId = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "Transactions",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    TransactionType = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<int>(type: "integer", nullable: true),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    MerchantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "Xpense",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "Xpense",
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Transactions_Merchants_MerchantId",
                        column: x => x.MerchantId,
                        principalSchema: "Xpense",
                        principalTable: "Merchants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransactionTags",
                schema: "Xpense",
                columns: table => new
                {
                    TransactionId = table.Column<int>(type: "integer", nullable: false),
                    TagId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionTags", x => new { x.TransactionId, x.TagId });
                    table.ForeignKey(
                        name: "FK_TransactionTags_Tags_TagId",
                        column: x => x.TagId,
                        principalSchema: "Xpense",
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransactionTags_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalSchema: "Xpense",
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountNumber",
                schema: "Xpense",
                table: "Accounts",
                column: "AccountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Label",
                schema: "Xpense",
                table: "Categories",
                column: "Label",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_PriorityId",
                schema: "Xpense",
                table: "Categories",
                column: "PriorityId");

            migrationBuilder.CreateIndex(
                name: "IX_Merchants_Label",
                schema: "Xpense",
                table: "Merchants",
                column: "Label",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Priorities_Label",
                schema: "Xpense",
                table: "Priorities",
                column: "Label",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Label",
                schema: "Xpense",
                table: "Tags",
                column: "Label",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AccountId",
                schema: "Xpense",
                table: "Transactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CategoryId",
                schema: "Xpense",
                table: "Transactions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_MerchantId",
                schema: "Xpense",
                table: "Transactions",
                column: "MerchantId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionTags_TagId",
                schema: "Xpense",
                table: "TransactionTags",
                column: "TagId");

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
                name: "TransactionTags",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "TransferLegs",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "Tags",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "Transactions",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "Transfers",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "Categories",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "Merchants",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "Accounts",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "Priorities",
                schema: "Xpense");
        }
    }
}

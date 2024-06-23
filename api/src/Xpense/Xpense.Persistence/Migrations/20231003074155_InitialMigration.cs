using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xpense.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Xpense");

            migrationBuilder.EnsureSchema(
                name: "Currency");

            migrationBuilder.CreateTable(
                name: "CategoryLevels",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Weight = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryLevels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Currencies",
                schema: "Currency",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsoCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tag",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BgColorHex = table.Column<string>(type: "nchar(6)", fixedLength: true, maxLength: 6, nullable: true),
                    FgColorHex = table.Column<string>(type: "nchar(6)", fixedLength: true, maxLength: 6, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tag", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CategoryLevelId = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categories_CategoryLevels_CategoryLevelId",
                        column: x => x.CategoryLevelId,
                        principalSchema: "Xpense",
                        principalTable: "CategoryLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Accounts",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Number = table.Column<string>(type: "nchar(10)", fixedLength: true, maxLength: 10, nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsTransactionLocked = table.Column<bool>(type: "bit", nullable: false),
                    IsDefaultAccount = table.Column<bool>(type: "bit", nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Accounts_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalSchema: "Currency",
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Country",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DialCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Country", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Country_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalSchema: "Currency",
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CurrencyExchangeRate",
                schema: "Currency",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PrincipalCurrencyId = table.Column<int>(type: "int", nullable: false),
                    ForeignCurrencyId = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrencyExchangeRate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurrencyExchangeRate_Currencies_ForeignCurrencyId",
                        column: x => x.ForeignCurrencyId,
                        principalSchema: "Currency",
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CurrencyExchangeRate_Currencies_PrincipalCurrencyId",
                        column: x => x.PrincipalCurrencyId,
                        principalSchema: "Currency",
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountTag",
                schema: "Xpense",
                columns: table => new
                {
                    AccountsId = table.Column<int>(type: "int", nullable: false),
                    TagsId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTag", x => new { x.AccountsId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_AccountTag_Accounts_AccountsId",
                        column: x => x.AccountsId,
                        principalSchema: "Xpense",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountTag_Tag_TagsId",
                        column: x => x.TagsId,
                        principalSchema: "Xpense",
                        principalTable: "Tag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CurrencyExchangeRateAudits",
                schema: "Currency",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PrincipalCurrencyId = table.Column<int>(type: "int", nullable: false),
                    ForeignCurrencyId = table.Column<int>(type: "int", nullable: false),
                    ExchangeRateId = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrencyExchangeRateAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurrencyExchangeRateAudits_Currencies_ForeignCurrencyId",
                        column: x => x.ForeignCurrencyId,
                        principalSchema: "Currency",
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CurrencyExchangeRateAudits_Currencies_PrincipalCurrencyId",
                        column: x => x.PrincipalCurrencyId,
                        principalSchema: "Currency",
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CurrencyExchangeRateAudits_CurrencyExchangeRate_ExchangeRateId",
                        column: x => x.ExchangeRateId,
                        principalSchema: "Currency",
                        principalTable: "CurrencyExchangeRate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransactionType = table.Column<int>(type: "int", nullable: false),
                    FromAccountId = table.Column<int>(type: "int", nullable: false),
                    ToAccountId = table.Column<int>(type: "int", nullable: false),
                    CurrencyExchangeRateAuditId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_Accounts_FromAccountId",
                        column: x => x.FromAccountId,
                        principalSchema: "Xpense",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_Accounts_ToAccountId",
                        column: x => x.ToAccountId,
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
                        name: "FK_Transactions_CurrencyExchangeRateAudits_CurrencyExchangeRateAuditId",
                        column: x => x.CurrencyExchangeRateAuditId,
                        principalSchema: "Currency",
                        principalTable: "CurrencyExchangeRateAudits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TagTransaction",
                schema: "Xpense",
                columns: table => new
                {
                    TagsId = table.Column<int>(type: "int", nullable: false),
                    TransactionsId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagTransaction", x => new { x.TagsId, x.TransactionsId });
                    table.ForeignKey(
                        name: "FK_TagTransaction_Tag_TagsId",
                        column: x => x.TagsId,
                        principalSchema: "Xpense",
                        principalTable: "Tag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TagTransaction_Transactions_TransactionsId",
                        column: x => x.TransactionsId,
                        principalSchema: "Xpense",
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_CurrencyId",
                schema: "Xpense",
                table: "Accounts",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Number",
                schema: "Xpense",
                table: "Accounts",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountTag_TagsId",
                schema: "Xpense",
                table: "AccountTag",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CategoryLevelId",
                schema: "Xpense",
                table: "Categories",
                column: "CategoryLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryLevels_Name",
                schema: "Xpense",
                table: "CategoryLevels",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Country_CurrencyId",
                schema: "Xpense",
                table: "Country",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Country_Name",
                schema: "Xpense",
                table: "Country",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_IsoCode",
                schema: "Currency",
                table: "Currencies",
                column: "IsoCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyExchangeRate_ForeignCurrencyId",
                schema: "Currency",
                table: "CurrencyExchangeRate",
                column: "ForeignCurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyExchangeRate_PrincipalCurrencyId",
                schema: "Currency",
                table: "CurrencyExchangeRate",
                column: "PrincipalCurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyExchangeRateAudits_ExchangeRateId",
                schema: "Currency",
                table: "CurrencyExchangeRateAudits",
                column: "ExchangeRateId");

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyExchangeRateAudits_ForeignCurrencyId",
                schema: "Currency",
                table: "CurrencyExchangeRateAudits",
                column: "ForeignCurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyExchangeRateAudits_PrincipalCurrencyId",
                schema: "Currency",
                table: "CurrencyExchangeRateAudits",
                column: "PrincipalCurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Tag_Name",
                schema: "Xpense",
                table: "Tag",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TagTransaction_TransactionsId",
                schema: "Xpense",
                table: "TagTransaction",
                column: "TransactionsId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CategoryId",
                schema: "Xpense",
                table: "Transactions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CurrencyExchangeRateAuditId",
                schema: "Xpense",
                table: "Transactions",
                column: "CurrencyExchangeRateAuditId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_FromAccountId",
                schema: "Xpense",
                table: "Transactions",
                column: "FromAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ToAccountId",
                schema: "Xpense",
                table: "Transactions",
                column: "ToAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountTag",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "Country",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "TagTransaction",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "Tag",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "Transactions",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "Accounts",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "Categories",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "CurrencyExchangeRateAudits",
                schema: "Currency");

            migrationBuilder.DropTable(
                name: "CategoryLevels",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "CurrencyExchangeRate",
                schema: "Currency");

            migrationBuilder.DropTable(
                name: "Currencies",
                schema: "Currency");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xpense.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class Remove_UnecessaryEntities_ForTrackingExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Currencies_CurrencyId",
                schema: "Xpense",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_CategoryLevels_CategoryLevelId",
                schema: "Xpense",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_TagTransaction_Tag_TagsId",
                schema: "Xpense",
                table: "TagTransaction");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_CurrencyExchangeRateAudits_CurrencyExchangeRateAuditId",
                schema: "Xpense",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "AccountTag",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "CategoryLevels",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "Country",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "CurrencyExchangeRateAudits",
                schema: "Currency");

            migrationBuilder.DropTable(
                name: "CurrencyExchangeRate",
                schema: "Currency");

            migrationBuilder.DropTable(
                name: "Currencies",
                schema: "Currency");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_CurrencyExchangeRateAuditId",
                schema: "Xpense",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Categories_CategoryLevelId",
                schema: "Xpense",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_CurrencyId",
                schema: "Xpense",
                table: "Accounts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Tag",
                schema: "Xpense",
                table: "Tag");

            migrationBuilder.DropColumn(
                name: "CurrencyExchangeRateAuditId",
                schema: "Xpense",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                schema: "Xpense",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "IsTransactionLocked",
                schema: "Xpense",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "Xpense",
                table: "Accounts");

            migrationBuilder.RenameTable(
                name: "Tag",
                schema: "Xpense",
                newName: "Tags",
                newSchema: "Xpense");

            migrationBuilder.RenameColumn(
                name: "CategoryLevelId",
                schema: "Xpense",
                table: "Categories",
                newName: "Priority");

            migrationBuilder.RenameColumn(
                name: "Number",
                schema: "Xpense",
                table: "Accounts",
                newName: "AccountNumber");

            migrationBuilder.RenameIndex(
                name: "IX_Accounts_Number",
                schema: "Xpense",
                table: "Accounts",
                newName: "IX_Accounts_AccountNumber");

            migrationBuilder.RenameIndex(
                name: "IX_Tag_Name",
                schema: "Xpense",
                table: "Tags",
                newName: "IX_Tags_Name");

            migrationBuilder.AlterColumn<int>(
                name: "FromAccountId",
                schema: "Xpense",
                table: "Transactions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Tags",
                schema: "Xpense",
                table: "Tags",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TagTransaction_Tags_TagsId",
                schema: "Xpense",
                table: "TagTransaction",
                column: "TagsId",
                principalSchema: "Xpense",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TagTransaction_Tags_TagsId",
                schema: "Xpense",
                table: "TagTransaction");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Tags",
                schema: "Xpense",
                table: "Tags");

            migrationBuilder.EnsureSchema(
                name: "Currency");

            migrationBuilder.RenameTable(
                name: "Tags",
                schema: "Xpense",
                newName: "Tag",
                newSchema: "Xpense");

            migrationBuilder.RenameColumn(
                name: "Priority",
                schema: "Xpense",
                table: "Categories",
                newName: "CategoryLevelId");

            migrationBuilder.RenameColumn(
                name: "AccountNumber",
                schema: "Xpense",
                table: "Accounts",
                newName: "Number");

            migrationBuilder.RenameIndex(
                name: "IX_Accounts_AccountNumber",
                schema: "Xpense",
                table: "Accounts",
                newName: "IX_Accounts_Number");

            migrationBuilder.RenameIndex(
                name: "IX_Tags_Name",
                schema: "Xpense",
                table: "Tag",
                newName: "IX_Tag_Name");

            migrationBuilder.AlterColumn<int>(
                name: "FromAccountId",
                schema: "Xpense",
                table: "Transactions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrencyExchangeRateAuditId",
                schema: "Xpense",
                table: "Transactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CurrencyId",
                schema: "Xpense",
                table: "Accounts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsTransactionLocked",
                schema: "Xpense",
                table: "Accounts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                schema: "Xpense",
                table: "Accounts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Tag",
                schema: "Xpense",
                table: "Tag",
                column: "Id");

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
                name: "CategoryLevels",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Weight = table.Column<int>(type: "int", nullable: false)
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
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsoCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Country",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    DialCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
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
                    ForeignCurrencyId = table.Column<int>(type: "int", nullable: false),
                    PrincipalCurrencyId = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
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
                name: "CurrencyExchangeRateAudits",
                schema: "Currency",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExchangeRateId = table.Column<int>(type: "int", nullable: false),
                    ForeignCurrencyId = table.Column<int>(type: "int", nullable: false),
                    PrincipalCurrencyId = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CurrencyExchangeRateAuditId",
                schema: "Xpense",
                table: "Transactions",
                column: "CurrencyExchangeRateAuditId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CategoryLevelId",
                schema: "Xpense",
                table: "Categories",
                column: "CategoryLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_CurrencyId",
                schema: "Xpense",
                table: "Accounts",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTag_TagsId",
                schema: "Xpense",
                table: "AccountTag",
                column: "TagsId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Currencies_CurrencyId",
                schema: "Xpense",
                table: "Accounts",
                column: "CurrencyId",
                principalSchema: "Currency",
                principalTable: "Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_CategoryLevels_CategoryLevelId",
                schema: "Xpense",
                table: "Categories",
                column: "CategoryLevelId",
                principalSchema: "Xpense",
                principalTable: "CategoryLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TagTransaction_Tag_TagsId",
                schema: "Xpense",
                table: "TagTransaction",
                column: "TagsId",
                principalSchema: "Xpense",
                principalTable: "Tag",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_CurrencyExchangeRateAudits_CurrencyExchangeRateAuditId",
                schema: "Xpense",
                table: "Transactions",
                column: "CurrencyExchangeRateAuditId",
                principalSchema: "Currency",
                principalTable: "CurrencyExchangeRateAudits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

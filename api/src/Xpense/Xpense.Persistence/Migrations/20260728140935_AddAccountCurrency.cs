using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xpense.Persistence.Migrations
{
    /// <summary>
    /// Accounts become denominated: a Currency column, and a balance in minor units rather than
    /// a decimal.
    /// <para>
    /// EF scaffolded this as DROP Balance + ADD BalanceCents, which discards every balance. That
    /// is harmless on today's empty database and dangerous the moment it is not, so the columns
    /// are added first, the existing decimal balances are converted into minor units, and only
    /// then is the old column dropped. Existing rows default to EUR.
    /// </para>
    /// </summary>
    public partial class AddAccountCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BalanceCents",
                schema: "Xpense",
                table: "Accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "Currency",
                schema: "Xpense",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // 12.34 -> 1234. ROUND before the cast so 0.1+0.2 style artefacts cannot truncate.
            migrationBuilder.Sql(
                @"UPDATE ""Xpense"".""Accounts"" SET ""BalanceCents"" = ROUND(""Balance"" * 100)::bigint;");

            migrationBuilder.DropColumn(
                name: "Balance",
                schema: "Xpense",
                table: "Accounts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Balance",
                schema: "Xpense",
                table: "Accounts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                @"UPDATE ""Xpense"".""Accounts"" SET ""Balance"" = ""BalanceCents"" / 100.0;");

            migrationBuilder.DropColumn(
                name: "BalanceCents",
                schema: "Xpense",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Currency",
                schema: "Xpense",
                table: "Accounts");
        }
    }
}

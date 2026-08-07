using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xpense.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenamePrioritiesToNecessityScale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Xpense"."Priorities" SET "Label" = 'Essential', "Weight" = 1 WHERE "Id" = 1;
                UPDATE "Xpense"."Priorities" SET "Label" = 'Important', "Weight" = 2 WHERE "Id" = 2;
                UPDATE "Xpense"."Priorities" SET "Label" = 'Useful',    "Weight" = 3 WHERE "Id" = 3;
                UPDATE "Xpense"."Priorities" SET "Label" = 'Optional',  "Weight" = 4 WHERE "Id" = 4;
                UPDATE "Xpense"."Priorities" SET "Label" = 'Avoidable', "Weight" = 5 WHERE "Id" = 5;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "Xpense",
                table: "Priorities",
                keyColumn: "Id",
                keyValue: 1,
                column: "Label",
                value: "Extreme");

            migrationBuilder.UpdateData(
                schema: "Xpense",
                table: "Priorities",
                keyColumn: "Id",
                keyValue: 2,
                column: "Label",
                value: "High");

            migrationBuilder.UpdateData(
                schema: "Xpense",
                table: "Priorities",
                keyColumn: "Id",
                keyValue: 3,
                column: "Label",
                value: "Medium");

            migrationBuilder.UpdateData(
                schema: "Xpense",
                table: "Priorities",
                keyColumn: "Id",
                keyValue: 4,
                column: "Label",
                value: "Low");

            migrationBuilder.UpdateData(
                schema: "Xpense",
                table: "Priorities",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Label", "Weight" },
                values: new object[] { "None", 0.0 });
        }
    }
}

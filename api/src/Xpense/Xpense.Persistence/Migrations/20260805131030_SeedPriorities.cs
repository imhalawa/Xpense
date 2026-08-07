using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Xpense.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedPriorities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Scaffolded as InsertData from the HasData call, then rewritten as SQL for the
            // ON CONFLICT clause. Any database that ran the old startup seeder already holds these
            // five rows, and a plain insert would fail there on the primary key -- a migration that
            // works on every fresh database and only on a fresh database is worse than no migration.
            migrationBuilder.Sql(
                """
                INSERT INTO "Xpense"."Priorities" ("Id", "CreatedAt", "IsDeleted", "Label", "UpdatedAt", "Weight")
                VALUES
                    (1, '2026-08-05T00:00:00Z', false, 'Extreme', NULL, 1),
                    (2, '2026-08-05T00:00:00Z', false, 'High',    NULL, 2),
                    (3, '2026-08-05T00:00:00Z', false, 'Medium',  NULL, 3),
                    (4, '2026-08-05T00:00:00Z', false, 'Low',     NULL, 4),
                    (5, '2026-08-05T00:00:00Z', false, 'None',    NULL, 0)
                ON CONFLICT DO NOTHING;
                """);

            // Inserting explicit Ids does not advance the identity sequence, which is still at 1.
            // Nothing creates a Priority at runtime today, but the first thing that does would
            // collide on the primary key and the error would point at the wrong cause.
            migrationBuilder.Sql(
                """SELECT setval(pg_get_serial_sequence('"Xpense"."Priorities"', 'Id'), 5, true);""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "Xpense",
                table: "Priorities",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                schema: "Xpense",
                table: "Priorities",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                schema: "Xpense",
                table: "Priorities",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                schema: "Xpense",
                table: "Priorities",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                schema: "Xpense",
                table: "Priorities",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.Sql(
                """SELECT setval(pg_get_serial_sequence('"Xpense"."Priorities"', 'Id'), 1, false);""");
        }
    }
}

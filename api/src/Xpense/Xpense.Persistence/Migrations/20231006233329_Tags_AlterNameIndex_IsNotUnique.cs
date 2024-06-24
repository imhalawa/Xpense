using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xpense.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Tags_AlterNameIndex_IsNotUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tag_Name",
                schema: "Xpense",
                table: "Tag");

            migrationBuilder.CreateIndex(
                name: "IX_Tag_Name",
                schema: "Xpense",
                table: "Tag",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tag_Name",
                schema: "Xpense",
                table: "Tag");

            migrationBuilder.CreateIndex(
                name: "IX_Tag_Name",
                schema: "Xpense",
                table: "Tag",
                column: "Name",
                unique: true);
        }
    }
}

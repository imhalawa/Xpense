using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xpense.Persistence.Migrations
{
    public partial class AddLegacyClaimTokens : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClaimTokens",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DatasetDownloadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpectedRecordCount = table.Column<int>(type: "integer", nullable: true),
                    ExpectedTypeCounts = table.Column<string>(type: "jsonb", nullable: true),
                    ExpectedManifestHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: true),
                    ExpectedSourceContentHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: true),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimTokens", x => x.Id);
                    table.CheckConstraint("CK_ClaimToken_Consumed", "\"ConsumedAt\" IS NULL OR (\"DatasetDownloadedAt\" IS NOT NULL AND \"ConsumedAt\" >= \"DatasetDownloadedAt\")");
                    table.CheckConstraint("CK_ClaimToken_Expiry", "\"ExpiresAt\" > \"CreatedAt\"");
                    table.CheckConstraint("CK_ClaimToken_ManifestHash", "\"ExpectedManifestHash\" IS NULL OR octet_length(\"ExpectedManifestHash\") = 32");
                    table.CheckConstraint("CK_ClaimToken_Purpose", "\"Purpose\" = 'legacy-claim-v1'");
                    table.CheckConstraint("CK_ClaimToken_Snapshot", "(\"DatasetDownloadedAt\" IS NULL AND \"ExpectedRecordCount\" IS NULL AND \"ExpectedTypeCounts\" IS NULL AND \"ExpectedManifestHash\" IS NULL AND \"ExpectedSourceContentHash\" IS NULL) OR (\"DatasetDownloadedAt\" IS NOT NULL AND \"ExpectedRecordCount\" >= 0 AND \"ExpectedTypeCounts\" IS NOT NULL AND \"ExpectedManifestHash\" IS NOT NULL AND \"ExpectedSourceContentHash\" IS NOT NULL)");
                    table.CheckConstraint("CK_ClaimToken_SourceContentHash", "\"ExpectedSourceContentHash\" IS NULL OR octet_length(\"ExpectedSourceContentHash\") = 32");
                    table.CheckConstraint("CK_ClaimToken_TokenHash", "octet_length(\"TokenHash\") = 32");
                    table.CheckConstraint("CK_ClaimToken_Updated", "\"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_ClaimTokens_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "Xpense",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimTokens_Purpose",
                schema: "Xpense",
                table: "ClaimTokens",
                column: "Purpose",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimTokens_TokenHash",
                schema: "Xpense",
                table: "ClaimTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimTokens_UserId",
                schema: "Xpense",
                table: "ClaimTokens",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimTokens",
                schema: "Xpense");
        }
    }
}

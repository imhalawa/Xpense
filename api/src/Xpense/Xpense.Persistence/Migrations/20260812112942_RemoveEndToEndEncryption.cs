using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xpense.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEndToEndEncryption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimTokens",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "RecordEnvelopes",
                schema: "Xpense");

            migrationBuilder.DropColumn(
                name: "Nonce",
                schema: "Xpense",
                table: "EncryptedRecords");

            migrationBuilder.DropColumn(
                name: "ProtocolVersion",
                schema: "Xpense",
                table: "EncryptedRecords");

            migrationBuilder.RenameColumn(
                name: "Ciphertext",
                schema: "Xpense",
                table: "EncryptedRecords",
                newName: "Payload");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Payload",
                schema: "Xpense",
                table: "EncryptedRecords",
                newName: "Ciphertext");

            migrationBuilder.AddColumn<byte[]>(
                name: "Nonce",
                schema: "Xpense",
                table: "EncryptedRecords",
                type: "bytea",
                maxLength: 4096,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "ProtocolVersion",
                schema: "Xpense",
                table: "EncryptedRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ClaimTokens",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DatasetDownloadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpectedManifestHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: true),
                    ExpectedRecordCount = table.Column<int>(type: "integer", nullable: true),
                    ExpectedSourceContentHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: true),
                    ExpectedTypeCounts = table.Column<string>(type: "jsonb", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "RecordEnvelopes",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EncapsulatedKey = table.Column<byte[]>(type: "bytea", maxLength: 4096, nullable: true),
                    EncryptedRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Nonce = table.Column<byte[]>(type: "bytea", maxLength: 4096, nullable: false),
                    ProtocolVersion = table.Column<int>(type: "integer", nullable: false),
                    WrappedKey = table.Column<byte[]>(type: "bytea", maxLength: 4096, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordEnvelopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecordEnvelopes_EncryptedRecords_EncryptedRecordId",
                        column: x => x.EncryptedRecordId,
                        principalSchema: "Xpense",
                        principalTable: "EncryptedRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecordEnvelopes_Groups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "Xpense",
                        principalTable: "Groups",
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

            migrationBuilder.CreateIndex(
                name: "IX_RecordEnvelopes_EncryptedRecordId_GroupId",
                schema: "Xpense",
                table: "RecordEnvelopes",
                columns: new[] { "EncryptedRecordId", "GroupId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_RecordEnvelopes_GroupId",
                schema: "Xpense",
                table: "RecordEnvelopes",
                column: "GroupId");
        }
    }
}

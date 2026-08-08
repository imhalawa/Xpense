using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xpense.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVaultIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingRegistrations",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AttestationState = table.Column<string>(type: "character varying(16384)", maxLength: 16384, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingRegistrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserEncryptionIdentities",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicKey = table.Column<byte[]>(type: "bytea", maxLength: 4096, nullable: false),
                    EncryptedPrivateKey = table.Column<byte[]>(type: "bytea", maxLength: 4096, nullable: false),
                    Nonce = table.Column<byte[]>(type: "bytea", maxLength: 4096, nullable: false),
                    ProtocolVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEncryptionIdentities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserEncryptionIdentities_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "Xpense",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ciphertext = table.Column<byte[]>(type: "bytea", maxLength: 65536, nullable: false),
                    Nonce = table.Column<byte[]>(type: "bytea", maxLength: 4096, nullable: false),
                    ProtocolVersion = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "Xpense",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VaultWrappers",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    CredentialId = table.Column<byte[]>(type: "bytea", maxLength: 4096, nullable: true),
                    Salt = table.Column<byte[]>(type: "bytea", maxLength: 4096, nullable: false),
                    Ciphertext = table.Column<byte[]>(type: "bytea", maxLength: 4096, nullable: false),
                    Nonce = table.Column<byte[]>(type: "bytea", maxLength: 4096, nullable: false),
                    Parameters = table.Column<string>(type: "jsonb", nullable: true),
                    AuthenticationTokenHash = table.Column<byte[]>(type: "bytea", maxLength: 4096, nullable: true),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProtocolVersion = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaultWrappers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VaultWrappers_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "Xpense",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingRegistrations_NormalizedEmail",
                schema: "Xpense",
                table: "PendingRegistrations",
                column: "NormalizedEmail",
                unique: true,
                filter: "\"ConsumedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserEncryptionIdentities_UserId",
                schema: "Xpense",
                table: "UserEncryptionIdentities",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_UserId",
                schema: "Xpense",
                table: "UserProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VaultWrappers_AuthenticationTokenHash",
                schema: "Xpense",
                table: "VaultWrappers",
                column: "AuthenticationTokenHash",
                unique: true,
                filter: "\"AuthenticationTokenHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VaultWrappers_UserId_Kind_CredentialId",
                schema: "Xpense",
                table: "VaultWrappers",
                columns: new[] { "UserId", "Kind", "CredentialId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingRegistrations",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "UserEncryptionIdentities",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "UserProfiles",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "VaultWrappers",
                schema: "Xpense");
        }
    }
}

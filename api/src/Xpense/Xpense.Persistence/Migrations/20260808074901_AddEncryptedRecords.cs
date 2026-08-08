using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xpense.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEncryptedRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "EncryptedRecordSequence",
                schema: "Xpense");

            migrationBuilder.CreateTable(
                name: "EncryptedRecords",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordType = table.Column<int>(type: "integer", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ProtocolVersion = table.Column<int>(type: "integer", nullable: false),
                    Nonce = table.Column<byte[]>(type: "bytea", maxLength: 4096, nullable: false),
                    Ciphertext = table.Column<byte[]>(type: "bytea", maxLength: 65536, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('\"Xpense\".\"EncryptedRecordSequence\"')"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncryptedRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncryptedRecords_SharedResources_ParentResourceId",
                        column: x => x.ParentResourceId,
                        principalSchema: "Xpense",
                        principalTable: "SharedResources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EncryptedRecords_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
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
                    EncryptedRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    WrappedKey = table.Column<byte[]>(type: "bytea", maxLength: 4096, nullable: false),
                    Nonce = table.Column<byte[]>(type: "bytea", maxLength: 4096, nullable: false),
                    EncapsulatedKey = table.Column<byte[]>(type: "bytea", maxLength: 4096, nullable: true),
                    ProtocolVersion = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "SyncOperations",
                schema: "Xpense",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EncryptedRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncOperations_EncryptedRecords_EncryptedRecordId",
                        column: x => x.EncryptedRecordId,
                        principalSchema: "Xpense",
                        principalTable: "EncryptedRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SyncOperations_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "Xpense",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EncryptedRecords_OwnerUserId",
                schema: "Xpense",
                table: "EncryptedRecords",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EncryptedRecords_ParentResourceId",
                schema: "Xpense",
                table: "EncryptedRecords",
                column: "ParentResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_EncryptedRecords_SequenceNumber",
                schema: "Xpense",
                table: "EncryptedRecords",
                column: "SequenceNumber",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_SyncOperations_EncryptedRecordId",
                schema: "Xpense",
                table: "SyncOperations",
                column: "EncryptedRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncOperations_UserId_IdempotencyKey",
                schema: "Xpense",
                table: "SyncOperations",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecordEnvelopes",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "SyncOperations",
                schema: "Xpense");

            migrationBuilder.DropTable(
                name: "EncryptedRecords",
                schema: "Xpense");

            migrationBuilder.DropSequence(
                name: "EncryptedRecordSequence",
                schema: "Xpense");
        }
    }
}

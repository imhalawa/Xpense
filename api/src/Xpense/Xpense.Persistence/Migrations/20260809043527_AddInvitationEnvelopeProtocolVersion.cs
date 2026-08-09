using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xpense.Persistence.Migrations
{
    public partial class AddInvitationEnvelopeProtocolVersion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EnvelopeProtocolVersion",
                schema: "Xpense",
                table: "GroupInvitations",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"Xpense\".\"GroupInvitations\" SET \"EnvelopeProtocolVersion\" = 1 WHERE \"GroupKeyEnvelope\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GroupInvitation_Envelope_Protocol",
                schema: "Xpense",
                table: "GroupInvitations",
                sql: "(\"GroupKeyEnvelope\" IS NULL AND \"EnvelopeProtocolVersion\" IS NULL) OR (\"GroupKeyEnvelope\" IS NOT NULL AND \"EnvelopeProtocolVersion\" = 1)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_GroupInvitation_Envelope_Protocol",
                schema: "Xpense",
                table: "GroupInvitations");

            migrationBuilder.DropColumn(
                name: "EnvelopeProtocolVersion",
                schema: "Xpense",
                table: "GroupInvitations");
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class GroupInvitationEntityTypeConfiguration : IEntityTypeConfiguration<GroupInvitation>
{
    private const string XpenseSchema = "Xpense";

    public void Configure(EntityTypeBuilder<GroupInvitation> builder)
    {
        builder.ToTable("GroupInvitations", XpenseSchema);
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.TargetNormalizedEmail).HasMaxLength(256);
        builder.Property(invitation => invitation.TokenHash).HasMaxLength(4096).IsRequired();
        builder.Property(invitation => invitation.GroupKeyEnvelope).HasMaxLength(4096);
        builder.Property(invitation => invitation.EnvelopeProtocolVersion);
        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();
        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(invitation => invitation.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<XpenseUser>()
            .WithMany()
            .HasForeignKey(invitation => invitation.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<XpenseUser>()
            .WithMany()
            .HasForeignKey(invitation => invitation.AcceptedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_GroupInvitation_Envelope_Protocol",
            "(\"GroupKeyEnvelope\" IS NULL AND \"EnvelopeProtocolVersion\" IS NULL) OR (\"GroupKeyEnvelope\" IS NOT NULL AND \"EnvelopeProtocolVersion\" = 1)"));
    }
}

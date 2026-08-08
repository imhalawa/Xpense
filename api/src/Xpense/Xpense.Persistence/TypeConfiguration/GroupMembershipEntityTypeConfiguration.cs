using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class GroupMembershipEntityTypeConfiguration : IEntityTypeConfiguration<GroupMembership>
{
    private const string XpenseSchema = "Xpense";

    public void Configure(EntityTypeBuilder<GroupMembership> builder)
    {
        builder.ToTable("GroupMemberships", XpenseSchema);
        builder.HasKey(membership => membership.Id);
        builder.Property(membership => membership.GroupKeyEnvelope).HasMaxLength(4096);
        builder.HasIndex(membership => new { membership.GroupId, membership.UserId }).IsUnique();
        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(membership => membership.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<XpenseUser>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

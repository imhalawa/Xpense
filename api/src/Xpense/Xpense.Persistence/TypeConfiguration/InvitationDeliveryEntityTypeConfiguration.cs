using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class InvitationDeliveryEntityTypeConfiguration : IEntityTypeConfiguration<InvitationDelivery>
{
    private const string XpenseSchema = "Xpense";

    public void Configure(EntityTypeBuilder<InvitationDelivery> builder)
    {
        builder.ToTable("InvitationDeliveries", XpenseSchema);
        builder.HasKey(delivery => delivery.Id);
        builder.Property(delivery => delivery.EmailAddress).HasMaxLength(256).IsRequired();
        builder.Property(delivery => delivery.ProtectedPayload).HasMaxLength(65536);
        builder.Property(delivery => delivery.LastError).HasMaxLength(2000);
        builder.HasIndex(delivery => delivery.InvitationId).IsUnique();
        builder.HasOne<GroupInvitation>()
            .WithOne()
            .HasForeignKey<InvitationDelivery>(delivery => delivery.InvitationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class PendingRegistrationEntityTypeConfiguration : IEntityTypeConfiguration<PendingRegistration>
{
    private const string XpenseSchema = "Xpense";

    public void Configure(EntityTypeBuilder<PendingRegistration> builder)
    {
        builder.ToTable("PendingRegistrations", XpenseSchema);
        builder.HasKey(registration => registration.Id);
        builder.Property(registration => registration.NormalizedEmail).HasMaxLength(256).IsRequired();
        builder.Property(registration => registration.AttestationState).HasMaxLength(16384).IsRequired();
        builder.HasIndex(registration => registration.NormalizedEmail)
            .IsUnique()
            .HasFilter("\"ConsumedAt\" IS NULL");
    }
}

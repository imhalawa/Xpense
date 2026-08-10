using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class UserEncryptionIdentityEntityTypeConfiguration : IEntityTypeConfiguration<UserEncryptionIdentity>
{
    private const string XpenseSchema = "Xpense";

    public void Configure(EntityTypeBuilder<UserEncryptionIdentity> builder)
    {
        builder.ToTable("UserEncryptionIdentities", XpenseSchema);
        builder.HasKey(identity => identity.Id);
        builder.Property(identity => identity.PublicKey).HasMaxLength(4096).IsRequired();
        builder.Property(identity => identity.EncryptedPrivateKey).HasMaxLength(4096).IsRequired();
        builder.Property(identity => identity.Nonce).HasMaxLength(4096).IsRequired();
        builder.HasIndex(identity => identity.UserId).IsUnique();
        builder.HasOne<XpenseUser>()
            .WithOne()
            .HasForeignKey<UserEncryptionIdentity>(identity => identity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

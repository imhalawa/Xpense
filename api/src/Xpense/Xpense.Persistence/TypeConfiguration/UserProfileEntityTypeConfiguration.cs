using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class UserProfileEntityTypeConfiguration : IEntityTypeConfiguration<UserProfile>
{
    private const string XpenseSchema = "Xpense";

    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles", XpenseSchema);
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.Ciphertext).HasMaxLength(65536).IsRequired();
        builder.Property(profile => profile.Nonce).HasMaxLength(4096).IsRequired();
        builder.HasIndex(profile => profile.UserId).IsUnique();
        builder.HasOne<XpenseUser>()
            .WithOne()
            .HasForeignKey<UserProfile>(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

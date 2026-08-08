using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class VaultWrapperEntityTypeConfiguration : IEntityTypeConfiguration<VaultWrapper>
{
    private const string XpenseSchema = "Xpense";

    public void Configure(EntityTypeBuilder<VaultWrapper> builder)
    {
        builder.ToTable("VaultWrappers", XpenseSchema);
        builder.HasKey(wrapper => wrapper.Id);
        builder.Property(wrapper => wrapper.CredentialId).HasMaxLength(4096);
        builder.Property(wrapper => wrapper.Salt).HasMaxLength(4096).IsRequired();
        builder.Property(wrapper => wrapper.Ciphertext).HasMaxLength(4096).IsRequired();
        builder.Property(wrapper => wrapper.Nonce).HasMaxLength(4096).IsRequired();
        builder.Property(wrapper => wrapper.Parameters).HasColumnType("jsonb");
        builder.Property(wrapper => wrapper.AuthenticationTokenHash).HasMaxLength(4096);
        builder.Property(wrapper => wrapper.Label).HasMaxLength(100);
        builder.HasIndex(wrapper => new { wrapper.UserId, wrapper.Kind, wrapper.CredentialId }).IsUnique();
        builder.HasIndex(wrapper => wrapper.AuthenticationTokenHash)
            .IsUnique()
            .HasFilter("\"AuthenticationTokenHash\" IS NOT NULL");
        builder.HasOne<XpenseUser>()
            .WithMany()
            .HasForeignKey(wrapper => wrapper.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

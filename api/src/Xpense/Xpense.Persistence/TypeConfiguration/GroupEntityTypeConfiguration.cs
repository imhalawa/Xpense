using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class GroupEntityTypeConfiguration : IEntityTypeConfiguration<Group>
{
    private const string XpenseSchema = "Xpense";

    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("Groups", XpenseSchema);
        builder.HasKey(group => group.Id);
        builder.Property(group => group.NameCiphertext).HasMaxLength(4096).IsRequired();
        builder.Property(group => group.NameNonce).HasMaxLength(4096).IsRequired();
        builder.HasOne<XpenseUser>()
            .WithMany()
            .HasForeignKey(group => group.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

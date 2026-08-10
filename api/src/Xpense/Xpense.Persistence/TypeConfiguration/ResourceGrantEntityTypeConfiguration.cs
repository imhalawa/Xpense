using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class ResourceGrantEntityTypeConfiguration : IEntityTypeConfiguration<ResourceGrant>
{
    private const string XpenseSchema = "Xpense";

    public void Configure(EntityTypeBuilder<ResourceGrant> builder)
    {
        builder.ToTable("ResourceGrants", XpenseSchema);
        builder.HasKey(grant => grant.Id);
        builder.HasIndex(grant => new { grant.GroupId, grant.ResourceType, grant.ResourceId })
            .IsUnique()
            .HasFilter("\"State\" = 0");
        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(grant => grant.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<XpenseUser>()
            .WithMany()
            .HasForeignKey(grant => grant.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

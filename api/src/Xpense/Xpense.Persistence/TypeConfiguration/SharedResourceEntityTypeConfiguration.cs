using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class SharedResourceEntityTypeConfiguration : IEntityTypeConfiguration<SharedResource>
{
    private const string XpenseSchema = "Xpense";

    public void Configure(EntityTypeBuilder<SharedResource> builder)
    {
        builder.ToTable("SharedResources", XpenseSchema);
        builder.HasKey(resource => resource.Id);
        builder.HasOne<XpenseUser>()
            .WithMany()
            .HasForeignKey(resource => resource.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

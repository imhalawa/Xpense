using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Services.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public sealed class TransferLegEntityTypeConfiguration : BaseEntityTypeConfiguration<TransferLeg>
{
    public override void Configure(EntityTypeBuilder<TransferLeg> builder)
    {
        base.Configure(builder);
        builder.Metadata.SetSchema(XpenseSchema);
        builder.HasIndex(leg => new { leg.TransferId, leg.Direction }).IsUnique();
        builder.HasOne(leg => leg.Account)
            .WithMany()
            .HasForeignKey(leg => leg.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

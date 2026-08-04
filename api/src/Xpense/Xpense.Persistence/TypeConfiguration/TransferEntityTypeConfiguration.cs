using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public sealed class TransferEntityTypeConfiguration : BaseEntityTypeConfiguration<Transfer>
{
    public override void Configure(EntityTypeBuilder<Transfer> builder)
    {
        base.Configure(builder);
        builder.Metadata.SetSchema(XpenseSchema);
        builder.Property(transfer => transfer.Reason).HasMaxLength(500);

        builder.HasOne(transfer => transfer.SourceAccount)
            .WithMany()
            .HasForeignKey(transfer => transfer.SourceAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transfer => transfer.DestinationAccount)
            .WithMany()
            .HasForeignKey(transfer => transfer.DestinationAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(transfer => transfer.Legs)
            .WithOne(leg => leg.Transfer)
            .HasForeignKey(leg => leg.TransferId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class AccountEntityTypeConfiguration : BaseEntityTypeConfiguration<Account>
{
    public override void Configure(EntityTypeBuilder<Account> builder)
    {
        base.Configure(builder);
        builder.Metadata.SetSchema(XpenseSchema);

        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.AccountNumber).HasMaxLength(10).IsRequired().IsFixedLength();
        builder.Property(e => e.Currency).IsRequired();

        // Balance is projected from BalanceCents + Currency, not stored.
        builder.Ignore(e => e.Balance);

        // Account Number Index 
        builder.HasIndex(e => e.AccountNumber).IsUnique();
    }
}
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

        builder.Property(account => account.Label).HasMaxLength(100).IsRequired();
        builder.Property(account => account.AccountNumber).HasMaxLength(10).IsRequired().IsFixedLength();
        builder.Property(account => account.Currency).IsRequired();

        // Balance is projected from BalanceMinorUnits + Currency, not stored.
        builder.Ignore(account => account.Balance);

        // AccountNumber is the public identifier, so it has to be unique.
        builder.HasIndex(account => account.AccountNumber).IsUnique();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.API.Data.Models;

namespace Xpense.API.Data.TypeConfiguration;

public class AccountEntityTypeConfiguration: BaseEntityTypeConfiguration<Account>
{
    public override void Configure(EntityTypeBuilder<Account> builder)
    {
        base.Configure(builder);
        builder.Metadata.SetSchema(XpenseSchema);


        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Number).HasMaxLength(10).IsRequired().IsFixedLength();

        // Account Number Index 
        builder.HasIndex(e => e.Number).IsUnique();

        // Account(M) - Currency(1)
        builder.HasOne(e => e.Currency).WithMany(c => c.LinkedAccounts).HasForeignKey(e=>e.CurrencyId);

        // Account(m) - Tags(m)
        builder.HasMany(e => e.Tags).WithMany(e => e.Accounts);
    }
}
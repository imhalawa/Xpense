using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.API.Data.Models;

namespace Xpense.API.Data.TypeConfiguration;

public class AccountEntityTypeConfiguration: BaseEntityTypeConfiguration<Account>
{
    public override void Configure(EntityTypeBuilder<Account> builder)
    {
        base.Configure(builder);

        // Account(1) - Transaction(M)
        builder.HasMany(e => e.AccountTransactions).WithOne(e => e.FromAccount);
    }
}
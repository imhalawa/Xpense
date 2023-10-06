using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.API.Data.Models;

namespace Xpense.API.Data.TypeConfiguration;

public class TransactionEntityTypeConfiguration: BaseEntityTypeConfiguration<Transaction>
{
    public override void Configure(EntityTypeBuilder<Transaction> builder)
    {
        base.Configure(builder);
        builder.Metadata.SetSchema(XpenseSchema);

        // Transaction (M) - Account(1) [Deposit Transactions]
        builder.HasOne(e => e.ToAccount).WithMany(e => e.DepositTransactions).HasForeignKey(e=>e.ToAccountId).OnDelete(DeleteBehavior.Restrict);

        // Transaction (M) - Account(1) [Withdraw Transactions]
        builder.HasOne(e => e.FromAccount).WithMany(e => e.WithdrawTransactions).HasForeignKey(e => e.FromAccountId).OnDelete(DeleteBehavior.Restrict);

        // Transaction (M) - Category(1)
        builder.HasOne(e => e.Category).WithMany(e => e.Transactions).HasForeignKey(e=>e.CategoryId);

        // Transaction (M) - Tag(M) 
        builder.HasMany(e => e.Tags).WithMany(e => e.Transactions);

        // Transaction (M) - CurrencyExchangeRateAudit(1)
        builder.HasOne(e => e.CurrencyExchangeRateAudit).WithMany(e => e.Transactions).HasForeignKey(e=>e.CurrencyExchangeRateAuditId);
    }
}
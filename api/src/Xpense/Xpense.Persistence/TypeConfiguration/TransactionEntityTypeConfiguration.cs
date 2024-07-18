using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Services.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class TransactionEntityTypeConfiguration : BaseEntityTypeConfiguration<Transaction>
{
    public override void Configure(EntityTypeBuilder<Transaction> builder)
    {
        base.Configure(builder);
        builder.Metadata.SetSchema(XpenseSchema);

        // Transaction (M) - Account(1)
        builder.HasOne(e => e.Account).WithMany(e => e.Transactions).HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);

        // Transaction (M) - Category(1)
        builder.HasOne(e => e.Category).WithMany(e => e.Transactions).HasForeignKey(e => e.CategoryId);

        // Transaction (M) - Tag(M) 
        builder
            .HasMany(e => e.Tags)
            .WithMany(e => e.Transactions)
            .UsingEntity("TransactionTags",
               l => l.HasOne(typeof(Tag)).WithMany().HasForeignKey("TagId").HasPrincipalKey(nameof(Tag.Id)),
               r => r.HasOne(typeof(Transaction)).WithMany().HasForeignKey("TransactionId").HasPrincipalKey(nameof(Transaction.Id)),
               j => j.HasKey("TransactionId", "TagId")
            );

        // Transaction (1) - Merchant (M)
        builder.HasOne(e => e.Merchant).WithMany(e => e.Transactions).HasForeignKey(e => e.MerchantId);
    }
}
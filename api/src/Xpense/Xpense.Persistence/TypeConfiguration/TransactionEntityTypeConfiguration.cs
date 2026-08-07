using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class TransactionEntityTypeConfiguration : BaseEntityTypeConfiguration<Transaction>
{
    public override void Configure(EntityTypeBuilder<Transaction> builder)
    {
        base.Configure(builder);
        builder.Metadata.SetSchema(XpenseSchema);

        builder.Ignore(transaction => transaction.Amount);
        builder.Ignore(transaction => transaction.Kind);

        builder.Property(transaction => transaction.Reason).HasMaxLength(500);

        builder.HasOne(transaction => transaction.SourceAccount)
            .WithMany()
            .HasForeignKey(transaction => transaction.SourceAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(transaction => transaction.DestinationAccount)
            .WithMany()
            .HasForeignKey(transaction => transaction.DestinationAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(transaction => transaction.Category)
            .WithMany(category => category.Transactions)
            .HasForeignKey(transaction => transaction.CategoryId);

        builder.HasOne(transaction => transaction.Merchant)
            .WithMany(merchant => merchant.Transactions)
            .HasForeignKey(transaction => transaction.MerchantId);

        builder
            .HasMany(transaction => transaction.Tags)
            .WithMany(tag => tag.Transactions)
            .UsingEntity("TransactionTags",
               tagSide => tagSide.HasOne(typeof(Tag)).WithMany().HasForeignKey("TagId").HasPrincipalKey(nameof(Tag.Id)),
               transactionSide => transactionSide.HasOne(typeof(Transaction)).WithMany().HasForeignKey("TransactionId").HasPrincipalKey(nameof(Transaction.Id)),
               joinEntity => joinEntity.HasKey("TransactionId", "TagId")
            );

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Transaction_Sides_And_Classification",
            """
            (
              ("SourceAccountId" IS NULL) <> ("DestinationAccountId" IS NULL)
              AND "CategoryId" IS NOT NULL AND "MerchantId" IS NOT NULL
            )
            OR
            (
              "SourceAccountId" IS NOT NULL AND "DestinationAccountId" IS NOT NULL
              AND "CategoryId" IS NULL AND "MerchantId" IS NULL
            )
            """));
    }
}

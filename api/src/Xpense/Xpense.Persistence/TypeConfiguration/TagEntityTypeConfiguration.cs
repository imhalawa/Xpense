using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class TagEntityTypeConfiguration : BaseEntityTypeConfiguration<Tag>
{
    public override void Configure(EntityTypeBuilder<Tag> builder)
    {
        base.Configure(builder);
        builder.Metadata.SetSchema(XpenseSchema);

        builder.Property(tag => tag.Label).HasMaxLength(100).IsRequired();
        builder.Property(tag => tag.BgColorHex).HasMaxLength(6).IsFixedLength();
        builder.Property(tag => tag.FgColorHex).HasMaxLength(6).IsFixedLength();

        // Tag Name Index
        builder.HasIndex(tag => tag.Label).IsUnique();

        // Tags (M) - Transactions (M)
        builder.HasMany(tag => tag.Transactions).WithMany(transaction => transaction.Tags).UsingEntity("TransactionTags",
           transactionSide => transactionSide.HasOne(typeof(Transaction)).WithMany().HasForeignKey("TransactionId").HasPrincipalKey(nameof(Transaction.Id)),
           tagSide => tagSide.HasOne(typeof(Tag)).WithMany().HasForeignKey("TagId").HasPrincipalKey(nameof(Tag.Id)),
           joinEntity => joinEntity.HasKey("TagId", "TransactionId")
        );
    }
}
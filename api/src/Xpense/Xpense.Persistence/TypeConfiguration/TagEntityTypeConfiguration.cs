using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Services.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class TagEntityTypeConfiguration : BaseEntityTypeConfiguration<Tag>
{
    public override void Configure(EntityTypeBuilder<Tag> builder)
    {
        base.Configure(builder);
        builder.Metadata.SetSchema(XpenseSchema);

        builder.Property(e => e.Label).HasMaxLength(100).IsRequired();
        builder.Property(e => e.BgColorHex).HasMaxLength(6).IsFixedLength();
        builder.Property(e => e.FgColorHex).HasMaxLength(6).IsFixedLength();

        // Tag Name Index
        builder.HasIndex(e => e.Label).IsUnique();

        // Tags (M) - Transactions (M)
        builder.HasMany(e => e.Transactions).WithMany(e => e.Tags).UsingEntity("TransactionTags",
           l => l.HasOne(typeof(Transaction)).WithMany().HasForeignKey("TransactionId").HasPrincipalKey(nameof(Transaction.Id)),
           r => r.HasOne(typeof(Tag)).WithMany().HasForeignKey("TagId").HasPrincipalKey(nameof(Tag.Id)),
           j => j.HasKey("TagId", "TransactionId")
        );
    }
}
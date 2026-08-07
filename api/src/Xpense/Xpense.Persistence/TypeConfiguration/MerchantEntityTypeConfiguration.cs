using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration
{
    public class MerchantEntityTypeConfiguration : BaseEntityTypeConfiguration<Merchant>
    {

        public override void Configure(EntityTypeBuilder<Merchant> builder)
        {
            base.Configure(builder);
            builder.Metadata.SetSchema(XpenseSchema);

            builder.HasIndex(merchant => merchant.Label).IsUnique();

            builder.Property(merchant => merchant.Label).HasMaxLength(100).IsRequired();

            builder.HasMany(merchant => merchant.Transactions)
                .WithOne(transaction => transaction.Merchant)
                .HasForeignKey(transaction => transaction.MerchantId);
        }
    }
}

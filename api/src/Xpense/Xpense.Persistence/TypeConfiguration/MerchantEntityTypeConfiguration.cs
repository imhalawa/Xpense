using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Services.Entities;

namespace Xpense.Persistence.TypeConfiguration
{
    public class MerchantEntityTypeConfiguration : BaseEntityTypeConfiguration<Merchant>
    {

        public override void Configure(EntityTypeBuilder<Merchant> builder)
        {
            base.Configure(builder);
            builder.Metadata.SetSchema(XpenseSchema);

            // Merchant names must be unique
            builder.HasIndex(e => e.Label).IsUnique();

            builder.Property(e => e.Label).HasMaxLength(100).IsRequired();

            //  Merchant (M) - Transaction (1)
            builder.HasMany(e => e.Transactions).WithOne(e => e.Merchant).HasForeignKey(e => e.MerchantId);
        }
    }
}

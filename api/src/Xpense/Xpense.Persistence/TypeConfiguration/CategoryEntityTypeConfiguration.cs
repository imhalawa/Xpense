using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration
{
    public class CategoryEntityTypeConfiguration : BaseEntityTypeConfiguration<Category>
    {
        public override void Configure(EntityTypeBuilder<Category> builder)
        {
            base.Configure(builder);
            builder.Metadata.SetSchema(XpenseSchema);

            builder.Property(category => category.Label).HasMaxLength(100).IsRequired();
            builder.HasIndex(category => category.Label).IsUnique();

            // Category (1) - Transaction (M) 
            builder.HasMany(category => category.Transactions)
                .WithOne(transaction => transaction.Category)
                .HasForeignKey(transaction => transaction.CategoryId);
        }
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.API.Data.Models;

namespace Xpense.API.Data.TypeConfiguration
{
    public class CategoryEntityTypeConfiguration : BaseEntityTypeConfiguration<Category>
    {
        public override void Configure(EntityTypeBuilder<Category> builder)
        {
            base.Configure(builder);
            builder.Metadata.SetSchema(XpenseSchema);

            builder.Property(e => e.Name).HasMaxLength(100).IsRequired();

            // Category (1) - Transaction (M) 
            builder.HasMany(e => e.Transactions).WithOne(e => e.Category).HasForeignKey(e => e.CategoryId);
        }
    }
}
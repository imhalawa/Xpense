using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Services.Entities;

namespace Xpense.Persistence.TypeConfiguration
{
    public class CategoryEntityTypeConfiguration : BaseEntityTypeConfiguration<Category>
    {
        public override void Configure(EntityTypeBuilder<Category> builder)
        {
            base.Configure(builder);
            builder.Metadata.SetSchema(XpenseSchema);

            builder.Property(e => e.Label).HasMaxLength(100).IsRequired();

            // Category (1) - Transaction (M) 
            builder.HasMany(e => e.Transactions).WithOne(e => e.Category).HasForeignKey(e => e.CategoryId);

            // Category (M) - Priority (1)
            builder.HasOne(e => e.Priority).WithMany(e => e.Categories).HasForeignKey(e => e.PriorityId);
        }
    }
}
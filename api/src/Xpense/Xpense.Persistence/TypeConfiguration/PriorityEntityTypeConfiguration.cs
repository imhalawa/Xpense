using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Services.Entities;

namespace Xpense.Persistence.TypeConfiguration
{
    public class PriorityEntityTypeConfiguration : BaseEntityTypeConfiguration<Priority>
    {
        public override void Configure(EntityTypeBuilder<Priority> builder)
        {
            base.Configure(builder);
            builder.Metadata.SetSchema(XpenseSchema);

            builder.Property(e => e.Label).HasMaxLength(100).IsRequired();

            // Priority (1) - Category (M) 
            builder.HasMany(e => e.Categories).WithOne(e => e.Priority).HasForeignKey(e => e.PriorityId);
        }

    }
}

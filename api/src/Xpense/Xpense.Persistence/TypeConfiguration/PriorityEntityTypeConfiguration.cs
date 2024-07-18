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

            builder.HasIndex(e => e.Label).IsUnique();

            builder.Property(e => e.Label).HasMaxLength(100).IsRequired();

            // PriorityId (1) - Category (M) 
            builder.HasMany(p => p.Categories).WithOne(c => c.Priority).HasForeignKey(c => c.PriorityId);
        }
    }
}

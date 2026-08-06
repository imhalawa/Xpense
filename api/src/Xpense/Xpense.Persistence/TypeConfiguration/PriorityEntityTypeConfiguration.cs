using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration
{
    public class PriorityEntityTypeConfiguration : BaseEntityTypeConfiguration<Priority>
    {
        public override void Configure(EntityTypeBuilder<Priority> builder)
        {
            base.Configure(builder);
            builder.Metadata.SetSchema(XpenseSchema);

            builder.HasIndex(priority => priority.Label).IsUnique();

            builder.Property(priority => priority.Label).HasMaxLength(100).IsRequired();

            builder.HasMany(priority => priority.Categories).WithOne(category => category.Priority).HasForeignKey(category => category.PriorityId);

            SeedPriorities(builder);
        }

        private static void SeedPriorities(EntityTypeBuilder<Priority> builder)
        {
            var seededAt = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc);

            builder.HasData(
                new Priority { Id = 1, Label = "Extreme", Weight = 1, CreatedAt = seededAt },
                new Priority { Id = 2, Label = "High", Weight = 2, CreatedAt = seededAt },
                new Priority { Id = 3, Label = "Medium", Weight = 3, CreatedAt = seededAt },
                new Priority { Id = 4, Label = "Low", Weight = 4, CreatedAt = seededAt },
                new Priority { Id = 5, Label = "None", Weight = 0, CreatedAt = seededAt });
        }
    }
}

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
                new Priority { Id = 1, Label = "Essential", Weight = 1, CreatedAt = seededAt },
                new Priority { Id = 2, Label = "Important", Weight = 2, CreatedAt = seededAt },
                new Priority { Id = 3, Label = "Useful", Weight = 3, CreatedAt = seededAt },
                new Priority { Id = 4, Label = "Optional", Weight = 4, CreatedAt = seededAt },
                new Priority { Id = 5, Label = "Avoidable", Weight = 5, CreatedAt = seededAt });
        }
    }
}

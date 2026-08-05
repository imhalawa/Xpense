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

            builder.HasIndex(e => e.Label).IsUnique();

            builder.Property(e => e.Label).HasMaxLength(100).IsRequired();

            // PriorityId (1) - Category (M)
            builder.HasMany(p => p.Categories).WithOne(c => c.Priority).HasForeignKey(c => c.PriorityId);

            SeedPriorities(builder);
        }

        /// <summary>
        /// Reference data, so it belongs to the schema rather than to application startup. See
        /// docs/adr/0005-reference-data-lives-in-migrations.md.
        /// <para>
        /// <c>CreatedAt</c> is a literal on purpose: <c>DateTime.UtcNow</c> here makes the model
        /// snapshot non-deterministic, and <c>migrations add</c> then emits a migration every run.
        /// </para>
        /// </summary>
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

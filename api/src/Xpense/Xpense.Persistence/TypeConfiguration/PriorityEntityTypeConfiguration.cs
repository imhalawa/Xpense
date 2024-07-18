using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;
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

            // Seed Data
            Seed(builder);
        }

        private void Seed(EntityTypeBuilder<Priority> builder)
        {
            var prioritiesJson = File.ReadAllText(@"Seeds" + Path.DirectorySeparatorChar + "PrioritySeeds.json");
            if (string.IsNullOrEmpty(prioritiesJson)) throw new InvalidOperationException("Cannot seed priorities, the seeding file is empty.");

            var priorities = Parse(prioritiesJson)!;
            builder.HasData(priorities.ToArray());
            return;

            IEnumerable<Priority> Parse(string json)
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var prioritiesElement = root.GetProperty("priorities");
                return JsonSerializer.Deserialize<Priority[]>(prioritiesElement.GetRawText())!;
            }
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class EventRecordEntityTypeConfiguration : BaseEntityTypeConfiguration<EventRecord>
{
    public override void Configure(EntityTypeBuilder<EventRecord> builder)
    {
        base.Configure(builder);

        builder.ToTable("Events", XpenseSchema);

        builder.Property(eventRecord => eventRecord.Type).HasMaxLength(200).IsRequired();
        builder.Property(eventRecord => eventRecord.Source).HasMaxLength(100).IsRequired();

        builder.Property(eventRecord => eventRecord.Body).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(eventRecord => eventRecord.EventId).IsUnique();

        builder.HasIndex(eventRecord => eventRecord.CreatedAt)
            .HasFilter("\"ProcessedAt\" IS NULL")
            .HasDatabaseName("IX_Events_Outstanding");
    }
}

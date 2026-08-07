using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class NotificationEntityTypeConfiguration : BaseEntityTypeConfiguration<Notification>
{
    public override void Configure(EntityTypeBuilder<Notification> builder)
    {
        base.Configure(builder);
        builder.Metadata.SetSchema(XpenseSchema);

        builder.Property(notification => notification.Title).HasMaxLength(200).IsRequired();
        builder.Property(notification => notification.Message).HasMaxLength(1000).IsRequired();
        builder.Property(notification => notification.Payload).HasColumnType("jsonb").IsRequired();

        builder.Property(notification => notification.PayloadHash).HasMaxLength(64).IsRequired().IsFixedLength();

        builder.HasIndex(notification => new { notification.EventId, notification.PayloadHash })
            .IsUnique()
            .HasDatabaseName("UQ_Notifications_Event_Payload");

        builder.HasIndex(notification => notification.CreatedAt).IsDescending();
        builder.HasIndex(notification => notification.ReadAt).HasFilter("\"ReadAt\" IS NULL");

    }
}

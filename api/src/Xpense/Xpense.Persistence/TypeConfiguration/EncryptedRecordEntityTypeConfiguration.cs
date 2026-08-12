using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class EncryptedRecordEntityTypeConfiguration : IEntityTypeConfiguration<EncryptedRecord>
{
    private const string SequenceDefault = "nextval('\"Xpense\".\"EncryptedRecordSequence\"')";
    private const string XpenseSchema = "Xpense";

    public void Configure(EntityTypeBuilder<EncryptedRecord> builder)
    {
        builder.ToTable("EncryptedRecords", XpenseSchema);
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Payload).HasMaxLength(65536).IsRequired();
        builder.Property(record => record.SequenceNumber).HasDefaultValueSql(SequenceDefault);
        builder.HasIndex(record => record.SequenceNumber).IsUnique();
        builder.HasOne<XpenseUser>()
            .WithMany()
            .HasForeignKey(record => record.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SharedResource>()
            .WithMany()
            .HasForeignKey(record => record.ParentResourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

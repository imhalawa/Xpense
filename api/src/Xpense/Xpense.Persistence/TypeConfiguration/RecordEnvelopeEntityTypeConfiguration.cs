using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class RecordEnvelopeEntityTypeConfiguration : IEntityTypeConfiguration<RecordEnvelope>
{
    private const string XpenseSchema = "Xpense";

    public void Configure(EntityTypeBuilder<RecordEnvelope> builder)
    {
        builder.ToTable("RecordEnvelopes", XpenseSchema);
        builder.HasKey(envelope => envelope.Id);
        builder.Property(envelope => envelope.WrappedKey).HasMaxLength(4096).IsRequired();
        builder.Property(envelope => envelope.Nonce).HasMaxLength(4096).IsRequired();
        builder.Property(envelope => envelope.EncapsulatedKey).HasMaxLength(4096);
        builder.HasIndex(envelope => new { envelope.EncryptedRecordId, envelope.GroupId })
            .IsUnique()
            .AreNullsDistinct(false);
        builder.HasOne<EncryptedRecord>()
            .WithMany()
            .HasForeignKey(envelope => envelope.EncryptedRecordId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(envelope => envelope.GroupId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

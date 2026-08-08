using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class SyncOperationEntityTypeConfiguration : IEntityTypeConfiguration<SyncOperation>
{
    private const string XpenseSchema = "Xpense";

    public void Configure(EntityTypeBuilder<SyncOperation> builder)
    {
        builder.ToTable("SyncOperations", XpenseSchema);
        builder.HasKey(operation => operation.Id);
        builder.Property(operation => operation.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(operation => new { operation.UserId, operation.IdempotencyKey }).IsUnique();
        builder.HasOne<XpenseUser>()
            .WithMany()
            .HasForeignKey(operation => operation.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<EncryptedRecord>()
            .WithMany()
            .HasForeignKey(operation => operation.EncryptedRecordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

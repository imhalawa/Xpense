using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public sealed class ClaimTokenEntityTypeConfiguration : IEntityTypeConfiguration<ClaimToken>
{
    private const string XpenseSchema = "Xpense";

    public void Configure(EntityTypeBuilder<ClaimToken> builder)
    {
        builder.ToTable("ClaimTokens", XpenseSchema);
        builder.HasKey(claimToken => claimToken.Id);
        builder.Property(claimToken => claimToken.Purpose).HasMaxLength(64).IsRequired();
        builder.Property(claimToken => claimToken.TokenHash).HasMaxLength(32).IsRequired();
        builder.Property(claimToken => claimToken.ExpectedTypeCounts).HasColumnType("jsonb");
        builder.Property(claimToken => claimToken.ExpectedManifestHash).HasMaxLength(32);
        builder.Property(claimToken => claimToken.ExpectedSourceContentHash).HasMaxLength(32);
        builder.HasIndex(claimToken => claimToken.Purpose).IsUnique();
        builder.HasIndex(claimToken => claimToken.TokenHash).IsUnique();
        builder.HasOne<XpenseUser>()
            .WithMany()
            .HasForeignKey(claimToken => claimToken.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_ClaimToken_Purpose",
                $"\"Purpose\" = '{ClaimToken.LegacyPurpose}'");
            table.HasCheckConstraint("CK_ClaimToken_TokenHash", "octet_length(\"TokenHash\") = 32");
            table.HasCheckConstraint(
                "CK_ClaimToken_ManifestHash",
                "\"ExpectedManifestHash\" IS NULL OR octet_length(\"ExpectedManifestHash\") = 32");
            table.HasCheckConstraint(
                "CK_ClaimToken_SourceContentHash",
                "\"ExpectedSourceContentHash\" IS NULL OR octet_length(\"ExpectedSourceContentHash\") = 32");
            table.HasCheckConstraint("CK_ClaimToken_Expiry", "\"ExpiresAt\" > \"CreatedAt\"");
            table.HasCheckConstraint("CK_ClaimToken_Updated", "\"UpdatedAt\" >= \"CreatedAt\"");
            table.HasCheckConstraint(
                "CK_ClaimToken_Consumed",
                "\"ConsumedAt\" IS NULL OR (\"DatasetDownloadedAt\" IS NOT NULL AND \"ConsumedAt\" >= \"DatasetDownloadedAt\")");
            table.HasCheckConstraint(
                "CK_ClaimToken_Snapshot",
                "(\"DatasetDownloadedAt\" IS NULL AND \"ExpectedRecordCount\" IS NULL AND \"ExpectedTypeCounts\" IS NULL AND \"ExpectedManifestHash\" IS NULL AND \"ExpectedSourceContentHash\" IS NULL) OR (\"DatasetDownloadedAt\" IS NOT NULL AND \"ExpectedRecordCount\" >= 0 AND \"ExpectedTypeCounts\" IS NOT NULL AND \"ExpectedManifestHash\" IS NOT NULL AND \"ExpectedSourceContentHash\" IS NOT NULL)");
        });
    }
}

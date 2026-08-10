using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Domain.Entities;

namespace Xpense.Persistence.TypeConfiguration;

public class PendingPasskeyAssertionEntityTypeConfiguration : IEntityTypeConfiguration<PendingPasskeyAssertion>
{
    private const string XpenseSchema = "Xpense";

    public void Configure(EntityTypeBuilder<PendingPasskeyAssertion> builder)
    {
        builder.ToTable("PendingPasskeyAssertions", XpenseSchema);
        builder.HasKey(assertion => assertion.Id);
        builder.Property(assertion => assertion.AssertionState).HasMaxLength(16384).IsRequired();
        builder.Property(assertion => assertion.NormalizedEmail).HasMaxLength(256);
    }
}

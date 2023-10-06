using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.API.Data.Models;

namespace Xpense.API.Data.TypeConfiguration;

public class CurrencyExchangeRateAuditEntityTypeConfiguration : BaseEntityTypeConfiguration<CurrencyExchangeRateAudit>
{
    public override void Configure(EntityTypeBuilder<CurrencyExchangeRateAudit> builder)
    {
        base.Configure(builder);
        builder.Metadata.SetSchema(CurrencySchema);

        // CurrencyExchangeRateAudit (M) - CurrencyExchangeRate (1)
        builder.HasOne(e => e.ExchangeRate).WithMany(e => e.Audits).HasForeignKey(e => e.ExchangeRateId);

        // CurrencyExchangeRate (M) - Currency (1)
        builder.HasOne(e => e.PrincipalCurrency).WithMany(e => e.PrincipalExchangeRateAudits).HasForeignKey(e => e.PrincipalCurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ForeignCurrency).WithMany(e => e.ForeignCurrencyExchangeRateAudits).HasForeignKey(e => e.ForeignCurrencyId).OnDelete(DeleteBehavior.Restrict);

    }
}
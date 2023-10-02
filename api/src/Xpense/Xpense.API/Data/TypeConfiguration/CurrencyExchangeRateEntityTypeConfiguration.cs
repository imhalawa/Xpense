using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.API.Data.Models;

namespace Xpense.API.Data.TypeConfiguration
{
    public class CurrencyExchangeRateEntityTypeConfiguration:BaseEntityTypeConfiguration<CurrencyExchangeRate>
    {
        public override void Configure(EntityTypeBuilder<CurrencyExchangeRate> builder)
        {
            base.Configure(builder);
            builder.Metadata.SetSchema(CurrencySchema);

            // CurrencyExchangeRate (M) - Currency (1)
            builder.HasOne(e => e.PrincipalCurrency).WithMany(e => e.PrincipalExchangeRates).HasForeignKey(e=>e.PrincipalCurrencyId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(e => e.ForeignCurrency).WithMany(e => e.ForeignCurrencyExchangeRates).HasForeignKey(e=>e.ForeignCurrencyId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.API.Data.Models;

namespace Xpense.API.Data.TypeConfiguration
{
    public class CurrencyEntityTypeConfiguration : BaseEntityTypeConfiguration<Currency>
    {
        public override void Configure(EntityTypeBuilder<Currency> builder)
        {
            base.Configure(builder);
            builder.Metadata.SetSchema(CurrencySchema);

            builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
            builder.Property(e => e.IsoCode).HasMaxLength(3).IsRequired();
            
            // IsoCode Index
            builder.HasIndex(e => e.IsoCode).IsUnique();
        }
    }
}

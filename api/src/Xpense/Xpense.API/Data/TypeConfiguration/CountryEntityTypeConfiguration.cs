using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.API.Data.Models;

namespace Xpense.API.Data.TypeConfiguration
{
    public class CountryEntityTypeConfiguration: BaseEntityTypeConfiguration<Country>
    {
        public override void Configure(EntityTypeBuilder<Country> builder)
        {
            base.Configure(builder);
            builder.Metadata.SetSchema(XpenseSchema);

            builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
            builder.Property(e => e.DialCode).HasMaxLength(10).IsRequired();

            // Country Name Index
            builder.HasIndex(e => e.Name).IsUnique();
            
            // Country (M) - Currency (1)
            builder.HasOne(e => e.Currency).WithMany(e => e.Countries).HasForeignKey(e => e.CurrencyId);
;        }
    }
}

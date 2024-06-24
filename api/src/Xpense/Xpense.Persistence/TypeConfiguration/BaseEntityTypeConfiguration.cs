using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xpense.Services.Entities;

namespace Xpense.Persistence.TypeConfiguration
{
    public class BaseEntityTypeConfiguration<T> : IEntityTypeConfiguration<T> where T : BaseEntity
    {
        protected const string XpenseSchema = "Xpense";

        public virtual void Configure(EntityTypeBuilder<T> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.CreatedOn).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.LastUpdated);
        }
    }
}

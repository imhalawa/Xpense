using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Reflection;
using Xpense.API.Data.Models;

namespace Xpense.API.Data
{
    public class XpenseDbContext : DbContext
    {
        public XpenseDbContext(DbContextOptions<XpenseDbContext> options) : base(options)
        {
        }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<CurrencyExchangeRateAudit> CurrencyExchangeRateAudits { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<CategoryLevel> CategoryLevels { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            ConfigureDecimalColumnsStore(modelBuilder, 18, 2);
            base.OnModelCreating(modelBuilder);
        }

        private void ConfigureDecimalColumnsStore(ModelBuilder modelBuilder, int precision, int scale)
        {
            var decimalColumns = modelBuilder.Model
                .GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?));

            foreach (var decimalProperty in decimalColumns)
            {
                decimalProperty.SetPrecision(precision);
                decimalProperty.SetScale(scale);
            }
        }
    }
}

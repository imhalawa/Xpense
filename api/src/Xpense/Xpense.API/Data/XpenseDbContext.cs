using System.Data;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Data.Models;
using Xpense.API.Data.TypeConfiguration;
using Xpense.API.Enums;

namespace Xpense.API.Data
{
    public class XpenseDbContext : DbContext
    {
        public DbSet<Account> Accounts { get; set; }
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<CurrencyRateAudit> CurrencyAudits { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<CategoryLevel> CategoryLevels { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(BaseEntityTypeConfiguration).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}

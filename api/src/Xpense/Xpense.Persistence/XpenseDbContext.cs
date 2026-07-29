using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Linq.Expressions;
using System.Reflection;
using Xpense.Services.Abstract.Entities;
using Xpense.Services.Entities;

namespace Xpense.Persistence
{
    public class XpenseDbContext : DbContext
    {
        private readonly DbContextOptions<XpenseDbContext> options;

        public XpenseDbContext() { }

        public XpenseDbContext(DbContextOptions<XpenseDbContext> options) : base(options)
        {
            this.options = options;
        }

        public virtual DbSet<Account> Accounts { get; set; }
        public virtual DbSet<Merchant> Merchants { get; set; }
        public virtual DbSet<Transaction> Transactions { get; set; }
        public virtual DbSet<Transfer> Transfers { get; set; }
        public virtual DbSet<TransferLeg> TransferLegs { get; set; }
        public virtual DbSet<Category> Categories { get; set; }
        public virtual DbSet<Priority> Priorities { get; set; }
        public virtual DbSet<Tag> Tags { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            ConfigureDecimalColumnsStore(modelBuilder, 18, 2);
            ConfigureUtcDateTimes(modelBuilder);
            ApplyGlobalQueryFilter(modelBuilder, s => !s.IsDeleted);
            base.OnModelCreating(modelBuilder);
        }

        /// <summary>
        /// Relational date columns carry no offset, so EF hands values back with
        /// <see cref="DateTimeKind.Unspecified"/>. Everything is written in UTC, so tag reads as
        /// UTC too -- otherwise DateTimeOffset conversions in the response DTOs silently apply
        /// the server's local offset. Also required by Npgsql, which rejects Local kinds.
        /// </summary>
        private static void ConfigureUtcDateTimes(ModelBuilder modelBuilder)
        {
            var utc = new ValueConverter<DateTime, DateTime>(
                value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
                value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

            var nullableUtc = new ValueConverter<DateTime?, DateTime?>(
                value => value.HasValue && value.Value.Kind == DateTimeKind.Local
                    ? value.Value.ToUniversalTime()
                    : value,
                value => value.HasValue
                    ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
                    : value);

            var dateProperties = modelBuilder.Model
                .GetEntityTypes()
                .SelectMany(entity => entity.GetProperties());

            foreach (var property in dateProperties)
            {
                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(utc);
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(nullableUtc);
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
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

        private void ApplyGlobalQueryFilter(ModelBuilder builder, Expression<Func<BaseEntity, bool>> predicate)
        {
            foreach (var mutableEntityType in builder.Model.GetEntityTypes())
            {
                if (mutableEntityType.ClrType.IsAssignableTo(typeof(BaseEntity)))
                {
                    var parameter = Expression.Parameter(mutableEntityType.ClrType);
                    var body = ReplacingExpressionVisitor.Replace(predicate.Parameters.First(), parameter, predicate.Body);
                    var lambdaExpression = Expression.Lambda(body, parameter);
                    mutableEntityType.SetQueryFilter(lambdaExpression);
                }
            }
        }
    }
}

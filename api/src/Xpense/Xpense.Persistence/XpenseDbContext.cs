using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Linq.Expressions;
using System.Reflection;
using Xpense.Domain.Entities;

namespace Xpense.Persistence
{
    public class XpenseDbContext : DbContext
    {
        public XpenseDbContext() { }

        public XpenseDbContext(DbContextOptions<XpenseDbContext> options) : base(options) { }

        public virtual DbSet<Account> Accounts { get; set; }
        public virtual DbSet<Merchant> Merchants { get; set; }
        public virtual DbSet<Transaction> Transactions { get; set; }
        public virtual DbSet<Category> Categories { get; set; }
        public virtual DbSet<Priority> Priorities { get; set; }
        public virtual DbSet<Tag> Tags { get; set; }
        public virtual DbSet<Budget> Budgets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            ConfigureDecimalColumnsStore(modelBuilder, 18, 2);
            ConfigureUtcDateTimes(modelBuilder);
            ApplyGlobalQueryFilter(modelBuilder, entity => !entity.IsDeleted);
            base.OnModelCreating(modelBuilder);
        }

        private static void ConfigureUtcDateTimes(ModelBuilder modelBuilder)
        {
            var utc = new ValueConverter<DateTime, DateTime>(
                value => value.Kind == DateTimeKind.Utc
                    ? value
                    : value.Kind == DateTimeKind.Local
                        ? value.ToUniversalTime()
                        : DateTime.SpecifyKind(value, DateTimeKind.Utc),
                value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

            var nullableUtc = new ValueConverter<DateTime?, DateTime?>(
                value => !value.HasValue
                    ? value
                    : value.Value.Kind == DateTimeKind.Utc
                        ? value
                        : value.Value.Kind == DateTimeKind.Local
                            ? value.Value.ToUniversalTime()
                            : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
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

        private void ConfigureDecimalColumnsStore(ModelBuilder modelBuilder, int precision, int scale)
        {
            var decimalColumns = modelBuilder.Model
                .GetEntityTypes()
                .SelectMany(entityType => entityType.GetProperties())
                .Where(property => property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?));

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

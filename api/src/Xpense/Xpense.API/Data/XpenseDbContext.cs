using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Xpense.API.Data.Models;

namespace Xpense.API.Data
{
    public class XpenseDbContext : DbContext
    {
        public XpenseDbContext() { }

        public XpenseDbContext(DbContextOptions<XpenseDbContext> options) : base(options)
        {
        }
        public virtual DbSet<Account> Accounts { get; set; }
        public virtual DbSet<Transaction> Transactions { get; set; }
        public virtual DbSet<Category> Categories { get; set; }
        public virtual DbSet<Tag> Tags { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            ConfigureDecimalColumnsStore(modelBuilder, 18, 2);
            ApplyGlobalQueryFilter(modelBuilder, s => !s.IsDeleted);
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

        private void ApplyGlobalQueryFilter (ModelBuilder builder, Expression<Func<BaseEntity, bool>> predicate)
        {
           foreach(var mutableEntityType in builder.Model.GetEntityTypes())
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

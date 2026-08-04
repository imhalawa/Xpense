using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Xpense.Persistence;

/// <summary>
/// Used by `dotnet ef` at design time. The connection string only needs to be well-formed for
/// migration scaffolding; it is not opened unless you actually run `database update`.
/// </summary>
public class XpenseContextFactory : IDesignTimeDbContextFactory<XpenseDbContext>
{
    public XpenseDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<XpenseDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=xpense;Username=xpense;Password=xpense");

        return new XpenseDbContext(optionsBuilder.Options);
    }
}

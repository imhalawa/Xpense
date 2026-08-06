using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Xpense.Persistence;

public class XpenseContextFactory : IDesignTimeDbContextFactory<XpenseDbContext>
{
    public XpenseDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<XpenseDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=xpense;Username=xpense;Password=xpense");

        return new XpenseDbContext(optionsBuilder.Options);
    }
}

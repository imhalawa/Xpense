using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Xpense.Persistence;

public class XpenseContextFactory : IDesignTimeDbContextFactory<XpenseDbContext>
{
    public XpenseDbContext CreateDbContext(string[] args)
    {
  var optionsBuilder = new DbContextOptionsBuilder<XpenseDbContext>();
        optionsBuilder.UseSqlServer("Server=atom;Database=Xpense;User Id=sa;Password=P@ssw0rd;TrustServerCertificate=true");

        return new XpenseDbContext(optionsBuilder.Options);    
        }
}

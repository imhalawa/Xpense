using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xpense.Persistence;

namespace Xpense.Tests.Infrastructure;

public sealed class WebApiTestFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            RemoveProductionDbContext(services);
            connection.Open();
            services.AddDbContext<XpenseDbContext>(options => options.UseSqlite(connection));
        });
    }

    /// <summary>
    /// AddDbContext registers more than DbContextOptions&lt;T&gt;: it also registers the
    /// non-generic DbContextOptions and, from EF 9 onwards, IDbContextOptionsConfiguration&lt;T&gt;.
    /// Leaving any of them behind means both SqlServer and Sqlite stay registered, which EF 10
    /// rejects outright ("Only a single database provider can be registered").
    /// </summary>
    private static void RemoveProductionDbContext(IServiceCollection services)
    {
        var doomed = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(XpenseDbContext) ||
                descriptor.ServiceType == typeof(DbContextOptions) ||
                descriptor.ServiceType == typeof(DbContextOptions<XpenseDbContext>) ||
                (descriptor.ServiceType.IsGenericType &&
                 descriptor.ServiceType.GetGenericArguments().Contains(typeof(XpenseDbContext))))
            .ToList();

        foreach (var descriptor in doomed)
            services.Remove(descriptor);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<XpenseDbContext>().Database.EnsureCreated();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            connection.Dispose();

        base.Dispose(disposing);
    }
}

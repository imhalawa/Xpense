using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xpense.Persistence;

namespace Xpense.Tests.Infrastructure;

public sealed class WebApiTestFactory : WebApplicationFactory<Program>
{
    private readonly string connectionString;
    private readonly IInterceptor[] interceptors;

    public WebApiTestFactory(string connectionString, params IInterceptor[] interceptors)
    {
        this.connectionString = connectionString;
        this.interceptors = interceptors;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            RemoveProductionDbContext(services);
            services.AddDbContext<XpenseDbContext>(options =>
                options.UseNpgsql(connectionString).AddInterceptors(interceptors));
        });
    }

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
}

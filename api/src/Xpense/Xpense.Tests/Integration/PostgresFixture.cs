using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xpense.Persistence;

namespace Xpense.Tests.Integration;

[SetUpFixture]
public sealed class PostgresFixture
{
    private const string TemplateDatabase = "xpense_template";

    private static PostgreSqlContainer container;
    private static int databaseCounter;

    [OneTimeSetUp]
    public async Task StartContainer()
    {
        container = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("postgres")
            .WithUsername("xpense")
            .WithPassword("xpense")
            .Build();

        await container.StartAsync();

        await ExecuteOnMaintenanceDatabase($"CREATE DATABASE {TemplateDatabase}");

        var options = new DbContextOptionsBuilder<XpenseDbContext>()
            .UseNpgsql(ConnectionStringFor(TemplateDatabase))
            .Options;

        await using (var context = new XpenseDbContext(options))
            await context.Database.MigrateAsync();

        NpgsqlConnection.ClearAllPools();
    }

    [OneTimeTearDown]
    public async Task StopContainer()
    {
        NpgsqlConnection.ClearAllPools();

        if (container is not null)
            await container.DisposeAsync();
    }

    public static async Task<string> CreateDatabase()
    {
        var name = $"xpense_test_{Interlocked.Increment(ref databaseCounter)}";
        await ExecuteOnMaintenanceDatabase($"CREATE DATABASE {name} TEMPLATE {TemplateDatabase}");
        return ConnectionStringFor(name);
    }

    private static string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = database
        }.ConnectionString;

    private static async Task ExecuteOnMaintenanceDatabase(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionStringFor("postgres"));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

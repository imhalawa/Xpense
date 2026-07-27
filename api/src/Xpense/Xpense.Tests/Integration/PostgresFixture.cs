using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xpense.Persistence;

namespace Xpense.Tests.Integration;

/// <summary>
/// One Postgres container for the whole integration run.
/// <para>
/// Integration tests used to run on SQLite in-memory, which is fast but is not the provider
/// that ships. Postgres enforces things SQLite does not -- most relevantly it rejects a
/// DateTime whose Kind is not Utc for a `timestamp with time zone` column, which is exactly the
/// class of bug the UTC work was about.
/// </para>
/// <para>
/// Per-test isolation comes from cloning a template database rather than migrating each time:
/// CREATE DATABASE ... TEMPLATE is a file copy and costs a few milliseconds, where re-running
/// migrations for every test would dominate the suite.
/// </para>
/// </summary>
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

        // Apply the real migrations once. If the Postgres migration is broken, the whole
        // integration suite fails here rather than in a confusing place later.
        var options = new DbContextOptionsBuilder<XpenseDbContext>()
            .UseNpgsql(ConnectionStringFor(TemplateDatabase))
            .Options;

        await using (var context = new XpenseDbContext(options))
            await context.Database.MigrateAsync();

        // A template cannot be cloned while anything is connected to it.
        NpgsqlConnection.ClearAllPools();
    }

    [OneTimeTearDown]
    public async Task StopContainer()
    {
        NpgsqlConnection.ClearAllPools();

        if (container is not null)
            await container.DisposeAsync();
    }

    /// <summary>Clones the migrated template and returns a connection string for the copy.</summary>
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

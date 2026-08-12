using FluentAssertions;
using Npgsql;

namespace Xpense.Tests.Integration;

[TestFixture]
public class EncryptedRecordSchemaTests
{
    private static readonly IReadOnlyDictionary<string, string[]> LegacyColumns =
        new Dictionary<string, string[]>
        {
            ["Accounts"] = ["Id", "AccountNumber", "BalanceMinorUnits", "CreatedAt", "Currency", "IsDefault", "IsDeleted", "Label", "UpdatedAt"],
            ["Budgets"] = ["Id", "AlertThresholdPercent", "AmountMinorUnits", "CategoryId", "CreatedAt", "Currency", "EndsOn", "IsDeleted", "Recurrence", "StartsOn", "UpdatedAt"],
            ["Categories"] = ["Id", "CreatedAt", "IsDeleted", "Label", "PriorityId", "UpdatedAt"],
            ["Merchants"] = ["Id", "CreatedAt", "IsDeleted", "Label", "UpdatedAt"],
            ["Notifications"] = ["Id", "CreatedAt", "EventId", "IsDeleted", "Kind", "Message", "OwnerId", "Payload", "PayloadHash", "ReadAt", "Title", "UpdatedAt"],
            ["Tags"] = ["Id", "BgColorHex", "CreatedAt", "FgColorHex", "IsDeleted", "Label", "UpdatedAt"],
            ["Transactions"] = ["Id", "AmountMinorUnits", "CategoryId", "CreatedAt", "Currency", "DestinationAccountId", "IsDeleted", "MerchantId", "OccurredAt", "Reason", "SourceAccountId", "UpdatedAt"]
        };

    private string connectionString = null!;

    [SetUp]
    public async Task SetUp() => connectionString = await PostgresFixture.CreateDatabase();

    [Test]
    public async Task The_sync_tables_exist()
    {
        var expected = new[] { "EncryptedRecords", "ResourceGrants", "SyncOperations" };
        var actual = await ReadStrings(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'Xpense'");

        actual.Should().Contain(expected);
    }

    [Test]
    public async Task The_envelope_and_claim_tables_are_gone()
    {
        var actual = await ReadStrings(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'Xpense'");

        actual.Should().NotContain("RecordEnvelopes");
        actual.Should().NotContain("ClaimTokens");
    }

    [Test]
    public async Task Idempotency_keys_are_unique()
    {
        var actual = await ReadStrings(
            "SELECT indexname FROM pg_indexes WHERE schemaname = 'Xpense'");

        actual.Should().Contain("IX_SyncOperations_UserId_IdempotencyKey");
        actual.Should().Contain("IX_ResourceGrants_GroupId_ResourceType_ResourceId");
    }

    [Test]
    public async Task Sequence_numbers_increase_across_inserts()
    {
        var userId = Guid.CreateVersion7();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await InsertUser(connection, userId);

        var first = await InsertEncryptedRecord(connection, userId);
        var second = await InsertEncryptedRecord(connection, userId);

        second.Should().BeGreaterThan(first);
    }

    [Test]
    public async Task The_expand_migration_preserves_every_legacy_column()
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var expected in LegacyColumns)
        {
            await using var command = new NpgsqlCommand(
                "SELECT column_name FROM information_schema.columns WHERE table_schema = 'Xpense' AND table_name = @tableName",
                connection);
            command.Parameters.AddWithValue("tableName", expected.Key);

            var actual = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                actual.Add(reader.GetString(0));

            actual.Should().Contain(expected.Value, $"{expected.Key} is protected by the expand-only migration rule");
        }
    }

    private async Task<List<string>> ReadStrings(string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();

        while (await reader.ReadAsync())
            values.Add(reader.GetString(0));

        return values;
    }

    private static async Task InsertUser(NpgsqlConnection connection, Guid userId)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "Xpense"."Users"
                ("Id", "State", "CreatedAt", "EmailConfirmed", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
            VALUES
                (@userId, 1, now(), false, false, false, false, 0)
            """,
            connection);
        command.Parameters.AddWithValue("userId", userId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> InsertEncryptedRecord(NpgsqlConnection connection, Guid userId)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "Xpense"."EncryptedRecords"
                ("Id", "RecordType", "OwnerUserId", "Revision", "Payload", "IsDeleted", "CreatedAt", "UpdatedAt")
            VALUES
                (@id, 0, @userId, 1, @payload, false, now(), now())
            RETURNING "SequenceNumber"
            """,
            connection);
        command.Parameters.AddWithValue("id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("payload", new byte[] { 1, 2, 3 });
        return (long)(await command.ExecuteScalarAsync())!;
    }
}

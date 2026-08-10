using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Persistence;
using Xpense.Tests.Infrastructure;

namespace Xpense.Tests.Integration;

[TestFixture]
[NonParallelizable]
public class SyncChangesTests
{
    private WebApiTestFactory factory = null!;
    private HttpClient client = null!;
    private Guid currentUserId;
    private Guid otherUserId;

    [SetUp]
    public async Task SetUp()
    {
        currentUserId = Guid.CreateVersion7();
        otherUserId = Guid.CreateVersion7();
        factory = new WebApiTestFactory(await PostgresFixture.CreateDatabase()).AsUser(currentUserId);
        client = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        dbContext.Users.AddRange(User(currentUserId), User(otherUserId));
        await dbContext.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Test]
    public async Task An_empty_cursor_returns_accessible_records_and_tombstones_only()
    {
        await AddRecord(currentUserId, false, DateTime.UtcNow.AddDays(-1));
        await AddRecord(currentUserId, true, DateTime.UtcNow);
        var hidden = await AddRecord(otherUserId, false, DateTime.UtcNow.AddDays(1));

        var response = await client.GetFromJsonAsync<ChangesResponse>("/api/v1/sync/changes");

        response.Should().NotBeNull();
        response!.Records.Should().HaveCount(2);
        response.Records.Should().Contain(record => record.IsDeleted);
        response.Records.Should().NotContain(record => record.Id == hidden.Id);
    }

    [Test]
    public async Task The_cursor_resumes_without_a_gap_or_repeat()
    {
        var first = await AddRecord(currentUserId, false, DateTime.UtcNow.AddDays(3));
        var second = await AddRecord(currentUserId, false, DateTime.UtcNow.AddDays(2));
        var third = await AddRecord(currentUserId, false, DateTime.UtcNow.AddDays(1));

        var firstPage = await client.GetFromJsonAsync<ChangesResponse>("/api/v1/sync/changes?pageSize=2");
        var secondPage = await client.GetFromJsonAsync<ChangesResponse>(
            $"/api/v1/sync/changes?pageSize=2&cursor={Uri.EscapeDataString(firstPage!.NextCursor)}");

        firstPage.Records.Select(record => record.Id).Should().Equal(first.Id, second.Id);
        firstPage.HasMore.Should().BeTrue();
        secondPage!.Records.Select(record => record.Id).Should().Equal(third.Id);
        secondPage.HasMore.Should().BeFalse();
        firstPage.Records.Select(record => record.Id)
            .Should().NotIntersectWith(secondPage.Records.Select(record => record.Id));
    }

    [Test]
    public async Task Changes_are_ordered_by_sequence_not_update_time()
    {
        var first = await AddRecord(currentUserId, false, DateTime.UtcNow.AddDays(10));
        var second = await AddRecord(currentUserId, false, DateTime.UtcNow.AddDays(-10));

        var response = await client.GetFromJsonAsync<ChangesResponse>("/api/v1/sync/changes");

        response!.Records.Select(record => record.Id).Should().Equal(first.Id, second.Id);
    }

    [Test]
    public async Task A_garbage_cursor_returns_bad_request()
    {
        var response = await client.GetAsync("/api/v1/sync/changes?cursor=not-a-cursor");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("cursor");
    }

    private async Task<EncryptedRecord> AddRecord(Guid ownerUserId, bool isDeleted, DateTime updatedAt)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var record = new EncryptedRecord
        {
            Id = Guid.CreateVersion7(),
            RecordType = EncryptedRecordType.Account,
            OwnerUserId = ownerUserId,
            Revision = 1,
            ProtocolVersion = 1,
            Nonce = [1, 2, 3],
            Ciphertext = [4, 5, 6],
            IsDeleted = isDeleted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = updatedAt
        };
        dbContext.EncryptedRecords.Add(record);
        await dbContext.SaveChangesAsync();
        return record;
    }

    private static XpenseUser User(Guid id) => new()
    {
        Id = id,
        State = AccountState.Active,
        CreatedAt = DateTime.UtcNow
    };

    private sealed record ChangesResponse(ChangeRecord[] Records, string NextCursor, bool HasMore);

    private sealed record ChangeRecord(Guid Id, bool IsDeleted);
}

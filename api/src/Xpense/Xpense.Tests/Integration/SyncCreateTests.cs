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
public class SyncCreateTests
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
    public async Task A_batch_create_returns_an_absolute_location()
    {
        var response = await client.PostAsJsonAsync("/api/v1/sync/records", new CreateRequest([ValidRecord()]));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.IsAbsoluteUri.Should().BeTrue();
        (await response.Content.ReadFromJsonAsync<CreateResponse>())!.Records.Should().HaveCount(1);
    }

    [Test]
    public async Task A_repeated_idempotency_key_returns_the_original_record()
    {
        var item = ValidRecord();

        var first = await client.PostAsJsonAsync("/api/v1/sync/records", new CreateRequest([item]));
        var second = await client.PostAsJsonAsync("/api/v1/sync/records", new CreateRequest([item with { Id = Guid.CreateVersion7() }]));
        var firstBody = await first.Content.ReadFromJsonAsync<CreateResponse>();
        var secondBody = await second.Content.ReadFromJsonAsync<CreateResponse>();

        second.StatusCode.Should().Be(HttpStatusCode.Created);
        secondBody!.Records.Single().Id.Should().Be(firstBody!.Records.Single().Id);
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<XpenseDbContext>().EncryptedRecords.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task One_invalid_item_rejects_the_whole_batch()
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/sync/records",
            new CreateRequest([ValidRecord(), ValidRecord() with { ProtocolVersion = 0 }]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<XpenseDbContext>().EncryptedRecords.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task An_unknown_record_type_is_rejected()
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/sync/records",
            new CreateRequest([ValidRecord() with { RecordType = 999 }]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Oversized_ciphertext_is_rejected()
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/sync/records",
            new CreateRequest([ValidRecord() with { Ciphertext = new byte[65537] }]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task A_parent_resource_owned_by_someone_else_is_hidden()
    {
        var resourceId = Guid.CreateVersion7();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
            dbContext.SharedResources.Add(new SharedResource
            {
                Id = resourceId,
                Type = SharedResourceType.Account,
                OwnerUserId = otherUserId,
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            "/api/v1/sync/records",
            new CreateRequest([ValidRecord() with { ParentResourceId = resourceId }]));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task A_created_record_advances_the_global_sequence()
    {
        var existing = await AddExistingRecord();

        var response = await client.PostAsJsonAsync("/api/v1/sync/records", new CreateRequest([ValidRecord()]));
        var created = (await response.Content.ReadFromJsonAsync<CreateResponse>())!.Records.Single();

        created.SequenceNumber.Should().BeGreaterThan(existing.SequenceNumber);
    }

    private async Task<EncryptedRecord> AddExistingRecord()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var record = new EncryptedRecord
        {
            Id = Guid.CreateVersion7(),
            RecordType = EncryptedRecordType.Account,
            OwnerUserId = currentUserId,
            Revision = 1,
            ProtocolVersion = 1,
            Nonce = [1],
            Ciphertext = [2],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.EncryptedRecords.Add(record);
        await dbContext.SaveChangesAsync();
        return record;
    }

    private static CreateItem ValidRecord() => new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7().ToString(),
        (int)EncryptedRecordType.Account,
        null,
        1,
        [1, 2, 3],
        [4, 5, 6],
        new EnvelopeItem([7, 8, 9], [10, 11, 12], 1));

    private static XpenseUser User(Guid id) => new()
    {
        Id = id,
        State = AccountState.Active,
        CreatedAt = DateTime.UtcNow
    };

    private sealed record CreateRequest(CreateItem[] Records);

    private sealed record CreateItem(
        Guid Id,
        string IdempotencyKey,
        int RecordType,
        Guid? ParentResourceId,
        int ProtocolVersion,
        byte[] Nonce,
        byte[] Ciphertext,
        EnvelopeItem PersonalEnvelope);

    private sealed record EnvelopeItem(byte[] WrappedKey, byte[] Nonce, int ProtocolVersion);

    private sealed record CreateResponse(CreatedRecord[] Records);

    private sealed record CreatedRecord(Guid Id, long SequenceNumber);
}

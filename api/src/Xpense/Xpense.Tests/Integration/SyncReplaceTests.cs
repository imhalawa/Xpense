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
public class SyncReplaceTests
{
    private WebApiTestFactory factory = null!;
    private HttpClient client = null!;
    private Guid currentUserId;

    [SetUp]
    public async Task SetUp()
    {
        currentUserId = Guid.CreateVersion7();
        factory = new WebApiTestFactory(await PostgresFixture.CreateDatabase()).AsUser(currentUserId);
        client = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        dbContext.Users.Add(User(currentUserId));
        await dbContext.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Test]
    public async Task A_matching_revision_replaces_ciphertext_and_advances_revision_and_sequence()
    {
        var record = await AddRecord(currentUserId);
        var request = new ReplaceRequest(1, 1, [7, 8, 9], [10, 11, 12]);

        var response = await client.PutAsJsonAsync($"/api/v1/sync/records/{record.Id}", request);
        var body = await response.Content.ReadFromJsonAsync<RecordResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Revision.Should().Be(2);
        body.SequenceNumber.Should().BeGreaterThan(record.SequenceNumber);
        body.Ciphertext.Should().Equal(request.Ciphertext);
    }

    [Test]
    public async Task A_stale_revision_returns_conflict_with_the_latest_ciphertext()
    {
        var record = await AddRecord(currentUserId);
        var firstCiphertext = new byte[] { 9, 9, 9 };
        await client.PutAsJsonAsync(
            $"/api/v1/sync/records/{record.Id}",
            new ReplaceRequest(1, 1, [8], firstCiphertext));

        var response = await client.PutAsJsonAsync(
            $"/api/v1/sync/records/{record.Id}",
            new ReplaceRequest(1, 1, [7], [6]));
        var body = await response.Content.ReadFromJsonAsync<RecordResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        body!.Revision.Should().Be(2);
        body.Ciphertext.Should().Equal(firstCiphertext);
    }

    [Test]
    public async Task Concurrent_updates_let_exactly_one_writer_win()
    {
        var record = await AddRecord(currentUserId);

        var first = client.PutAsJsonAsync(
            $"/api/v1/sync/records/{record.Id}",
            new ReplaceRequest(1, 1, [1], [2]));
        var second = client.PutAsJsonAsync(
            $"/api/v1/sync/records/{record.Id}",
            new ReplaceRequest(1, 1, [3], [4]));
        var responses = await Task.WhenAll(first, second);

        responses.Select(response => response.StatusCode)
            .Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
    }

    [Test]
    public async Task A_viewer_receives_not_found()
    {
        var ownerId = Guid.CreateVersion7();
        var resourceId = Guid.CreateVersion7();
        var groupId = Guid.CreateVersion7();
        await SeedViewer(ownerId, resourceId, groupId);
        var record = await AddRecord(ownerId, resourceId);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/sync/records/{record.Id}",
            new ReplaceRequest(1, 1, [7], [8]));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Replacement_does_not_change_record_type_or_owner()
    {
        var record = await AddRecord(currentUserId);

        await client.PutAsJsonAsync(
            $"/api/v1/sync/records/{record.Id}",
            new ReplaceRequest(1, 1, [7], [8]));

        await using var scope = factory.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .EncryptedRecords.AsNoTracking().SingleAsync(item => item.Id == record.Id);
        stored.OwnerUserId.Should().Be(currentUserId);
        stored.RecordType.Should().Be(EncryptedRecordType.Account);
    }

    private async Task SeedViewer(Guid ownerId, Guid resourceId, Guid groupId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var now = DateTime.UtcNow;
        dbContext.Users.Add(User(ownerId));
        dbContext.Groups.Add(new Xpense.Domain.Entities.Group
        {
            Id = groupId,
            OwnerUserId = ownerId,
            NameCiphertext = [1],
            NameNonce = [2],
            ProtocolVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.GroupMemberships.Add(new GroupMembership
        {
            Id = Guid.CreateVersion7(),
            GroupId = groupId,
            UserId = currentUserId,
            Role = MembershipRole.Member,
            State = MembershipState.Active,
            EnvelopeProtocolVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.SharedResources.Add(new SharedResource
        {
            Id = resourceId,
            Type = SharedResourceType.Account,
            OwnerUserId = ownerId,
            CreatedAt = now
        });
        dbContext.ResourceGrants.Add(new ResourceGrant
        {
            Id = Guid.CreateVersion7(),
            GroupId = groupId,
            ResourceType = SharedResourceType.Account,
            ResourceId = resourceId,
            Permission = GrantPermission.Viewer,
            State = GrantState.Active,
            GrantedByUserId = ownerId,
            CreatedAt = now,
            UpdatedAt = now
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task<EncryptedRecord> AddRecord(Guid ownerUserId, Guid? parentResourceId = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var record = new EncryptedRecord
        {
            Id = Guid.CreateVersion7(),
            RecordType = EncryptedRecordType.Account,
            OwnerUserId = ownerUserId,
            ParentResourceId = parentResourceId,
            Revision = 1,
            ProtocolVersion = 1,
            Nonce = [1, 2, 3],
            Ciphertext = [4, 5, 6],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
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

    private sealed record ReplaceRequest(long ExpectedRevision, int ProtocolVersion, byte[] Nonce, byte[] Ciphertext);

    private sealed record RecordResponse(
        Guid Id,
        EncryptedRecordType RecordType,
        Guid OwnerUserId,
        long Revision,
        byte[] Ciphertext,
        long SequenceNumber);
}

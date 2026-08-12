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
public class SyncDeleteTests
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
    public async Task Delete_creates_a_tombstone_without_changing_revision()
    {
        var record = await AddRecord(currentUserId);

        var delete = await client.DeleteAsync($"/api/v1/sync/records/{record.Id}");
        var changes = await client.GetFromJsonAsync<ChangesResponse>("/api/v1/sync/changes");
        var tombstone = changes!.Records.Single(item => item.Id == record.Id);

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        tombstone.Tombstone.Should().BeTrue();
        tombstone.Revision.Should().Be(record.Revision);
        tombstone.SequenceNumber.Should().BeGreaterThan(record.SequenceNumber);
    }

    [Test]
    public async Task Deleting_twice_is_idempotent()
    {
        var record = await AddRecord(currentUserId);

        var first = await client.DeleteAsync($"/api/v1/sync/records/{record.Id}");
        var firstSequence = await SequenceNumber(record.Id);
        var second = await client.DeleteAsync($"/api/v1/sync/records/{record.Id}");

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SequenceNumber(record.Id)).Should().Be(firstSequence);
    }

    [TestCase(GrantPermission.Viewer, HttpStatusCode.NotFound)]
    [TestCase(GrantPermission.Editor, HttpStatusCode.NoContent)]
    public async Task Group_permission_controls_delete(GrantPermission permission, HttpStatusCode expected)
    {
        var ownerId = Guid.CreateVersion7();
        var resourceId = Guid.CreateVersion7();
        await SeedGrant(ownerId, resourceId, permission);
        var record = await AddRecord(ownerId, resourceId);

        var response = await client.DeleteAsync($"/api/v1/sync/records/{record.Id}");

        response.StatusCode.Should().Be(expected);
    }

    private async Task SeedGrant(Guid ownerId, Guid resourceId, GrantPermission permission)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var now = DateTime.UtcNow;
        var groupId = Guid.CreateVersion7();
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
            Permission = permission,
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
            Payload = [1, 2, 3],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.EncryptedRecords.Add(record);
        await dbContext.SaveChangesAsync();
        return record;
    }

    private async Task<long> SequenceNumber(Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .EncryptedRecords.Where(record => record.Id == id)
            .Select(record => record.SequenceNumber)
            .SingleAsync();
    }

    private static XpenseUser User(Guid id) => new()
    {
        Id = id,
        State = AccountState.Active,
        CreatedAt = DateTime.UtcNow
    };

    private sealed record ChangesResponse(ChangeRecord[] Records);

    private sealed record ChangeRecord(Guid Id, long Revision, bool Tombstone, long SequenceNumber);
}

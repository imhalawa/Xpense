using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Mono.Cecil;
using Xpense.API.Features.Sync;
using Xpense.API.Infrastructure;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.ValueObjects;
using Xpense.Persistence;
using Xpense.Tests.Infrastructure;

namespace Xpense.Tests.Integration
{

[TestFixture]
[NonParallelizable]
public class ServerBlindnessTests
{
    private const string PlaintextMarker = "7f4184ae-9e31-4b64-b5e8-b326885a9860";

    private static readonly Guid PlaintextMarkerGuid = Guid.Parse(PlaintextMarker);
    private static readonly byte[] TestEncryptionKey = Convert.FromHexString(
        "7BE510390AE9B484A447EAC4993196CA66E805C2C5841206F73E13CB4F37F7A2");
    private static readonly string[] PlaintextMarkerTextRepresentations =
    [
        PlaintextMarker,
        PlaintextMarkerGuid.ToString("N"),
        PlaintextMarkerGuid.ToString("B"),
        PlaintextMarkerGuid.ToString("P"),
        Convert.ToBase64String(Encoding.UTF8.GetBytes(PlaintextMarker)),
        Convert.ToBase64String(Encoding.Unicode.GetBytes(PlaintextMarker))
    ];
    private static readonly byte[][] PlaintextMarkerByteRepresentations =
    [
        Encoding.UTF8.GetBytes(PlaintextMarker),
        Encoding.Unicode.GetBytes(PlaintextMarker),
        Encoding.BigEndianUnicode.GetBytes(PlaintextMarker),
        Encoding.ASCII.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes(PlaintextMarker))),
        Encoding.ASCII.GetBytes(Convert.ToBase64String(Encoding.Unicode.GetBytes(PlaintextMarker))),
        PlaintextMarkerGuid.ToByteArray()
    ];
    private static readonly Type[] PlaintextFinancialRoots =
    [
        typeof(Account),
        typeof(Transaction),
        typeof(Category),
        typeof(Budget),
        typeof(Merchant),
        typeof(Tag),
        typeof(Priority)
    ];
    private static readonly IReadOnlySet<string> PlaintextFinancialTypeNames =
        DiscoverPlaintextFinancialTypeNames();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private WebApiTestFactory factory = null!;
    private HttpClient client = null!;
    private Guid currentUserId;
    private Guid currentUserGroupId;

    [SetUp]
    public async Task SetUp()
    {
        currentUserId = Guid.CreateVersion7();
        currentUserGroupId = Guid.CreateVersion7();
        factory = new WebApiTestFactory(await PostgresFixture.CreateDatabase()).AsUser(currentUserId);
        client = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var now = DateTime.UtcNow;
        dbContext.Users.Add(User(currentUserId, now));
        dbContext.Groups.Add(Group(currentUserGroupId, currentUserId, now));
        dbContext.GroupMemberships.Add(Membership(
            currentUserGroupId,
            currentUserId,
            MembershipRole.Owner,
            now));
        await dbContext.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Test]
    public async Task Literal_client_plaintext_is_absent_from_every_blind_sync_table_column()
    {
        var accountId = Guid.CreateVersion7();
        var transactionId = Guid.CreateVersion7();
        var create = new CreateRequest(
        [
            Record(accountId, EncryptedRecordType.Account, "account", accountId),
            Record(transactionId, EncryptedRecordType.Transaction, "transaction", accountId)
        ]);

        var createResponse = await client.PostAsJsonAsync("/api/v1/sync/records", create);
        var envelopeResponse = await client.PostAsJsonAsync(
            $"/api/v1/sync/records/{accountId}/envelopes",
            new AddEnvelopeRequest(
                currentUserGroupId,
                GrantPermission.Viewer,
                OpaqueBytes("group-envelope-key"),
                OpaqueBytes("group-envelope-nonce"),
                OpaqueBytes("group-envelope-encapsulated-key"),
                1));

        createResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        envelopeResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var storedValues = new List<StoredColumnValue>();
        storedValues.AddRange(await ReadMappedColumnValues<EncryptedRecord>(dbContext));
        storedValues.AddRange(await ReadMappedColumnValues<RecordEnvelope>(dbContext));
        storedValues.AddRange(await ReadMappedColumnValues<ResourceGrant>(dbContext));
        storedValues.AddRange(await ReadMappedColumnValues<SyncOperation>(dbContext));

        storedValues.GroupBy(value => value.EntityType).Should().HaveCount(4);
        storedValues.Should().NotContain(
            value => ContainsPlaintextMarker(value.Value),
            "the API may store only opaque ciphertext and authorization metadata");
    }

    [Test]
    public void Marker_detection_covers_text_Guid_UTF8_UTF16_and_base64_representations()
    {
        var canaries = new object[]
        {
            PlaintextMarker,
            PlaintextMarkerGuid,
            Encoding.UTF8.GetBytes(PlaintextMarker),
            Encoding.Unicode.GetBytes(PlaintextMarker),
            Encoding.BigEndianUnicode.GetBytes(PlaintextMarker),
            Convert.ToBase64String(Encoding.UTF8.GetBytes(PlaintextMarker)),
            Convert.ToBase64String(Encoding.Unicode.GetBytes(PlaintextMarker)),
            Encoding.ASCII.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes(PlaintextMarker))),
            PlaintextMarkerGuid.ToByteArray()
        };

        canaries.Should().OnlyContain(value => ContainsPlaintextMarker(value));
    }

    [Test]
    public void Sync_feature_types_do_not_inspect_ciphertext_or_depend_on_plaintext_or_decryption_types()
    {
        using var module = ModuleDefinition.ReadModule(typeof(IEndpoint).Assembly.Location);
        var syncTypes = TypesInNamespace(module, typeof(GetSyncChanges).Namespace!).ToArray();
        var audit = Audit(syncTypes);
        var responseTypeName = typeof(EncryptedRecordResponse).FullName;

        audit.FinancialDependencies.Should().BeEmpty(
            "every type in the sync feature operates on opaque records rather than the plaintext financial graph");
        audit.DecryptionDependencies.Should().BeEmpty(
            "the sync feature has no server-side decryption or key-unwrapping operation");
        audit.PersistedCiphertextReaders.Should().ContainSingle();
        audit.PersistedCiphertextReaders[0].DeclaringType.FullName.Should().Be(
            responseTypeName,
            "persisted ciphertext may only be copied by the response contract mapper");
    }

    [Test]
    public void Compiled_IL_audit_detects_top_level_financial_and_decryption_dependencies_but_allows_hashing()
    {
        using var module = ModuleDefinition.ReadModule(typeof(ServerBlindnessTests).Assembly.Location);
        var testSyncTypes = TypesInNamespace(module, typeof(GetSyncChanges).Namespace!).ToArray();
        var forbiddenHelper = testSyncTypes.Single(
            type => type.Name == nameof(ServerBlindnessTopLevelForbiddenHelper));
        var allowedHashHelper = testSyncTypes.Single(
            type => type.Name == nameof(ServerBlindnessTopLevelHashHelper));
        var asyncHelper = testSyncTypes.Single(
            type => type.Name == nameof(ServerBlindnessTopLevelAsyncHelper));
        var generatedAsyncTypes = testSyncTypes
            .Where(type => IsNestedWithin(type, asyncHelper))
            .Where(type => type.Interfaces.Any(
                @interface => @interface.InterfaceType.FullName == typeof(IAsyncStateMachine).FullName))
            .ToArray();

        var forbiddenAudit = Audit(testSyncTypes);
        var allowedHashAudit = Audit([allowedHashHelper]);
        var generatedAsyncAudit = Audit(generatedAsyncTypes);

        forbiddenHelper.DeclaringType.Should().BeNull("the canary represents a separate top-level sync helper");
        generatedAsyncTypes.Should().NotBeEmpty(
            "namespace traversal must reach compiler-generated state machines nested under helper types");
        generatedAsyncAudit.FinancialDependencies.Should().Contain(typeof(BudgetPeriod).FullName);
        forbiddenAudit.FinancialDependencies.Should().Contain(
            [typeof(Account).FullName, typeof(Money).FullName, typeof(BudgetPeriod).FullName]);
        forbiddenAudit.DecryptionDependencies.Should().Contain(
            dependency => dependency.Contains($"{nameof(AesGcm)}::{nameof(AesGcm.Decrypt)}"));
        forbiddenAudit.DecryptionDependencies.Should().Contain(
            dependency => dependency.Contains("::Unprotect("));
        forbiddenAudit.DecryptionDependencies.Should().Contain(
            dependency => dependency.Contains("::Unwrap("));
        allowedHashAudit.DecryptionDependencies.Should().BeEmpty(
            "hashing does not decrypt or unwrap key material");
    }

    [Test]
    public void Encrypted_records_have_no_navigation_field_or_generic_reference_to_the_plaintext_graph()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var entityType = dbContext.Model.FindEntityType(typeof(EncryptedRecord));
        var modelNavigations = entityType!.GetNavigations()
            .Select(navigation => navigation.TargetEntityType.ClrType)
            .Concat(entityType.GetSkipNavigations().Select(navigation => navigation.TargetEntityType.ClrType))
            .Where(ContainsPlaintextFinancialType)
            .Select(type => type.FullName)
            .ToArray();
        var memberTypes = InstanceFields(typeof(EncryptedRecord))
            .Select(field => field.FieldType)
            .Concat(InstanceProperties(typeof(EncryptedRecord)).Select(property => property.PropertyType))
            .Where(ContainsPlaintextFinancialType)
            .Select(type => type.FullName)
            .ToArray();

        modelNavigations.Should().BeEmpty("EF must not connect the encrypted and plaintext graphs");
        memberTypes.Should().BeEmpty(
            "public, nonpublic, collection, and generic members must not connect the two graphs");
    }

    [Test]
    public async Task Sync_changes_returns_only_opaque_bytes_authorized_for_the_current_user()
    {
        var otherUserId = Guid.CreateVersion7();
        var membershipOnlyGroupId = Guid.CreateVersion7();
        var otherGroupId = Guid.CreateVersion7();
        var membershipOnlyResourceId = Guid.CreateVersion7();
        var otherResourceId = Guid.CreateVersion7();
        var ownerRecord = RecordFixture.Create(currentUserId, null, "owner-record");
        var membershipOnlyRecord = RecordFixture.Create(
            otherUserId,
            membershipOnlyResourceId,
            "membership-only-record");
        var privateOtherRecord = RecordFixture.Create(otherUserId, null, "private-other-record");
        var crossGroupOtherRecord = RecordFixture.Create(
            otherUserId,
            otherResourceId,
            "cross-group-other-record");
        var ownerEnvelope = EnvelopeFixture.Create(ownerRecord.Id, null, "owner-envelope");
        var foreignEnvelopes = new[]
        {
            EnvelopeFixture.Create(membershipOnlyRecord.Id, null, "membership-only-owner-envelope"),
            EnvelopeFixture.Create(
                membershipOnlyRecord.Id,
                membershipOnlyGroupId,
                "membership-only-group-envelope"),
            EnvelopeFixture.Create(privateOtherRecord.Id, null, "private-other-envelope"),
            EnvelopeFixture.Create(crossGroupOtherRecord.Id, null, "cross-group-owner-envelope"),
            EnvelopeFixture.Create(crossGroupOtherRecord.Id, otherGroupId, "cross-group-envelope")
        };

        await SeedVisibilityFixture(
            otherUserId,
            membershipOnlyGroupId,
            otherGroupId,
            membershipOnlyResourceId,
            otherResourceId,
            ownerRecord,
            membershipOnlyRecord,
            privateOtherRecord,
            crossGroupOtherRecord,
            ownerEnvelope,
            foreignEnvelopes);

        var response = await client.GetAsync("/api/v1/sync/changes?pageSize=200");
        var body = await response.Content.ReadAsStringAsync();
        var changes = JsonSerializer.Deserialize<ChangesResponse>(body, JsonOptions);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        changes.Should().NotBeNull();
        changes!.Records.Select(record => record.Id).Should().ContainSingle().Which.Should().Be(ownerRecord.Id);
        var ownerResponse = changes.Records.Single();
        ownerResponse.Nonce.Should().Equal(ownerRecord.Nonce);
        ownerResponse.Ciphertext.Should().Equal(ownerRecord.Ciphertext);
        ownerResponse.Envelopes.Should().ContainSingle();
        ownerResponse.Envelopes[0].WrappedKey.Should().Equal(ownerEnvelope.WrappedKey);
        ownerResponse.Envelopes[0].Nonce.Should().Equal(ownerEnvelope.Nonce);
        ownerResponse.Envelopes[0].EncapsulatedKey.Should().Equal(ownerEnvelope.EncapsulatedKey!);

        var foreignRecords = new[] { membershipOnlyRecord, privateOtherRecord, crossGroupOtherRecord };
        var forbiddenTextValues = foreignRecords
            .SelectMany(record => new[]
            {
                record.Id.ToString(),
                Convert.ToBase64String(record.Nonce),
                Convert.ToBase64String(record.Ciphertext)
            })
            .Concat(foreignEnvelopes.SelectMany(envelope => new[]
            {
                Convert.ToBase64String(envelope.WrappedKey),
                Convert.ToBase64String(envelope.Nonce),
                Convert.ToBase64String(envelope.EncapsulatedKey!)
            }));

        foreach (var forbidden in forbiddenTextValues)
            body.Should().NotContain(forbidden);
    }

    private async Task SeedVisibilityFixture(
        Guid otherUserId,
        Guid membershipOnlyGroupId,
        Guid otherGroupId,
        Guid membershipOnlyResourceId,
        Guid otherResourceId,
        EncryptedRecord ownerRecord,
        EncryptedRecord membershipOnlyRecord,
        EncryptedRecord privateOtherRecord,
        EncryptedRecord crossGroupOtherRecord,
        RecordEnvelope ownerEnvelope,
        RecordEnvelope[] foreignEnvelopes)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var now = DateTime.UtcNow;
        dbContext.Users.Add(User(otherUserId, now));
        dbContext.Groups.AddRange(
            Group(membershipOnlyGroupId, otherUserId, now),
            Group(otherGroupId, otherUserId, now));
        dbContext.GroupMemberships.AddRange(
            Membership(membershipOnlyGroupId, currentUserId, MembershipRole.Member, now),
            Membership(membershipOnlyGroupId, otherUserId, MembershipRole.Owner, now),
            Membership(otherGroupId, otherUserId, MembershipRole.Owner, now));
        dbContext.SharedResources.AddRange(
            Resource(membershipOnlyResourceId, otherUserId, now),
            Resource(otherResourceId, otherUserId, now));
        dbContext.ResourceGrants.Add(Grant(otherGroupId, otherResourceId, otherUserId, now));
        dbContext.EncryptedRecords.AddRange(
            ownerRecord,
            membershipOnlyRecord,
            privateOtherRecord,
            crossGroupOtherRecord);
        dbContext.RecordEnvelopes.Add(ownerEnvelope);
        dbContext.RecordEnvelopes.AddRange(foreignEnvelopes);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<StoredColumnValue[]> ReadMappedColumnValues<TEntity>(XpenseDbContext dbContext)
        where TEntity : class
    {
        var entityType = dbContext.Model.FindEntityType(typeof(TEntity))!;
        var rows = await dbContext.Set<TEntity>().AsNoTracking().ToArrayAsync();

        return rows.SelectMany(row => entityType.GetProperties().Select(property => new StoredColumnValue(
                typeof(TEntity).Name,
                property.GetColumnName(StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema())),
                ReadPropertyValue(property, row))))
            .ToArray();
    }

    private static object? ReadPropertyValue(IProperty property, object row) =>
        property.PropertyInfo?.GetValue(row) ?? property.FieldInfo?.GetValue(row);

    private static bool ContainsPlaintextMarker(object? value) => value switch
    {
        string text => PlaintextMarkerTextRepresentations.Any(
            representation => text.Contains(representation, StringComparison.OrdinalIgnoreCase)),
        Guid guid => guid == PlaintextMarkerGuid,
        byte[] bytes => PlaintextMarkerByteRepresentations.Any(representation => Contains(bytes, representation)),
        _ => false
    };

    private static bool Contains(byte[] bytes, byte[] candidate)
    {
        if (candidate.Length == 0 || candidate.Length > bytes.Length)
            return false;

        for (var index = 0; index <= bytes.Length - candidate.Length; index++)
        {
            if (bytes.AsSpan(index, candidate.Length).SequenceEqual(candidate))
                return true;
        }

        return false;
    }

    private static IlAuditResult Audit(IEnumerable<TypeDefinition> types)
    {
        var auditedTypes = types.DistinctBy(type => type.FullName).ToArray();
        var typeReferences = auditedTypes.SelectMany(ReferencedTypes).ToArray();
        var financialDependencies = typeReferences
            .Select(type => type.FullName)
            .Where(PlaintextFinancialTypeNames.Contains)
            .Distinct()
            .ToArray();
        var decryptionDependencies = typeReferences
            .Where(IsDecryptionOrKeyUnwrappingType)
            .Select(type => type.FullName)
            .Concat(auditedTypes.SelectMany(ReferencedMethods)
                .Where(IsDecryptionOrKeyUnwrappingMethod)
                .Select(method => method.FullName))
            .Distinct()
            .ToArray();

        return new IlAuditResult(
            financialDependencies,
            decryptionDependencies,
            auditedTypes.SelectMany(PersistedCiphertextReaders).ToArray());
    }

    private static IEnumerable<TypeDefinition> TypesInNamespace(ModuleDefinition module, string namespaceName) =>
        module.Types
            .Where(type => type.Namespace == namespaceName)
            .SelectMany(SelfAndNestedTypes);

    private static IEnumerable<TypeDefinition> SelfAndNestedTypes(TypeDefinition root)
    {
        yield return root;

        foreach (var nested in root.NestedTypes.SelectMany(SelfAndNestedTypes))
            yield return nested;
    }

    private static bool IsNestedWithin(TypeDefinition type, TypeDefinition root)
    {
        for (var declaringType = type.DeclaringType; declaringType is not null; declaringType = declaringType.DeclaringType)
        {
            if (declaringType.FullName == root.FullName)
                return true;
        }

        return false;
    }

    private static IEnumerable<TypeReference> ReferencedTypes(TypeDefinition type)
    {
        if (type.BaseType is not null)
        {
            foreach (var reference in Expand(type.BaseType))
                yield return reference;
        }

        foreach (var reference in type.Interfaces.SelectMany(@interface => Expand(@interface.InterfaceType)))
            yield return reference;

        foreach (var reference in type.GenericParameters.SelectMany(GenericParameterTypes))
            yield return reference;

        foreach (var reference in type.Fields.SelectMany(field => Expand(field.FieldType)))
            yield return reference;

        foreach (var reference in type.Properties.SelectMany(property => Expand(property.PropertyType)))
            yield return reference;

        foreach (var method in type.Methods)
        {
            foreach (var reference in Expand(method.ReturnType))
                yield return reference;

            foreach (var reference in method.Parameters.SelectMany(parameter => Expand(parameter.ParameterType)))
                yield return reference;

            foreach (var reference in method.GenericParameters.SelectMany(GenericParameterTypes))
                yield return reference;

            if (!method.HasBody)
                continue;

            foreach (var reference in method.Body.Variables.SelectMany(variable => Expand(variable.VariableType)))
                yield return reference;

            foreach (var reference in method.Body.ExceptionHandlers
                         .Where(handler => handler.CatchType is not null)
                         .SelectMany(handler => Expand(handler.CatchType!)))
                yield return reference;

            foreach (var instruction in method.Body.Instructions)
            {
                foreach (var reference in OperandTypes(instruction.Operand))
                    yield return reference;
            }
        }
    }

    private static IEnumerable<TypeReference> OperandTypes(object? operand) => operand switch
    {
        TypeReference type => Expand(type),
        MethodReference method => MethodTypes(method),
        FieldReference field => Expand(field.DeclaringType).Concat(Expand(field.FieldType)),
        _ => []
    };

    private static IEnumerable<TypeReference> MethodTypes(MethodReference method)
    {
        foreach (var reference in Expand(method.DeclaringType))
            yield return reference;

        foreach (var reference in Expand(method.ReturnType))
            yield return reference;

        foreach (var reference in method.Parameters.SelectMany(parameter => Expand(parameter.ParameterType)))
            yield return reference;

        if (method is GenericInstanceMethod genericMethod)
        {
            foreach (var reference in genericMethod.GenericArguments.SelectMany(Expand))
                yield return reference;
        }
    }

    private static IEnumerable<TypeReference> GenericParameterTypes(GenericParameter parameter) =>
        parameter.Constraints.SelectMany(constraint => Expand(constraint.ConstraintType));

    private static IEnumerable<TypeReference> Expand(TypeReference type)
    {
        yield return type;

        if (type is TypeSpecification specification)
        {
            foreach (var reference in Expand(specification.ElementType))
                yield return reference;
        }

        if (type is GenericInstanceType genericType)
        {
            foreach (var reference in genericType.GenericArguments.SelectMany(Expand))
                yield return reference;
        }

        foreach (var reference in type.GenericParameters.SelectMany(GenericParameterTypes))
            yield return reference;
    }

    private static IEnumerable<MethodReference> ReferencedMethods(TypeDefinition type) =>
        type.Methods
            .Where(method => method.HasBody)
            .SelectMany(method => method.Body.Instructions)
            .Select(instruction => instruction.Operand)
            .OfType<MethodReference>();

    private static bool IsDecryptionOrKeyUnwrappingType(TypeReference type) =>
        ContainsDecryptionOrKeyUnwrappingVerb(type.Name);

    private static bool IsDecryptionOrKeyUnwrappingMethod(MethodReference method) =>
        ContainsDecryptionOrKeyUnwrappingVerb(method.Name) ||
        IsDecryptionOrKeyUnwrappingType(method.DeclaringType);

    private static bool ContainsDecryptionOrKeyUnwrappingVerb(string name) =>
        name.Contains("Decrypt", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Unprotect", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Unwrap", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<MethodDefinition> PersistedCiphertextReaders(TypeDefinition type) =>
        type.Methods
            .Where(method => method.HasBody)
            .Where(method => method.Body.Instructions.Any(instruction =>
                instruction.Operand is MethodReference method &&
                method.Name == $"get_{nameof(EncryptedRecord.Ciphertext)}" &&
                method.DeclaringType.FullName == typeof(EncryptedRecord).FullName));

    private static IEnumerable<FieldInfo> InstanceFields(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var field in current.GetFields(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                yield return field;
        }
    }

    private static IEnumerable<PropertyInfo> InstanceProperties(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var property in current.GetProperties(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                yield return property;
        }
    }

    private static bool ContainsPlaintextFinancialType(Type type)
    {
        if (type.FullName is string fullName && PlaintextFinancialTypeNames.Contains(fullName))
            return true;

        if (type.HasElementType && type.GetElementType() is Type elementType && ContainsPlaintextFinancialType(elementType))
            return true;

        return type.IsGenericType && type.GetGenericArguments().Any(ContainsPlaintextFinancialType);
    }

    private static IReadOnlySet<string> DiscoverPlaintextFinancialTypeNames()
    {
        using var module = ModuleDefinition.ReadModule(typeof(Account).Assembly.Location);
        var domainTypes = module.GetTypes().ToDictionary(type => type.FullName, StringComparer.Ordinal);
        var discovered = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<TypeDefinition>(PlaintextFinancialRoots.Select(root => domainTypes[root.FullName!]));

        while (pending.TryDequeue(out var current))
        {
            if (!discovered.Add(current.FullName))
                continue;

            foreach (var reference in ReferencedTypes(current))
            {
                if (domainTypes.TryGetValue(reference.FullName, out var referenced) &&
                    !discovered.Contains(referenced.FullName))
                    pending.Enqueue(referenced);
            }
        }

        return discovered;
    }

    private static CreateRecordRequest Record(
        Guid id,
        EncryptedRecordType recordType,
        string purpose,
        Guid? parentResourceId)
    {
        var payload = EncryptPlaintext($"{purpose}:{PlaintextMarker}", purpose);
        return new CreateRecordRequest(
            id,
            Convert.ToHexString(OpaqueBytes($"idempotency-{purpose}")),
            recordType,
            parentResourceId,
            1,
            payload.Nonce,
            payload.Ciphertext,
            new PersonalEnvelopeRequest(
                OpaqueBytes($"personal-envelope-key-{purpose}"),
                OpaqueBytes($"personal-envelope-nonce-{purpose}"),
                1));
    }

    private static EncryptedPayload EncryptPlaintext(string plaintext, string purpose)
    {
        var nonce = OpaqueBytes($"payload-nonce-{purpose}")[..12];
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[16];
        using var aesGcm = new AesGcm(TestEncryptionKey, tag.Length);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        return new EncryptedPayload(nonce, [.. ciphertext, .. tag]);
    }

    private static byte[] OpaqueBytes(string purpose) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(purpose));

    private static XpenseUser User(Guid id, DateTime now) => new()
    {
        Id = id,
        State = AccountState.Active,
        CreatedAt = now
    };

    private static Xpense.Domain.Entities.Group Group(Guid id, Guid ownerUserId, DateTime now) => new()
    {
        Id = id,
        OwnerUserId = ownerUserId,
        NameCiphertext = OpaqueBytes($"group-name-{id}"),
        NameNonce = OpaqueBytes($"group-nonce-{id}"),
        ProtocolVersion = 1,
        CreatedAt = now,
        UpdatedAt = now
    };

    private static GroupMembership Membership(Guid groupId, Guid userId, MembershipRole role, DateTime now) => new()
    {
        Id = Guid.CreateVersion7(),
        GroupId = groupId,
        UserId = userId,
        Role = role,
        State = MembershipState.Active,
        EnvelopeProtocolVersion = 1,
        CreatedAt = now,
        UpdatedAt = now
    };

    private static SharedResource Resource(Guid id, Guid ownerUserId, DateTime now) => new()
    {
        Id = id,
        Type = SharedResourceType.Account,
        OwnerUserId = ownerUserId,
        CreatedAt = now
    };

    private static ResourceGrant Grant(Guid groupId, Guid resourceId, Guid grantedByUserId, DateTime now) => new()
    {
        Id = Guid.CreateVersion7(),
        GroupId = groupId,
        ResourceType = SharedResourceType.Account,
        ResourceId = resourceId,
        Permission = GrantPermission.Viewer,
        State = GrantState.Active,
        GrantedByUserId = grantedByUserId,
        CreatedAt = now,
        UpdatedAt = now
    };

    private sealed record StoredColumnValue(string EntityType, string? ColumnName, object? Value);

    private sealed record IlAuditResult(
        string[] FinancialDependencies,
        string[] DecryptionDependencies,
        MethodDefinition[] PersistedCiphertextReaders);

    private sealed record EncryptedPayload(byte[] Nonce, byte[] Ciphertext);

    private sealed record CreateRequest(CreateRecordRequest[] Records);

    private sealed record CreateRecordRequest(
        Guid Id,
        string IdempotencyKey,
        EncryptedRecordType RecordType,
        Guid? ParentResourceId,
        int ProtocolVersion,
        byte[] Nonce,
        byte[] Ciphertext,
        PersonalEnvelopeRequest PersonalEnvelope);

    private sealed record PersonalEnvelopeRequest(byte[] WrappedKey, byte[] Nonce, int ProtocolVersion);

    private sealed record AddEnvelopeRequest(
        Guid GroupId,
        GrantPermission Permission,
        byte[] WrappedKey,
        byte[] Nonce,
        byte[] EncapsulatedKey,
        int ProtocolVersion);

    private sealed record ChangesResponse(ChangeRecord[] Records, string NextCursor, bool HasMore);

    private sealed record ChangeRecord(
        Guid Id,
        byte[] Nonce,
        byte[] Ciphertext,
        ChangeEnvelope[] Envelopes);

    private sealed record ChangeEnvelope(
        Guid? GroupId,
        byte[] WrappedKey,
        byte[] Nonce,
        byte[]? EncapsulatedKey);

    private static class RecordFixture
    {
        public static EncryptedRecord Create(Guid ownerUserId, Guid? parentResourceId, string purpose) => new()
        {
            Id = Guid.CreateVersion7(),
            RecordType = EncryptedRecordType.Transaction,
            OwnerUserId = ownerUserId,
            ParentResourceId = parentResourceId,
            Revision = 1,
            ProtocolVersion = 1,
            Nonce = OpaqueBytes($"{purpose}-nonce"),
            Ciphertext = OpaqueBytes($"{purpose}-ciphertext"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static class EnvelopeFixture
    {
        public static RecordEnvelope Create(Guid recordId, Guid? groupId, string purpose) => new()
        {
            Id = Guid.CreateVersion7(),
            EncryptedRecordId = recordId,
            GroupId = groupId,
            WrappedKey = OpaqueBytes($"{purpose}-wrapped-key"),
            Nonce = OpaqueBytes($"{purpose}-nonce"),
            EncapsulatedKey = OpaqueBytes($"{purpose}-encapsulated-key"),
            ProtocolVersion = 1
        };
    }
}
}

namespace Xpense.API.Features.Sync
{
    internal static class ServerBlindnessTopLevelForbiddenHelper
    {
        public static Account Account { get; } = null!;

        public static Money Amount { get; } = null!;

        public static void ReadCiphertext(
            AesGcm aesGcm,
            byte[] nonce,
            byte[] ciphertext,
            byte[] tag,
            byte[] plaintext) =>
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        public static byte[] ReadProtectedValue(
            ServerBlindnessSyntheticUnprotector unprotector,
            byte[] protectedValue) =>
            unprotector.Unprotect(protectedValue);

        public static byte[] ReadWrappedKey(
            ServerBlindnessSyntheticKeyUnwrapper keyUnwrapper,
            byte[] wrappedKey) =>
            keyUnwrapper.Unwrap(wrappedKey);
    }

    internal static class ServerBlindnessTopLevelHashHelper
    {
        public static byte[] Hash(byte[] value) => SHA256.HashData(value);
    }

    internal static class ServerBlindnessTopLevelAsyncHelper
    {
        private static class NestedAsyncHelper
        {
            public static async Task<BudgetPeriod> ReadPeriod()
            {
                await Task.Yield();
                return new BudgetPeriod(DateTime.UnixEpoch, DateTime.UnixEpoch.AddDays(1), string.Empty);
            }
        }
    }

    internal sealed class ServerBlindnessSyntheticUnprotector
    {
        public byte[] Unprotect(byte[] protectedValue) => protectedValue;
    }

    internal sealed class ServerBlindnessSyntheticKeyUnwrapper
    {
        public byte[] Unwrap(byte[] wrappedKey) => wrappedKey;
    }
}

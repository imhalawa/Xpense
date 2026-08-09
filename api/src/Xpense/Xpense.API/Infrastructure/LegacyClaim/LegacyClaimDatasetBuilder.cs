using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Contracts;
using Xpense.Domain.Enums;
using Xpense.Persistence;

namespace Xpense.API.Infrastructure.LegacyClaim;

public sealed class LegacyClaimDatasetBuilder(XpenseDbContext dbContext)
{
    public const int ProtocolVersion = 1;

    public static readonly Guid RecordNamespace = new("f1d8c702-73fd-5d8a-b6d4-0869377cfa2c");

    private static readonly JsonSerializerOptions SourceJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string[] RecordTypeNames =
    [
        "account",
        "necessityScale",
        "category",
        "merchant",
        "tag",
        "transaction",
        "transfer",
        "budget",
        "notification"
    ];

    public async Task<BuiltLegacyDataset> Build(CancellationToken cancellationToken)
    {
        var accounts = await dbContext.Accounts.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(account => account.Id)
            .Select(account => new LegacyAccountRecord(
                Id("account", account.Id), account.Id, account.IsDeleted, account.CreatedAt,
                account.UpdatedAt, account.Label, account.AccountNumber, account.BalanceMinorUnits,
                account.Currency, account.IsDefault))
            .ToArrayAsync(cancellationToken);
        var necessityScales = await dbContext.Priorities.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(priority => priority.Id)
            .Select(priority => new LegacyNecessityScaleRecord(
                Id("necessityScale", priority.Id), priority.Id, priority.IsDeleted, priority.CreatedAt,
                priority.UpdatedAt, priority.Label, priority.Weight))
            .ToArrayAsync(cancellationToken);
        var categories = await dbContext.Categories.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(category => category.Id)
            .Select(category => new LegacyCategoryRecord(
                Id("category", category.Id), category.Id, category.IsDeleted, category.CreatedAt,
                category.UpdatedAt, category.Label, category.PriorityId,
                Id("necessityScale", category.PriorityId)))
            .ToArrayAsync(cancellationToken);
        var merchants = await dbContext.Merchants.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(merchant => merchant.Id)
            .Select(merchant => new LegacyMerchantRecord(
                Id("merchant", merchant.Id), merchant.Id, merchant.IsDeleted, merchant.CreatedAt,
                merchant.UpdatedAt, merchant.Label))
            .ToArrayAsync(cancellationToken);
        var tags = await dbContext.Tags.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(tag => tag.Id)
            .Select(tag => new LegacyTagRecord(
                Id("tag", tag.Id), tag.Id, tag.IsDeleted, tag.CreatedAt, tag.UpdatedAt,
                tag.Label, tag.BgColorHex, tag.FgColorHex))
            .ToArrayAsync(cancellationToken);
        var transactionEntities = await dbContext.Transactions.IgnoreQueryFilters().AsNoTracking()
            .Include(transaction => transaction.Tags)
            .OrderBy(transaction => transaction.Id)
            .ToArrayAsync(cancellationToken);
        var transactions = transactionEntities.Select(transaction =>
        {
            var recordType = transaction.Kind == TransactionKind.Transfer ? "transfer" : "transaction";
            var transactionTags = transaction.Tags?.OrderBy(tag => tag.Id).ToArray() ?? [];
            return new LegacyTransactionRecord(
                Id(recordType, transaction.Id), transaction.Id, transaction.IsDeleted,
                transaction.CreatedAt, transaction.UpdatedAt, transaction.Kind,
                transaction.AmountMinorUnits, transaction.Currency, transaction.OccurredAt,
                transaction.Reason, transaction.SourceAccountId,
                NullableId("account", transaction.SourceAccountId), transaction.DestinationAccountId,
                NullableId("account", transaction.DestinationAccountId), transaction.CategoryId,
                NullableId("category", transaction.CategoryId), transaction.MerchantId,
                NullableId("merchant", transaction.MerchantId),
                transactionTags.Select(tag => tag.Id).ToArray(),
                transactionTags.Select(tag => Id("tag", tag.Id)).ToArray());
        }).ToArray();
        var budgets = await dbContext.Budgets.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(budget => budget.Id)
            .Select(budget => new LegacyBudgetRecord(
                Id("budget", budget.Id), budget.Id, budget.IsDeleted, budget.CreatedAt,
                budget.UpdatedAt, budget.AmountMinorUnits, budget.Currency, budget.CategoryId,
                Id("category", budget.CategoryId), budget.Recurrence, budget.StartsOn, budget.EndsOn,
                budget.AlertThresholdPercent))
            .ToArrayAsync(cancellationToken);
        var notifications = await dbContext.Notifications.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(notification => notification.Id)
            .Select(notification => new LegacyNotificationRecord(
                Id("notification", notification.Id), notification.Id, notification.IsDeleted,
                notification.CreatedAt, notification.UpdatedAt, notification.OwnerId,
                notification.EventId, notification.Kind, notification.Title, notification.Message,
                notification.Payload, notification.PayloadHash, notification.ReadAt))
            .ToArrayAsync(cancellationToken);

        var identities = new List<(string Type, Guid Id)>();
        identities.AddRange(accounts.Select(record => ("account", record.Id)));
        identities.AddRange(necessityScales.Select(record => ("necessityScale", record.Id)));
        identities.AddRange(categories.Select(record => ("category", record.Id)));
        identities.AddRange(merchants.Select(record => ("merchant", record.Id)));
        identities.AddRange(tags.Select(record => ("tag", record.Id)));
        identities.AddRange(transactions.Select(record => (
            record.Kind == TransactionKind.Transfer ? "transfer" : "transaction", record.Id)));
        identities.AddRange(budgets.Select(record => ("budget", record.Id)));
        identities.AddRange(notifications.Select(record => ("notification", record.Id)));
        var orderedIdentities = identities
            .OrderBy(identity => identity.Type, StringComparer.Ordinal)
            .ThenBy(identity => identity.Id)
            .ToArray();
        var counts = RecordTypeNames.ToDictionary(
            recordType => recordType,
            recordType => orderedIdentities.Count(identity => identity.Type == recordType),
            StringComparer.Ordinal);
        var manifest = BuildManifest(orderedIdentities);
        var manifestHash = SHA256.HashData(Encoding.UTF8.GetBytes(manifest));

        var response = new LegacyClaimDatasetResponse(
            ProtocolVersion,
            orderedIdentities.Length,
            counts,
            Convert.ToHexStringLower(manifestHash),
            accounts,
            necessityScales,
            categories,
            merchants,
            tags,
            transactions,
            budgets,
            notifications);
        var sourceHash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(response, SourceJsonOptions));
        return new BuiltLegacyDataset(response, manifestHash, sourceHash);
    }

    public static Guid Id(string recordType, int legacyId)
    {
        var namespaceBytes = RecordNamespace.ToByteArray();
        ToNetworkOrder(namespaceBytes);
        var nameBytes = Encoding.UTF8.GetBytes(
            $"Xpense legacy claim v1|{recordType}|{legacyId.ToString(CultureInfo.InvariantCulture)}");
        var input = new byte[namespaceBytes.Length + nameBytes.Length];
        namespaceBytes.CopyTo(input, 0);
        nameBytes.CopyTo(input, namespaceBytes.Length);
        var hash = SHA1.HashData(input);
        hash[6] = (byte)((hash[6] & 0x0f) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        var guidBytes = hash[..16];
        ToNetworkOrder(guidBytes);
        return new Guid(guidBytes);
    }

    public static string BuildManifest(IEnumerable<(string Type, Guid Id)> identities)
    {
        var lines = identities
            .Select(identity => $"{identity.Type}|{identity.Id:D}".ToLowerInvariant())
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToArray();
        var count = lines.Length.ToString(CultureInfo.InvariantCulture);
        return lines.Length == 0 ? count : $"{count}\n{string.Join('\n', lines)}";
    }

    private static Guid? NullableId(string type, int? legacyId) =>
        legacyId is null ? null : Id(type, legacyId.Value);

    private static void ToNetworkOrder(byte[] bytes)
    {
        (bytes[0], bytes[3]) = (bytes[3], bytes[0]);
        (bytes[1], bytes[2]) = (bytes[2], bytes[1]);
        (bytes[4], bytes[5]) = (bytes[5], bytes[4]);
        (bytes[6], bytes[7]) = (bytes[7], bytes[6]);
    }
}

public sealed record BuiltLegacyDataset(
    LegacyClaimDatasetResponse Response,
    byte[] ManifestHash,
    byte[] SourceContentHash)
{
    public ExpectedEncryptedRecord[] ExpectedEncryptedRecords()
    {
        var expected = new List<ExpectedEncryptedRecord>();
        expected.AddRange(Response.Accounts.Select(record =>
            new ExpectedEncryptedRecord(record.Id, EncryptedRecordType.Account, record.IsDeleted, record.Id)));
        expected.AddRange(Response.NecessityScales.Select(record =>
            new ExpectedEncryptedRecord(record.Id, EncryptedRecordType.NecessityScale, record.IsDeleted)));
        expected.AddRange(Response.Categories.Select(record =>
            new ExpectedEncryptedRecord(record.Id, EncryptedRecordType.Category, record.IsDeleted)));
        expected.AddRange(Response.Merchants.Select(record =>
            new ExpectedEncryptedRecord(record.Id, EncryptedRecordType.Merchant, record.IsDeleted)));
        expected.AddRange(Response.Tags.Select(record =>
            new ExpectedEncryptedRecord(record.Id, EncryptedRecordType.Tag, record.IsDeleted)));
        expected.AddRange(Response.Transactions.Select(record =>
            new ExpectedEncryptedRecord(
                record.Id,
                record.Kind == TransactionKind.Transfer
                    ? EncryptedRecordType.Transfer
                    : EncryptedRecordType.Transaction,
                record.IsDeleted,
                record.Kind == TransactionKind.Income
                    ? record.DestinationAccountId
                    : record.SourceAccountId)));
        expected.AddRange(Response.Budgets.Select(record =>
            new ExpectedEncryptedRecord(record.Id, EncryptedRecordType.Budget, record.IsDeleted, record.Id)));
        expected.AddRange(Response.Notifications.Select(record =>
            new ExpectedEncryptedRecord(record.Id, EncryptedRecordType.Notification, record.IsDeleted)));
        return expected.ToArray();
    }
}

public sealed record ExpectedEncryptedRecord(
    Guid Id,
    EncryptedRecordType RecordType,
    bool IsDeleted,
    Guid? ParentResourceId = null);

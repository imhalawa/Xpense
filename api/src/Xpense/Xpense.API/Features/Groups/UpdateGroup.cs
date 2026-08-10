using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.Authorization;
using Xpense.Persistence;

namespace Xpense.API.Features.Groups;

public sealed class UpdateGroup : IEndpoint
{
    private const int MaximumDecodedLength = 4096;
    private const int MaximumEncodedLength = 5464;
    private const int SupportedProtocolVersion = 1;

    /// <summary>
    /// Replaces the encrypted group name.
    /// </summary>
    public sealed record Request(
        string? NameCiphertext,
        string? NameNonce,
        int ProtocolVersion);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            EncryptedValue(
                RuleFor(request => request.NameCiphertext),
                "The encrypted group name is required.",
                "The encrypted group name must be canonical Base64 containing 1 to 4096 bytes.");
            EncryptedValue(
                RuleFor(request => request.NameNonce),
                "The encrypted group name nonce is required.",
                "The encrypted group name nonce must be canonical Base64 containing 1 to 4096 bytes.");
            RuleFor(request => request.ProtocolVersion)
                .Equal(SupportedProtocolVersion).WithMessage("The protocol version must be 1.");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/groups/{groupId:guid}", Handle)
            .WithName(nameof(UpdateGroup))
            .Validated()
            .RequireAuthorization();

    private static async Task<Results<Ok<GroupResponse>, NotFound>> Handle(
        Guid groupId,
        Request request,
        AccessRules accessRules,
        GroupTransactionLock groupTransactionLock,
        ICurrentUser currentUser,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await using var groupLock = await groupTransactionLock.Acquire(groupId, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        if (!await accessRules.IsGroupOwner(groupId, cancellationToken))
            return TypedResults.NotFound();

        var userId = currentUser.Id;
        var item = await (
            from membership in dbContext.GroupMemberships
            join groupEntity in dbContext.Groups on membership.GroupId equals groupEntity.Id
            where groupEntity.Id == groupId &&
                  membership.UserId == userId
            select new { Group = groupEntity, Membership = membership })
            .SingleOrDefaultAsync(cancellationToken);

        if (item is null)
            return TypedResults.NotFound();

        item.Group.Rename(
            Convert.FromBase64String(request.NameCiphertext!),
            Convert.FromBase64String(request.NameNonce!),
            request.ProtocolVersion);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.Ok(GroupResponse.Of(item.Group, item.Membership));
    }

    private static IRuleBuilderOptions<Request, string?> EncryptedValue(
        IRuleBuilderInitial<Request, string?> rule,
        string requiredMessage,
        string invalidMessage) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(requiredMessage)
            .Must(IsCanonicalBase64).WithMessage(invalidMessage);

    private static bool IsCanonicalBase64(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaximumEncodedLength)
            return false;

        Span<byte> decoded = stackalloc byte[MaximumDecodedLength];
        return Convert.TryFromBase64String(value, decoded, out var bytesWritten) &&
               bytesWritten > 0 &&
               Convert.ToBase64String(decoded[..bytesWritten]) == value;
    }
}

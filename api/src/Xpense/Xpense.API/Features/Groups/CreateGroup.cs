using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Persistence;

namespace Xpense.API.Features.Groups;

public sealed class CreateGroup : IEndpoint
{
    private const int MaximumDecodedLength = 4096;
    private const int MaximumEncodedLength = 5464;
    private const int SupportedProtocolVersion = 1;

    /// <summary>
    /// Creates a group with an encrypted name and the caller's encrypted group-key envelope.
    /// </summary>
    public sealed record Request(
        string? NameCiphertext,
        string? NameNonce,
        int ProtocolVersion,
        string? OwnerKeyEnvelope);

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
            EncryptedValue(
                RuleFor(request => request.OwnerKeyEnvelope),
                "The owner key envelope is required.",
                "The owner key envelope must be canonical Base64 containing 1 to 4096 bytes.");
            RuleFor(request => request.ProtocolVersion)
                .Equal(SupportedProtocolVersion).WithMessage("The protocol version must be 1.");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/groups", Handle)
            .WithName(nameof(CreateGroup))
            .Validated()
            .RequireAuthorization();

    private static async Task<Created<GroupResponse>> Handle(
        Request request,
        ICurrentUser currentUser,
        XpenseDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var group = new Group
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = currentUser.Id,
            NameCiphertext = Convert.FromBase64String(request.NameCiphertext!),
            NameNonce = Convert.FromBase64String(request.NameNonce!),
            ProtocolVersion = request.ProtocolVersion,
            CreatedAt = now,
            UpdatedAt = now
        };
        var membership = new GroupMembership
        {
            Id = Guid.CreateVersion7(),
            GroupId = group.Id,
            UserId = currentUser.Id,
            Role = MembershipRole.Owner,
            State = MembershipState.Active,
            GroupKeyEnvelope = Convert.FromBase64String(request.OwnerKeyEnvelope!),
            EnvelopeProtocolVersion = request.ProtocolVersion,
            CreatedAt = now,
            UpdatedAt = now
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        dbContext.Groups.Add(group);
        dbContext.GroupMemberships.Add(membership);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.Created(
            httpContext.ResourceUri($"/api/v1/groups/{group.Id}"),
            GroupResponse.Of(group, membership));
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

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
using Xpense.Persistence;
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Domain.Transfers;
using Xpense.Domain.ValueObjects;

namespace Xpense.API.Features.Transfers;

public sealed class CreateTransfer : IEndpoint
{
    public sealed record Request(
        int SourceAccountId,
        int DestinationAccountId,
        MoneyRequest Amount,
        string Reason,
        DateTimeOffset? OccurredAt);

    public sealed record MoneyRequest(long Cents, string Currency);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.SourceAccountId)
                .GreaterThan(0).WithMessage("The sourceAccountId must reference an existing account.");

            RuleFor(request => request.DestinationAccountId)
                .GreaterThan(0).WithMessage("The destinationAccountId must reference an existing account.")
                .NotEqual(request => request.SourceAccountId)
                .WithMessage("Source and destination accounts must be different.");

            RuleFor(request => request.Amount)
                .NotNull().WithMessage("The amount is required.");

            When(request => request.Amount is not null, () =>
            {
                RuleFor(request => request.Amount.Cents)
                    .GreaterThan(0).WithMessage("The amount in cents must be positive.");

                RuleFor(request => request.Amount.Currency)
                    .Must(currency => TryParseCurrency(currency, out _))
                    .WithMessage("The currency must be a supported currency name.");
            });

            RuleFor(request => request.Reason).MaximumLength(500);
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/transfers", Handle).WithName(nameof(CreateTransfer)).Validated();

    // ponytail: 201 without a Location header -- there is no GET /api/v1/transfers/{id} to
    // point at yet. Switch to a resource URI when that endpoint lands.
    private static async Task<IResult> Handle(
        Request request,
        XpenseDbContext db,
        CancellationToken ct)
    {
        TryParseCurrency(request.Amount.Currency, out var currency);

        // Both balance changes and both audit legs commit together or not at all.
        await using var scope = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var source = await db.Accounts.FirstOrDefaultAsync(a => a.Id == request.SourceAccountId, ct)
                         ?? throw new AccountNotFoundException(request.SourceAccountId);

            var destination = await db.Accounts.FirstOrDefaultAsync(a => a.Id == request.DestinationAccountId, ct)
                              ?? throw new AccountNotFoundException(request.DestinationAccountId);

            var transfer = MoneyTransfer.Between(
                source,
                destination,
                Money.OfCents(request.Amount.Cents, currency),
                request.Reason,
                request.OccurredAt?.UtcDateTime ?? DateTime.UtcNow);

            db.Transfers.Add(transfer);
            await db.SaveChangesAsync(ct);
            await scope.CommitAsync(ct);

            return TypedResults.Created((string)null, TransferResponse.Of(transfer));
        }
        catch
        {
            await scope.RollbackAsync(ct);
            throw;
        }
    }

    private static bool TryParseCurrency(string currency, out Currency parsed)
    {
        parsed = default;
        return !string.IsNullOrWhiteSpace(currency)
               && Enum.GetNames<Currency>().Any(name => string.Equals(name, currency, StringComparison.OrdinalIgnoreCase))
               && Enum.TryParse(currency, ignoreCase: true, out parsed);
    }
}

using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xpense.Persistence;
using Xpense.Persistence.Repositories;
using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Entities;
using Xpense.Services.Enums;
using Xpense.Services.Exceptions;
using Xpense.Services.Features.Transactions.Commands;
using Xpense.Services.Features.Transactions.UseCases;
using Xpense.Services.ValueObjects;

namespace Xpense.Tests.Unit;

[TestFixture]
public class TransferTransactionUseCaseTests
{
    [Test]
    public async Task Handle_debits_source_credits_destination_and_persists_two_correlated_legs()
    {
        await using var database = await TransferTestDatabase.Create(20m, 3m);
        var useCase = new TransferTransactionUseCase(
            new AccountRepository(database.Context),
            new TransferRepository(database.Context));
        var occurredAt = new DateTimeOffset(2026, 7, 26, 9, 30, 0, TimeSpan.Zero);

        var result = await useCase.Handle(new TransferTransactionCommand(
            Money.OfCents(1234, Currency.USD),
            database.SourceId,
            database.DestinationId,
            "Shared rent",
            occurredAt.ToUnixTimeSeconds()));

        database.Context.ChangeTracker.Clear();
        var sourceBalance = await database.Context.Accounts
            .Where(account => account.Id == database.SourceId)
            .Select(account => account.Balance)
            .SingleAsync();
        var destinationBalance = await database.Context.Accounts
            .Where(account => account.Id == database.DestinationId)
            .Select(account => account.Balance)
            .SingleAsync();
        var persisted = await database.Context.Transfers
            .Include(transfer => transfer.Legs)
            .SingleAsync(transfer => transfer.Id == result.Id);

        sourceBalance.Should().Be(7.66m);
        destinationBalance.Should().Be(15.34m);
        persisted.Amount.Should().Be(1234);
        persisted.Currency.Should().Be(Currency.USD);
        persisted.SourceAccountId.Should().Be(database.SourceId);
        persisted.DestinationAccountId.Should().Be(database.DestinationId);
        persisted.Reason.Should().Be("Shared rent");
        persisted.CreatedOn.Should().Be(occurredAt.UtcDateTime);
        persisted.Legs.Should().BeEquivalentTo(
            new[]
            {
                new { AccountId = database.SourceId, Direction = TransferLegDirection.Debit, Amount = 1234L, Currency = Currency.USD },
                new { AccountId = database.DestinationId, Direction = TransferLegDirection.Credit, Amount = 1234L, Currency = Currency.USD }
            },
            options => options.ExcludingMissingMembers());
        persisted.Legs.Should().OnlyContain(leg => leg.TransferId == persisted.Id);
    }

    [Test]
    public async Task Handle_rejects_identical_accounts_without_changing_the_balance()
    {
        await using var database = await TransferTestDatabase.Create(20m, 3m);
        var useCase = new TransferTransactionUseCase(
            new AccountRepository(database.Context),
            new TransferRepository(database.Context));

        var action = () => useCase.Handle(new TransferTransactionCommand(
            Money.OfCents(100), database.SourceId, database.SourceId));

        await action.Should().ThrowAsync<InvalidTransferException>();
        database.Context.ChangeTracker.Clear();
        (await database.Context.Accounts.SingleAsync(account => account.Id == database.SourceId))
            .Balance.Should().Be(20m);
        (await database.Context.Transfers.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Handle_rejects_a_transfer_exceeding_the_source_balance()
    {
        await using var database = await TransferTestDatabase.Create(5m, 3m);
        var useCase = new TransferTransactionUseCase(
            new AccountRepository(database.Context),
            new TransferRepository(database.Context));

        var action = () => useCase.Handle(new TransferTransactionCommand(
            Money.OfCents(501), database.SourceId, database.DestinationId));

        await action.Should().ThrowAsync<InsufficientFundsForTransferException>();
        database.Context.ChangeTracker.Clear();
        (await database.Context.Accounts.SingleAsync(account => account.Id == database.SourceId))
            .Balance.Should().Be(5m);
        (await database.Context.Accounts.SingleAsync(account => account.Id == database.DestinationId))
            .Balance.Should().Be(3m);
        (await database.Context.Transfers.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Handle_does_not_partially_persist_balances_when_the_atomic_save_fails()
    {
        await using var database = await TransferTestDatabase.Create(20m, 3m);
        var useCase = new TransferTransactionUseCase(
            new AccountRepository(database.Context),
            new FailingTransferRepository());

        var action = () => useCase.Handle(new TransferTransactionCommand(
            Money.OfCents(1234), database.SourceId, database.DestinationId));

        await action.Should().ThrowAsync<InvalidOperationException>();
        await using var verificationContext = new XpenseDbContext(database.Options);
        (await verificationContext.Accounts.SingleAsync(account => account.Id == database.SourceId))
            .Balance.Should().Be(20m);
        (await verificationContext.Accounts.SingleAsync(account => account.Id == database.DestinationId))
            .Balance.Should().Be(3m);
        (await verificationContext.Transfers.CountAsync()).Should().Be(0);
    }

    private sealed class FailingTransferRepository : ITransferRepository
    {
        public Task<Transfer> ExecuteAtomic(Func<Task<Transfer>> createTransfer)
        {
            throw new InvalidOperationException("Simulated persistence failure.");
        }
    }

    private sealed class TransferTestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private TransferTestDatabase(
            SqliteConnection connection,
            DbContextOptions<XpenseDbContext> options,
            XpenseDbContext context,
            int sourceId,
            int destinationId)
        {
            this.connection = connection;
            Options = options;
            Context = context;
            SourceId = sourceId;
            DestinationId = destinationId;
        }

        public DbContextOptions<XpenseDbContext> Options { get; }
        public XpenseDbContext Context { get; }
        public int SourceId { get; }
        public int DestinationId { get; }

        public static async Task<TransferTestDatabase> Create(decimal sourceBalance, decimal destinationBalance)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<XpenseDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new XpenseDbContext(options);
            await context.Database.EnsureCreatedAsync();

            var source = Account("1000000000", "Source", sourceBalance);
            var destination = Account("2000000000", "Destination", destinationBalance);
            context.Accounts.AddRange(source, destination);
            await context.SaveChangesAsync();

            return new TransferTestDatabase(connection, options, context, source.Id, destination.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }

        private static Account Account(string number, string name, decimal balance)
        {
            return new Account
            {
                AccountNumber = number,
                Name = name,
                Balance = balance,
                CreatedOn = DateTime.UtcNow
            };
        }
    }
}

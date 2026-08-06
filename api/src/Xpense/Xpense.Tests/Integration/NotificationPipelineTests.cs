using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xpense.Notifications;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.Events;
using Xpense.Domain.ValueObjects;
using Xpense.Notifications.Rules;
using Xpense.Persistence;

namespace Xpense.Tests.Integration;

[TestFixture]
public class NotificationPipelineTests
{
    private const string SourceNumber = "1000000000";

    private string connectionString = null!;

    [SetUp]
    public async Task SetUp() => connectionString = await PostgresFixture.CreateDatabase();

    private XpenseDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<XpenseDbContext>().UseNpgsql(connectionString).Options);


    [Test]
    public async Task Emitting_an_event_writes_nothing_until_the_caller_saves()
    {
        await using var dbContext = NewDbContext();
        var bus = new EventBus(dbContext);

        await bus.Emit(Event.Of(Expense(amountMinorUnits: 1250, categoryId: 1)));

        (await dbContext.Events.CountAsync()).Should().Be(0, "nothing has been saved yet");

        await dbContext.SaveChangesAsync();

        (await dbContext.Events.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task An_emitted_event_round_trips_through_the_table()
    {
        var body = Expense(amountMinorUnits: 1250, categoryId: 7);

        await using var dbContext = NewDbContext();
        await new EventBus(dbContext).Emit(Event.Of(body));
        await dbContext.SaveChangesAsync();

        var stored = await dbContext.Events.SingleAsync();
        stored.Type.Should().Be(nameof(TransactionRecorded));
        stored.ProcessedAt.Should().BeNull("a fresh event is outstanding");
        stored.Attempts.Should().Be(0);

        var read = System.Text.Json.JsonSerializer.Deserialize<TransactionRecorded>(
            stored.Body, EventJson.Options);
        read.Should().BeEquivalentTo(body);
    }


    [Test]
    public async Task The_rule_says_nothing_when_spending_stays_under_the_limit()
    {
        var seeded = await Seed(limitMinorUnits: 30000);
        await SpendEur(seeded, 1250);

        var drafts = await Evaluate(Expense(1250, seeded.CategoryId));

        drafts.Should().BeEmpty();
    }

    [Test]
    public async Task The_rule_fires_on_the_expense_that_crosses_the_limit()
    {
        var seeded = await Seed(limitMinorUnits: 30000);
        await SpendEur(seeded, 29000);
        await SpendEur(seeded, 2000);

        var drafts = await Evaluate(Expense(2000, seeded.CategoryId));

        drafts.Should().HaveCount(1);
        drafts[0].Kind.Should().Be(NotificationKind.BudgetExceeded);
        drafts[0].Title.Should().Be("Food is over budget");
        drafts[0].Message.Should().Contain("310.00 EUR").And.Contain("10.00 EUR over");
    }

    [Test]
    public async Task The_rule_says_nothing_when_the_limit_was_already_passed()
    {
        var seeded = await Seed(limitMinorUnits: 30000);
        await SpendEur(seeded, 31000);
        await SpendEur(seeded, 500);

        var drafts = await Evaluate(Expense(500, seeded.CategoryId));

        drafts.Should().BeEmpty("the crossing already happened on an earlier expense");
    }

    [Test]
    public async Task The_rule_says_nothing_when_spending_lands_exactly_on_the_limit()
    {
        var seeded = await Seed(limitMinorUnits: 30000);
        await SpendEur(seeded, 30000);

        var drafts = await Evaluate(Expense(30000, seeded.CategoryId));

        drafts.Should().BeEmpty();
    }

    [Test]
    public async Task The_rule_ignores_spending_in_another_currency()
    {
        var seeded = await Seed(limitMinorUnits: 30000);
        await Spend(seeded, 40000, Currency.USD);

        var drafts = await Evaluate(Expense(40000, seeded.CategoryId, Currency.USD));

        drafts.Should().BeEmpty();
    }

    [TestCase(TransactionKind.Income)]
    [TestCase(TransactionKind.Transfer)]
    public async Task The_rule_ignores_anything_that_is_not_an_expense(TransactionKind kind)
    {
        var seeded = await Seed(limitMinorUnits: 1000);
        await SpendEur(seeded, 5000);

        var drafts = await Evaluate(Expense(5000, seeded.CategoryId) with { Kind = kind });

        drafts.Should().BeEmpty();
    }

    [Test]
    public async Task The_rule_says_nothing_for_a_period_the_budget_does_not_measure()
    {
        var seeded = await Seed(limitMinorUnits: 30000);
        await SpendEur(seeded, 40000);

        var drafts = await Evaluate(
            Expense(40000, seeded.CategoryId) with { OccurredAt = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc) });

        drafts.Should().BeEmpty();
    }

    [Test]
    public async Task One_expense_crossing_two_budgets_produces_two_drafts()
    {
        var seeded = await Seed(limitMinorUnits: 30000);

        await using (var dbContext = NewDbContext())
        {
            var category = await dbContext.Categories.SingleAsync(item => item.Id == seeded.CategoryId);
            dbContext.Budgets.Add(Budget.For(
                category,
                Money.OfMinorUnits(10000),
                Recurrence.Weekly,
                FirstOfThisMonth(),
                endsOn: null));
            await dbContext.SaveChangesAsync();
        }

        await SpendEur(seeded, 31000);

        var drafts = await Evaluate(Expense(31000, seeded.CategoryId));

        drafts.Should().HaveCount(2, "the monthly and the weekly budget were both crossed");
        drafts.Select(draft => draft.Kind).Should().AllBeEquivalentTo(NotificationKind.BudgetExceeded);
    }


    [Test]
    public async Task Processing_turns_an_outstanding_event_into_a_notification()
    {
        var seeded = await Seed(limitMinorUnits: 30000);
        await SpendEur(seeded, 31000);
        await EmitExpense(seeded, 31000);

        var claimed = await Process();

        claimed.Should().Be(1);

        await using var dbContext = NewDbContext();
        var notification = await dbContext.Notifications.SingleAsync();
        notification.Kind.Should().Be(NotificationKind.BudgetExceeded);
        notification.ReadAt.Should().BeNull("a new notification is unread");
        notification.PayloadHash.Should().HaveLength(64);

        var payload = System.Text.Json.Nodes.JsonNode.Parse(notification.Payload)!;
        payload["exceededByMinorUnits"]!.GetValue<long>().Should().Be(1000);
        payload["spentMinorUnits"]!.GetValue<long>().Should().Be(31000);
        payload["period"]!.GetValue<string>().Should().Be($"{DateTime.UtcNow:yyyy-MM}");

        (await dbContext.Events.SingleAsync()).ProcessedAt.Should().NotBeNull();
    }

    [Test]
    public async Task Processing_the_same_event_twice_stores_one_notification()
    {
        var seeded = await Seed(limitMinorUnits: 30000);
        await SpendEur(seeded, 31000);
        var eventId = await EmitExpense(seeded, 31000);

        await Process();

        await using (var dbContext = NewDbContext())
        {
            var stored = await dbContext.Events.SingleAsync(item => item.EventId == eventId);
            stored.ProcessedAt = null;
            await dbContext.SaveChangesAsync();
        }

        await Process();

        await using var verify = NewDbContext();
        (await verify.Notifications.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task An_event_nobody_has_a_rule_for_is_marked_processed_rather_than_retried()
    {
        await using (var dbContext = NewDbContext())
        {
            dbContext.Events.Add(new EventRecord
            {
                EventId = Guid.CreateVersion7(),
                Type = "SomethingNobodyHandles",
                OccurredAt = DateTime.UtcNow,
                Source = "test",
                Version = 1,
                Body = "{}",
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        (await Process()).Should().Be(1);

        await using var verify = NewDbContext();
        var stored = await verify.Events.SingleAsync();
        stored.ProcessedAt.Should().NotBeNull("nothing listens, which is a normal outcome");
        stored.Attempts.Should().Be(0, "nothing failed");
        (await verify.Notifications.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task An_event_that_cannot_be_processed_records_its_failure_and_is_retried()
    {
        await using (var dbContext = NewDbContext())
        {
            dbContext.Events.Add(new EventRecord
            {
                EventId = Guid.CreateVersion7(),
                Type = nameof(TransactionRecorded),
                OccurredAt = DateTime.UtcNow,
                Source = "test",
                Version = 1,
                Body = """{"transactionId":"not-a-number"}""",
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        await Process();

        await using var verify = NewDbContext();
        var stored = await verify.Events.SingleAsync();
        stored.Attempts.Should().Be(1);
        stored.LastError.Should().NotBeNullOrEmpty();
        stored.ProcessedAt.Should().BeNull("one failure is retried, not abandoned");
    }

    [Test]
    public async Task An_event_that_keeps_failing_is_eventually_abandoned()
    {
        await using (var dbContext = NewDbContext())
        {
            dbContext.Events.Add(new EventRecord
            {
                EventId = Guid.CreateVersion7(),
                Type = nameof(TransactionRecorded),
                OccurredAt = DateTime.UtcNow,
                Source = "test",
                Version = 1,
                Body = """{"transactionId":"not-a-number"}""",
                Attempts = EventRecord.MaxAttempts - 1,
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        await Process();

        await using var verify = NewDbContext();
        var stored = await verify.Events.SingleAsync();
        stored.Attempts.Should().Be(EventRecord.MaxAttempts);
        stored.ProcessedAt.Should().NotBeNull("one poisonous row must not block the queue forever");
        stored.LastError.Should().NotBeNullOrEmpty("the reason survives, so the row is its own dead letter");
    }


    private async Task<IReadOnlyList<NotificationDraft>> Evaluate(TransactionRecorded body)
    {
        await using var dbContext = NewDbContext();
        return await new BudgetExceededRule(dbContext).Evaluate(Event.Of(body, body.OccurredAt), default);
    }

    private async Task<int> Process()
    {
        await using var dbContext = NewDbContext();

        IEventDispatcher dispatcher = new EventDispatcher<TransactionRecorded>(
            [new BudgetExceededRule(dbContext)]);

        var processor = new EventProcessor(dbContext, [dispatcher], NullLogger<EventProcessor>.Instance);

        return await processor.ProcessBatch();
    }

    private async Task<Guid> EmitExpense(Seeded seeded, long minorUnits)
    {
        await using var dbContext = NewDbContext();

        var @event = Event.Of(Expense(minorUnits, seeded.CategoryId));
        await new EventBus(dbContext).Emit(@event);
        await dbContext.SaveChangesAsync();

        return @event.Attributes.EventId;
    }

    private static TransactionRecorded Expense(
        long amountMinorUnits,
        int categoryId,
        Currency currency = Currency.EUR) => new(
        TransactionId: 1,
        Kind: TransactionKind.Expense,
        AmountMinorUnits: amountMinorUnits,
        Currency: currency,
        OccurredAt: DateTime.UtcNow,
        CategoryId: categoryId,
        MerchantId: 1,
        SourceAccountNumber: SourceNumber,
        SourceBalanceAfterMinorUnits: 0,
        DestinationAccountNumber: null,
        DestinationBalanceAfterMinorUnits: null);

    private async Task<Seeded> Seed(long limitMinorUnits)
    {
        await using var dbContext = NewDbContext();

        var now = DateTime.UtcNow;
        var priority = new Priority { Label = "Normal", Weight = 1, CreatedAt = now };
        var category = new Category { Label = "Food", Priority = priority, CreatedAt = now };
        var merchant = new Merchant { Label = "Grocer", CreatedAt = now };
        var account = new Account
        {
            AccountNumber = SourceNumber,
            Label = "Cash",
            BalanceMinorUnits = 1_000_000,
            Currency = Currency.EUR,
            CreatedAt = now
        };
        var usdAccount = new Account
        {
            AccountNumber = "2000000000",
            Label = "USD Cash",
            BalanceMinorUnits = 1_000_000,
            Currency = Currency.USD,
            CreatedAt = now
        };

        dbContext.AddRange(priority, category, merchant, account, usdAccount);
        await dbContext.SaveChangesAsync();

        dbContext.Budgets.Add(Budget.For(
            category,
            Money.OfMinorUnits(limitMinorUnits),
            Recurrence.Monthly,
            FirstOfThisMonth(),
            endsOn: null));
        await dbContext.SaveChangesAsync();

        return new Seeded(category.Id, merchant.Id, account.Id, usdAccount.Id);
    }

    private Task SpendEur(Seeded seeded, long minorUnits) => Spend(seeded, minorUnits, Currency.EUR);

    private async Task Spend(Seeded seeded, long minorUnits, Currency currency)
    {
        await using var dbContext = NewDbContext();

        dbContext.Transactions.Add(new Transaction
        {
            AmountMinorUnits = minorUnits,
            Currency = currency,
            SourceAccountId = currency == Currency.EUR ? seeded.AccountId : seeded.UsdAccountId,
            CategoryId = seeded.CategoryId,
            MerchantId = seeded.MerchantId,
            Tags = [],
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    private static DateTime FirstOfThisMonth()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private sealed record Seeded(int CategoryId, int MerchantId, int AccountId, int UsdAccountId);
}

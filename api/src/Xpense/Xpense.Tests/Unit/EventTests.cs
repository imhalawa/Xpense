using FluentAssertions;
using Xpense.Domain.Enums;
using Xpense.Domain.Events;

namespace Xpense.Tests.Unit;

[TestFixture]
public class EventTests
{
    private static readonly DateTime OccurredAt = new(2026, 8, 6, 9, 30, 0, DateTimeKind.Utc);

    [Test]
    public void An_event_is_stamped_with_the_body_type_name()
    {
        var @event = Event.Of(Body());

        @event.Attributes.Type.Should().Be(nameof(TransactionRecorded));
        @event.Attributes.Version.Should().Be(Event.CurrentVersion);
        @event.Attributes.Source.Should().Be(Event.DefaultSource);
    }

    [Test]
    public void Every_event_gets_its_own_identity()
    {
        var first = Event.Of(Body());
        var second = Event.Of(Body());

        first.Attributes.EventId.Should().NotBe(second.Attributes.EventId);
        first.Attributes.EventId.Should().NotBe(Guid.Empty);
    }

    [Test]
    public async Task Event_ids_are_version_7_and_therefore_sort_by_creation()
    {
        var first = Event.Of(Body()).Attributes.EventId;
        await Task.Delay(5);
        var second = Event.Of(Body()).Attributes.EventId;

        Version(first).Should().Be(7);
        Version(second).Should().Be(7);
        string.CompareOrdinal(second.ToString(), first.ToString())
            .Should().BePositive("a later event should sort after an earlier one");
    }

    [Test]
    public void An_event_can_say_when_it_happened_rather_than_now()
    {
        Event.Of(Body(), OccurredAt).Attributes.OccurredAt.Should().Be(OccurredAt);
    }

    [Test]
    public void An_event_defaults_to_happening_now()
    {
        var before = DateTime.UtcNow;

        var @event = Event.Of(Body());

        @event.Attributes.OccurredAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }

    private static TransactionRecorded Body() => new(
        TransactionId: 1,
        Kind: TransactionKind.Expense,
        AmountMinorUnits: 1250,
        Currency: Currency.EUR,
        OccurredAt: OccurredAt,
        CategoryId: 3,
        MerchantId: 2,
        SourceAccountNumber: "1000000000",
        SourceBalanceAfterMinorUnits: 8750,
        DestinationAccountNumber: null,
        DestinationBalanceAfterMinorUnits: null);

    private static int Version(Guid value) => (value.ToByteArray()[7] & 0xF0) >> 4;
}

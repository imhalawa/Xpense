using FluentAssertions;
using Xpense.Services.ValueObjects;

namespace Xpense.Tests.Unit;

[TestFixture]
public class MoneyTests
{
    [Test]
    public void ToSingle_converts_cents_to_decimal_currency_units()
    {
        Money.OfCents(1234).ToSingle().Should().Be(12.34m);
    }
}

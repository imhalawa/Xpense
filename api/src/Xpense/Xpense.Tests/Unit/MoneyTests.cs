using FluentAssertions;
using Xpense.Domain.ValueObjects;

namespace Xpense.Tests.Unit;

[TestFixture]
public class MoneyTests
{
    [Test]
    public void ToDecimal_converts_minor_units_to_decimal_currency_units()
    {
        Money.OfMinorUnits(1234).ToDecimal().Should().Be(12.34m);
    }
}

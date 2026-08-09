using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xpense.Notifications;

namespace Xpense.Tests.Unit;

[TestFixture]
public class EventPumpTests
{
    [Test]
    public async Task Legacy_claim_mode_pauses_before_resolving_or_mutating_events()
    {
        var scopes = new CountingScopeFactory();
        var pump = new EventPump(
            scopes,
            NullLogger<EventPump>.Instance,
            Options.Create(new LegacyClaimOptions { Enabled = true }));

        await pump.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await pump.StopAsync(CancellationToken.None);

        scopes.Created.Should().Be(0);
    }

    private sealed class CountingScopeFactory : IServiceScopeFactory
    {
        public int Created { get; private set; }

        public IServiceScope CreateScope()
        {
            Created++;
            throw new InvalidOperationException("Claim mode must not create an event-processing scope.");
        }
    }
}

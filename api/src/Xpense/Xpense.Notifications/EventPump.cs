using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Xpense.Notifications;

public sealed class EventPump(
    IServiceScopeFactory scopes,
    ILogger<EventPump> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Event pump started");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<EventProcessor>();

                if (await processor.ProcessBatch(cancellationToken) < EventProcessor.BatchSize)
                    await Task.Delay(IdleDelay, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Event pump iteration failed");
                await Task.Delay(ErrorDelay, cancellationToken);
            }
        }

        logger.LogInformation("Event pump stopped");
    }
}

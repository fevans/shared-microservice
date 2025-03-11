using System.Text.Json;
using ECommerce.Shared.Infrastructure.EventBus;
using ECommerce.Shared.Infrastructure.EventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Shared.Infrastructure.Outbox;

public class OutboxBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<OutboxOptions> outboxOptions,
    ILogger<OutboxBackgroundService> logger)
    : BackgroundService
{
    private readonly TimeSpan _period = TimeSpan.FromSeconds(outboxOptions.Value.PublishIntervalInSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(_period);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            logger.LogInformation("Retrieving unpublished outbox events");
            using var serviceScope = serviceScopeFactory.CreateScope();
            var outboxStore = serviceScope.ServiceProvider.GetRequiredService<IOutboxStore>();
            var eventBus = serviceScope.ServiceProvider.GetRequiredService<IEventBus>();
            var unpublishedEvents = await outboxStore.GetUnpublishedOutboxEvents();
            foreach (var unpublishedEvent in unpublishedEvents)
            {
                var @event = JsonSerializer.Deserialize(unpublishedEvent.Data, Type.GetType(unpublishedEvent.EventType)) as Event;
                await eventBus.PublishAsync(@event);
                await outboxStore.MarkOutboxEventAsPublished(unpublishedEvent.Id);
            }

            logger.LogInformation(unpublishedEvents.Any()
                ? "Unpublished outbox events sent"
                : "No unpublished events to send");
        }
    }
}
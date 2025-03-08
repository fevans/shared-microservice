using System.Diagnostics;
using System.Text.Json;
using ECommerce.Shared.Infrastructure.EventBus;
using ECommerce.Shared.Infrastructure.EventBus.Abstractions;
using ECommerce.Shared.Observability;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;

namespace ECommerce.Shared.Infrastructure.RabbitMq;

public class RabbitMqEventBus(IRabbitMqConnection connection, RabbitMqTelemetry rabbitMqTelemetry) : IEventBus
{
    private const string ExchangeName = "ecommerce-exchange";
    private readonly ActivitySource _activitySource = rabbitMqTelemetry.ActivitySource;
    private readonly TextMapPropagator _propagator = Propagators.DefaultTextMapPropagator;

    private static void SetActivityContext(Activity? activity, string routingKey, string operation)
    {
        if (activity is not null)
        {
            activity.SetTag(OpenTelemetryMessagingConventions.System, "rabbitmq");
            activity.SetTag(OpenTelemetryMessagingConventions.OperationName, operation);
            activity.SetTag(OpenTelemetryMessagingConventions.DestinationName, routingKey);
        }
    }
    
    // Implement the PublishAsync method
    public async Task PublishAsync(Event @event)
    {
        var routingKey = "event.GetType().Name";
        await using var channel = await connection.Connection.CreateChannelAsync();
        var activityName = $"{OpenTelemetryMessagingConventions.PublishOperation} {routingKey}";
        using var activity = _activitySource.StartActivity(activityName, ActivityKind.Client);
        ActivityContext activityContextToInject = default;
        if (activity != null)
        {
            activityContextToInject = activity.Context;
        }
        
        var properties = new BasicProperties();
        _propagator.Inject(new PropagationContext(activityContextToInject, Baggage.Current), properties, 
            (properties, key, value) =>
            {
                properties.Headers ??= new Dictionary<string, object>();
                properties.Headers[key] = value;
            });
        SetActivityContext(activity, routingKey, OpenTelemetryMessagingConventions.PublishOperation);

        await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Fanout, false, autoDelete: false, null);
        var body = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType());
        await channel.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: string.Empty,
            mandatory: false,
            basicProperties: properties,
            body: body);
        
        //return Task.CompletedTask;
    }
    
    
}
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using ECommerce.Shared.Infrastructure.EventBus;
using ECommerce.Shared.Infrastructure.EventBus.Abstractions;
using ECommerce.Shared.Observability;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace ECommerce.Shared.Infrastructure.RabbitMq;

public class RabbitMqEventBus(IRabbitMqConnection connection, RabbitMqTelemetry rabbitMqTelemetry, IOptions<EventBusOptions> options) : IEventBus
{
    private const string ExchangeName = "ecommerce-exchange";
    private readonly ActivitySource _activitySource = rabbitMqTelemetry.ActivitySource;
    private readonly TextMapPropagator _propagator = Propagators.DefaultTextMapPropagator;
    private readonly ResiliencePipeline _pipeline = CreateResiliencePipeline(options.Value.RetryCount);

    private static void SetActivityContext(Activity? activity, string routingKey, string operation)
    {
        if (activity is not null)
        {
            activity.SetTag(OpenTelemetryMessagingConventions.System, "rabbitmq");
            activity.SetTag(OpenTelemetryMessagingConventions.OperationName, operation);
            activity.SetTag(OpenTelemetryMessagingConventions.DestinationName, routingKey);
        }
    }
    
    private static ResiliencePipeline CreateResiliencePipeline(int retryCount)
    {
        var retryOptions = new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder()
                .Handle<BrokerUnreachableException>()
                .Handle<SocketException>()
                .Handle<AlreadyClosedException>(),
            BackoffType = DelayBackoffType.Exponential,
            MaxRetryAttempts = retryCount
        };

        return new ResiliencePipelineBuilder()
            .AddRetry(retryOptions)
            .Build();
    }
    
    // Implement the PublishAsync method
    public async Task PublishAsync(Event @event)
    {
        var routingKey = @event.GetType().Name;
        await _pipeline.ExecuteAsync(async token  =>
        {
            await using var channel = await connection.Connection.CreateChannelAsync(cancellationToken: token);
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

            await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Fanout, false, autoDelete: false, null, cancellationToken: token);
            var body = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType());
            await channel.BasicPublishAsync(
                exchange: ExchangeName,
                routingKey: string.Empty,
                mandatory: false,
                basicProperties: properties,
                body: body, cancellationToken: token);

            //return Task.CompletedTask;
        }).ConfigureAwait(false);
    }
}
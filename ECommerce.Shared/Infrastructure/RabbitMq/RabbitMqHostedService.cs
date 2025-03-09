using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ECommerce.Shared.Infrastructure.EventBus;
using ECommerce.Shared.Infrastructure.EventBus.Abstractions;
using ECommerce.Shared.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client.Events;

namespace ECommerce.Shared.Infrastructure.RabbitMq;

/// <summary>
/// RabbitMqHostedService is responsible for managing the lifecycle of the RabbitMQ connection and message consumption.
/// It sets up the necessary RabbitMQ exchanges, queues, and bindings, and processes incoming messages by invoking the appropriate event handlers.
/// </summary>
public class RabbitMqHostedService(
    IServiceProvider serviceProvider,
    IOptions<EventHandlerRegistration> handlerRegistrations,
    IOptions<EventBusOptions> eventBusOptions,
    RabbitMqTelemetry telemetry)
    : IHostedService
{
    private const string ExchangeName = "ecommerce-exchange";
    private readonly EventHandlerRegistration _handlerRegistrations = handlerRegistrations.Value;
    private readonly EventBusOptions _eventBusOptions = eventBusOptions.Value;
    private readonly ActivitySource _activitySource = telemetry.ActivitySource;
    private readonly TextMapPropagator _propagator = Propagators.DefaultTextMapPropagator;
    
    /// <summary>
    /// Starts the RabbitMQ hosted service asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.Factory.StartNew(async () =>
        {
            var rabbitMqConnection = serviceProvider.GetRequiredService<IRabbitMqConnection>();
            
            var channel = await rabbitMqConnection.Connection.CreateChannelAsync(cancellationToken: cancellationToken);
            
            // Declare the exchange
            await channel.ExchangeDeclareAsync(
                exchange: ExchangeName,
                type: "fanout",
                durable: false,
                autoDelete: false,
                arguments: null, cancellationToken: cancellationToken);
             
            // Declare the queue
            await channel.QueueDeclareAsync(
                queue: _eventBusOptions.QueueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null, cancellationToken: cancellationToken);
                
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += OnMessageReceivedAsync;
            
            // Start consuming messages
            await channel.BasicConsumeAsync(
                queue: _eventBusOptions.QueueName,
                autoAck: true,
                consumer: consumer,
                consumerTag: string.Empty,
                noLocal: false,
                exclusive: false,
                arguments: null, cancellationToken: cancellationToken);
                
            // Bind the queue to the exchange for each event type
            foreach (var (eventName, _) in _handlerRegistrations.EventTypes)
            {
                await channel.QueueBindAsync(
                    queue: _eventBusOptions.QueueName,
                    exchange: ExchangeName,
                    routingKey: eventName,
                    arguments: null, cancellationToken: cancellationToken);
            }
        }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }
    
    /// <summary>
    /// Handles the received RabbitMQ message asynchronously.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="eventArgs">The event data containing the message.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs eventArgs)
    {
        var parentContext = _propagator.Extract(default, eventArgs.BasicProperties, (properties, key) =>
        {
            if (!properties.Headers.TryGetValue(key, out var value)) return [];
            return (value is byte[] bytes) ? [Encoding.UTF8.GetString(bytes)] : [];
        });
        var activityName = $"{OpenTelemetryMessagingConventions.ReceiveOperation} {eventArgs.RoutingKey}";
        using var activity = _activitySource.StartActivity(activityName, ActivityKind.Client, 
            parentContext.ActivityContext);
        SetActivityContext(activity, eventArgs.RoutingKey, OpenTelemetryMessagingConventions.ReceiveOperation);

         var eventName = eventArgs.RoutingKey;
         var message = Encoding.UTF8.GetString(eventArgs.Body.Span);
         activity?.SetTag("message", message);
     
         using var scope = serviceProvider.CreateScope();
     
         if (!_handlerRegistrations.EventTypes.TryGetValue(eventName, out var eventType))
         {
             return;
         }
     
         var @event = JsonSerializer.Deserialize(message, eventType) as Event;
         var handlers = scope.ServiceProvider.GetKeyedServices<IEventHandler>(eventType);
     
         foreach (var handler in handlers)
         {
             if (@event != null) await handler.Handle(@event);
         }
    }   

    /// <summary>
    /// Stops the RabbitMQ hosted service asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Sets the activity context for the given activity.
    /// </summary>
    /// <param name="activity">The activity to set the context for.</param>
    /// <param name="routingKey">The routing key of the message.</param>
    /// <param name="operation">The operation being performed (e.g., publish, receive).</param>
    private static void SetActivityContext(Activity? activity, string routingKey, string operation)
    {
        if (activity is null) return;
        activity.SetTag(OpenTelemetryMessagingConventions.System, "rabbitmq");
        activity.SetTag(OpenTelemetryMessagingConventions.OperationName, operation);
        activity.SetTag(OpenTelemetryMessagingConventions.DestinationName, routingKey);
    }
    
}
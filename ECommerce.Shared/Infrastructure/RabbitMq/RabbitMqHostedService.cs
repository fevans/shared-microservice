using System.Text;
using System.Text.Json;
using ECommerce.Shared.Infrastructure.EventBus;
using ECommerce.Shared.Infrastructure.EventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client.Events;

namespace ECommerce.Shared.Infrastructure.RabbitMq;

public class RabbitMqHostedService(
    IServiceProvider serviceProvider,
    IOptions<EventHandlerRegistration> handlerRegistrations,
    IOptions<EventBusOptions> eventBusOptions)
    : IHostedService
{
    private const string ExchangeName = "ecommerce-exchange";
    private readonly EventHandlerRegistration _handlerRegistrations = handlerRegistrations.Value;
    private readonly EventBusOptions _eventBusOptions = eventBusOptions.Value;
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.Factory.StartNew(async () =>
        {
            var rabbitMqConnection = serviceProvider.GetRequiredService<IRabbitMqConnection>();
            
            var channel = await rabbitMqConnection.Connection.CreateChannelAsync(cancellationToken: cancellationToken);
            
            await channel.ExchangeDeclareAsync(
                exchange: ExchangeName,
                type: "fanout",
                durable: false,
                autoDelete: false,
                arguments: null, cancellationToken: cancellationToken);
                
            await channel.QueueDeclareAsync(
                queue: _eventBusOptions.QueueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null, cancellationToken: cancellationToken);
                
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += OnMessageReceivedAsync;
            
            await channel.BasicConsumeAsync(
                queue: _eventBusOptions.QueueName,
                autoAck: true,
                consumer: consumer,
                consumerTag: string.Empty,
                noLocal: false,
                exclusive: false,
                arguments: null, cancellationToken: cancellationToken);
                
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
 private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs eventArgs)
 {
     var eventName = eventArgs.RoutingKey;
     var message = Encoding.UTF8.GetString(eventArgs.Body.Span);
 
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

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
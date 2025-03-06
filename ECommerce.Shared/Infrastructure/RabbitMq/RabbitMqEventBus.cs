using System.Text.Json;
using ECommerce.Shared.Infrastructure.EventBus;
using ECommerce.Shared.Infrastructure.EventBus.Abstractions;
using RabbitMQ.Client;

namespace ECommerce.Shared.Infrastructure.RabbitMq;

public class RabbitMqEventBus(IRabbitMqConnection connection) : IEventBus
{
    private const string ExchangeName = "ecommerce-exchange";

    // Implement the PublishAsync method
    public async Task PublishAsync(Event @event)
    {
        await using var channel = await connection.Connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Fanout, false, autoDelete: false, null);
        var body = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType());
        await channel.BasicPublishAsync( 
            exchange: ExchangeName,
            routingKey: string.Empty,
            
            body: body);
        
        //return Task.CompletedTask;
    }
    
    
}
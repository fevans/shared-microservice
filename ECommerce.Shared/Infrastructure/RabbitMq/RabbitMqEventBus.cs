using System.Text.Json;
using ECommerce.Shared.Infrastructure.EventBus;
using ECommerce.Shared.Infrastructure.EventBus.Abstractions;
using RabbitMQ.Client;

namespace ECommerce.Shared.Infrastructure.RabbitMq;

public class RabbitMqEventBus(IRabbitMqConnection connection) : IEventBus
{
    private const string ExchangeName = "ecommerce-exchange";

    // Implement the PublishAsync method
    public Task PublishAsync(Event @event)
    {
        using var channel = connection.Connection.CreateModel();
        channel.ExchangeDeclare(ExchangeName, ExchangeType.Fanout, false, autoDelete: false, null);
        var body = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType());
        channel.BasicPublish( 
            exchange: ExchangeName,
            routingKey: "",
            mandatory: false,
            basicProperties: null,
            body: body);
        
        return Task.CompletedTask;
    }
}
using ECommerce.Shared.Infrastructure.EventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Shared.Infrastructure.EventBus;

public static class EventBusHandlerExtensions
{
    public static IServiceCollection AddEventHander<TEvent, TEventHandler>(this IServiceCollection services)
        where TEvent : Event
        where TEventHandler : class, IEventHandler<TEvent>
    {
        services.AddKeyedTransient<IEventHandler, TEventHandler>(typeof(TEvent));
        services.Configure<EventHandlerRegistration>(o => o.EventTypes[typeof(TEvent).Name] = typeof(TEvent));
        
        return services;
    }
}
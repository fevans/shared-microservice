using System.Text.Json;
using ECommerce.Shared.Infrastructure.EventBus;
using ECommerce.Shared.Infrastructure.EventBus.Abstractions;
using ECommerce.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ECommerce.Shared.Infrastructure.Outbox;

internal class OutboxContext : DbContext, IOutboxStore
{
    public OutboxContext(DbContextOptions<OutboxContext> options)
        : base(options)
    {
    }

    public DbSet<OutboxEvent> OutboxEvents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OutboxEventConfiguration());
    }

    public async Task AddOutboxEvent<T>(T @event) where T : Event
    {
        var existingEvent = await OutboxEvents
            .FindAsync(@event.Id);
        if (existingEvent is null)
        {
            await OutboxEvents.AddAsync(new OutboxEvent
            {
                Id = @event.Id,
                EventType = @event.GetType().AssemblyQualifiedName,  //says AssemblyQualifiedName in text 
                Data = JsonSerializer.Serialize(@event)
            });
            await SaveChangesAsync();
        }
    }

    public async Task<List<OutboxEvent>> GetUnpublishedOutboxEvents() => await OutboxEvents
        .Where(e => !e.Sent)
        .ToListAsync();
    

    public async Task MarkOutboxEventAsPublished(Guid outboxEventId)
    {
       var outboxEvent = await OutboxEvents.FindAsync(outboxEventId);
       if (outboxEvent is not null)
       {
           outboxEvent.Sent = true;
           await SaveChangesAsync();
       }
    }

    public IExecutionStrategy CreateExecutionStrategy() => Database.CreateExecutionStrategy();
}
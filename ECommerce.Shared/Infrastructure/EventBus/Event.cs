namespace ECommerce.Shared.Infrastructure.EventBus;

public record Event
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
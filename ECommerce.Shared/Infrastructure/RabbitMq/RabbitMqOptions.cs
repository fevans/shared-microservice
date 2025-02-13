namespace ECommerce.Shared.Infrastructure.RabbitMq;

public class RabbitMqOptions
{
    //Create a property called HostName of type string
    public string HostName { get; set; } = string.Empty;

    // Create a const for RabbitMqSectionName of type string and set it to "RabbitMq"
    public const string RabbitMqSectionName = "RabbitMq";
}

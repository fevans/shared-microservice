namespace ECommerce.Shared.Observability;

public abstract class OpenTelemetryMessagingConventions
{
    public const string PublishOperation = "publish";
    public const string System = "messaging.system";
    public const string OperationName = "messaging.operation.name";
    public const string DestinationName = "messaging.destination.name";
    public const string ReceiveOperation = "receive";

}
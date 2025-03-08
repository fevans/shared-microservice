using ECommerce.Shared.Infrastructure.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ECommerce.Shared.Observability;

public static class OpenTelemetryStartupExtensions
{
    public static OpenTelemetryBuilder AddOpenTelemetryTracing(
        this IServiceCollection services, 
        string serviceName,
        Action<TracerProviderBuilder>? configureTracing = null)
    {
        return services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(serviceName))
            .WithTracing(builder =>
            {
                builder
                    .AddConsoleExporter()
                    .AddAspNetCoreInstrumentation()
                    .AddSource(RabbitMqTelemetry.ActivitySourceName);
                
                configureTracing?.Invoke(builder);
            });
    }
    public static TracerProviderBuilder WithSqlInstrumentation(this TracerProviderBuilder builder) => 
        builder.AddSqlClientInstrumentation();
}
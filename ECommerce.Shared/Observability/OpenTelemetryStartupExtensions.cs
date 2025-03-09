using ECommerce.Shared.Infrastructure.RabbitMq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ECommerce.Shared.Observability;

public static class OpenTelemetryStartupExtensions
{
    public static OpenTelemetryBuilder AddOpenTelemetryTracing(
        this IServiceCollection services, 
        IConfiguration configuration,
        string serviceName,
        Action<TracerProviderBuilder>? configureTracing = null)
    {
        var openTelemetryOptions = new OpenTelemetryOptions();
        configuration.GetSection(OpenTelemetryOptions.OpenTelemetrySectionName)
            .Bind(openTelemetryOptions);
        
        return services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(serviceName))
            .WithTracing(builder =>
            {
                builder
                    .AddConsoleExporter()
                    .AddAspNetCoreInstrumentation()
                    .AddSource(RabbitMqTelemetry.ActivitySourceName)
                    .AddOtlpExporter(options => options.Endpoint = new Uri(openTelemetryOptions.OtlpExporterEndpoint));
                
                configureTracing?.Invoke(builder);
            });
    }
    public static TracerProviderBuilder WithSqlInstrumentation(this TracerProviderBuilder builder) => 
        builder.AddSqlClientInstrumentation();
}
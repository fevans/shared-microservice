using ECommerce.Shared.Infrastructure.RabbitMq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
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
    
    public static OpenTelemetryBuilder AddOpenTelemetryMetrics(this OpenTelemetryBuilder openTelemetryBuilder,  
        string serviceName, 
        IServiceCollection services,
        Action<MeterProviderBuilder>? customMetrics = null)
    {
        services.AddSingleton(new MetricFactory(serviceName));
        return openTelemetryBuilder
            .WithMetrics(builder =>
            {
                builder
                    .AddConsoleExporter()
                    .AddAspNetCoreInstrumentation()
                    .AddMeter(serviceName)
                    .AddPrometheusExporter();
                
                customMetrics?.Invoke(builder);
            });
        
        
    }

    public static void UsePrometheusExporter(this WebApplication app) =>
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
}
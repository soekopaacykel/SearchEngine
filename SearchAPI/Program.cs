using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace SearchAPI;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        // Add OpenTelemetry metrics and logging
        var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
                resource.AddService(serviceName: "SearchAPI", serviceVersion: serviceVersion))
            .WithMetrics(metricsBuilder =>
            {
                metricsBuilder
                    .AddMeter("SearchAPI.Metrics")
                    // Ensure Prometheus-compatible histogram buckets for our custom latency metric
                    .AddView(
                        instrumentName: "search_latency_ms",
                        new ExplicitBucketHistogramConfiguration
                        {
                            Boundaries = new double[] { 1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000 }
                        }
                    )
                    .AddAspNetCoreInstrumentation()
                    .AddPrometheusExporter();
            });

        // Configure OpenTelemetry Logging
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.AddConsoleExporter();
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        // In Kubernetes we only expose HTTP (no TLS termination in the pod). Avoid forcing
        // HTTPS redirects which break health checks and Prometheus scrapes.
        if (app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }
        app.UseAuthorization();

        // Add Prometheus metrics endpoint
        app.UseOpenTelemetryPrometheusScrapingEndpoint();

        app.MapControllers();

        app.Run();
    }
}
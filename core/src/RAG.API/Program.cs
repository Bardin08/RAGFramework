using Microsoft.Extensions.Options;
using RAG.Application.Services;
using RAG.Core.Configuration;
using RAG.Infrastructure.Services;
using Serilog;
using Serilog.Events;

// Build initial configuration to read appsettings
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/rag-api-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Application starting...");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for logging
    builder.Host.UseSerilog();

    // Configure strongly-typed settings
    builder.Services.AddOptions<AppSettings>()
        .Bind(builder.Configuration)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.Configure<ElasticsearchSettings>(
        builder.Configuration.GetSection("Elasticsearch"));
    builder.Services.Configure<QdrantSettings>(
        builder.Configuration.GetSection("Qdrant"));
    builder.Services.Configure<EmbeddingServiceSettings>(
        builder.Configuration.GetSection("EmbeddingService"));
    builder.Services.Configure<OpenAISettings>(
        builder.Configuration.GetSection("OpenAI"));
    builder.Services.Configure<OllamaSettings>(
        builder.Configuration.GetSection("Ollama"));
    builder.Services.Configure<KeycloakSettings>(
        builder.Configuration.GetSection("Keycloak"));

    // Add services to the container.
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Register health check service
    builder.Services.AddHttpClient();
    builder.Services.AddMemoryCache();
    builder.Services.AddScoped<IHealthCheckService, HealthCheckService>();

    var app = builder.Build();

    // Validate and log configuration
    var appSettings = app.Services.GetRequiredService<IOptions<AppSettings>>().Value;
    Log.Information("Configuration loaded successfully");
    Log.Information("Elasticsearch URL: {ElasticsearchUrl}", appSettings.Elasticsearch.Url);
    Log.Information("Qdrant URL: {QdrantUrl}", appSettings.Qdrant.Url);
    Log.Information("Embedding Service URL: {EmbeddingServiceUrl}", appSettings.EmbeddingService.Url);
    Log.Information("OpenAI Model: {OpenAIModel}", appSettings.OpenAI.Model);
    Log.Information("Ollama URL: {OllamaUrl}, Model: {OllamaModel}", appSettings.Ollama.Url, appSettings.Ollama.Model);

    // Add Serilog request logging
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.GetLevel = (httpContext, elapsed, ex) => LogEventLevel.Information;
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
        };
    });

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    // Health check endpoints
    app.MapGet("/healthz", () => Results.Ok("OK"))
        .WithName("Liveness")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Liveness probe for Kubernetes";
            operation.Description = "Returns 200 OK if the application is running";
            return operation;
        })
        .Produces(200)
        .Produces(500);

    app.MapGet("/healthz/live", () => Results.Ok("OK"))
        .WithName("LivenessAlias")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Liveness probe alias";
            operation.Description = "Alternative liveness probe endpoint. Returns 200 OK if the application is running";
            return operation;
        })
        .Produces(200)
        .Produces(500);

    app.MapGet("/healthz/ready", async (IHealthCheckService healthService) =>
        {
            var health = await healthService.GetHealthStatusAsync();
            return health.Status == "Healthy" ? Results.Ok() : Results.StatusCode(503);
        })
        .WithName("Readiness")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Readiness probe for Kubernetes";
            operation.Description = "Returns 200 OK if all services are healthy, 503 Service Unavailable otherwise";
            return operation;
        })
        .Produces(200)
        .Produces(503);

    app.MapGet("/api/admin/health", async (IHealthCheckService healthService) =>
        {
            var health = await healthService.GetHealthStatusAsync();
            return Results.Ok(health);
        })
        .WithName("DetailedHealth")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Detailed health status of all services";
            operation.Description = "Returns detailed JSON with health status of all RAG system dependencies. Requires authentication in production.";
            return operation;
        })
        .Produces<RAG.Core.Domain.HealthStatus>(200)
        .Produces(401)
        .Produces(503);

    app.Run();
    Log.Information("Application stopped cleanly");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;

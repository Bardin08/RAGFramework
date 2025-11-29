using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Minio;
using RAG.API.Authentication;
using RAG.API.Filters;
using RAG.API.Middleware;
using RAG.Application.Interfaces;
using RAG.Application.Reranking;
using RAG.Application.Services;
using RAG.Core.Configuration;
using RAG.Infrastructure.Data;
using RAG.Infrastructure.Middleware;
using RAG.Infrastructure.Repositories;
using RAG.Infrastructure.Retrievers;
using RAG.Infrastructure.Services;
using RAG.Infrastructure.Storage;
using Serilog;
using Serilog.Events;

// Build initial configuration to read appsettings
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)  // Optional for testing
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
    builder.Services.Configure<MinIOSettings>(
        builder.Configuration.GetSection("MinIO"));
    builder.Services.Configure<ChunkingOptions>(
        builder.Configuration.GetSection("Chunking"));
    builder.Services.Configure<EmbeddingServiceOptions>(
        builder.Configuration.GetSection("EmbeddingService"));
    builder.Services.Configure<TextCleaningSettings>(
        builder.Configuration.GetSection("TextCleaning"));
    builder.Services.Configure<BM25Settings>(
        builder.Configuration.GetSection("BM25Settings"));
    builder.Services.Configure<DenseSettings>(
        builder.Configuration.GetSection("DenseSettings"));
    builder.Services.Configure<RetrievalSettings>(
        builder.Configuration.GetSection("RetrievalSettings"));
    builder.Services.Configure<HybridSearchConfig>(
        builder.Configuration.GetSection("HybridSearch"));
    builder.Services.Configure<RRFConfig>(
        builder.Configuration.GetSection("RRF"));

    // Configure authentication
    if (builder.Environment.IsDevelopment())
    {
        // Use test authentication in development
        builder.Services.AddAuthentication("TestScheme")
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                "TestScheme",
                options => { });

        builder.Services.AddAuthorization();

        Log.Warning("Using TEST AUTHENTICATION - DO NOT USE IN PRODUCTION");
        Log.Information("Test Token: {TestToken}", TestAuthenticationHandler.TestToken);
    }
    else
    {
        // TODO: Configure Keycloak JWT authentication for production
        throw new InvalidOperationException("Production authentication not yet configured. Please configure Keycloak JWT authentication.");
    }

    // Add services to the container.
    builder.Services.AddControllers();

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "RAG Architecture API",
            Version = "v1",
            Description = "Production-ready RAG framework for professional chatbots"
        });

        // Add XML comments for better documentation
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }

        // Add support for file uploads in Swagger UI
        options.OperationFilter<SwaggerFileOperationFilter>();

        // Add authentication support to Swagger UI
        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Enter test token: dev-test-token-12345"
        });

        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // Configure Npgsql to support dynamic JSON serialization
    var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("DefaultConnection"));
    dataSourceBuilder.EnableDynamicJson();
    var dataSource = dataSourceBuilder.Build();

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(dataSource));

    // Register MinIO SDK client
    builder.Services.AddSingleton<IMinioClient>(sp =>
    {
        var minioSettings = sp.GetRequiredService<IOptions<MinIOSettings>>().Value;
        return new MinioClient()
            .WithEndpoint(minioSettings.Endpoint)
            .WithCredentials(minioSettings.AccessKey, minioSettings.SecretKey)
            .WithSSL(minioSettings.UseSSL)
            .Build();
    });

    // Register MinIO client wrapper
    builder.Services.AddScoped<IMinIOClient, MinIOClient>();

    // Register application services
    builder.Services.AddScoped<IQueryProcessor, QueryProcessor>();
    builder.Services.AddScoped<BM25Retriever>(); // Registered as concrete class for factory pattern (Story 3.4)
    builder.Services.AddScoped<DenseRetriever>(); // Registered as concrete class for factory pattern (Story 3.4)
    builder.Services.AddScoped<HybridRetriever>(sp =>
    {
        // HybridRetriever depends on IRetriever (DIP), resolve concrete retrievers + RRF reranker
        var bm25 = sp.GetRequiredService<BM25Retriever>();
        var dense = sp.GetRequiredService<DenseRetriever>();
        var rrfReranker = sp.GetRequiredService<IRRFReranker>();
        var config = sp.GetRequiredService<IOptions<HybridSearchConfig>>();
        var logger = sp.GetRequiredService<ILogger<HybridRetriever>>();
        return new HybridRetriever(bm25, dense, rrfReranker, config, logger);
    }); // Story 4.2, Story 4.4
    builder.Services.AddScoped<AdaptiveRetriever>(sp =>
    {
        // AdaptiveRetriever depends on IQueryClassifier and three concrete retrievers
        var queryClassifier = sp.GetRequiredService<IQueryClassifier>();
        var bm25 = sp.GetRequiredService<BM25Retriever>();
        var dense = sp.GetRequiredService<DenseRetriever>();
        var hybrid = sp.GetRequiredService<HybridRetriever>();
        var logger = sp.GetRequiredService<ILogger<AdaptiveRetriever>>();
        return new AdaptiveRetriever(queryClassifier, bm25, dense, hybrid, logger);
    }); // Story 4.5
    builder.Services.AddScoped<RAG.Infrastructure.Factories.RetrievalStrategyFactory>(); // Factory for retrieval strategies (Story 3.4)
    builder.Services.AddScoped<IRRFReranker, RRFReranker>(); // Story 4.3
    builder.Services.AddScoped<IQueryClassifier, QueryClassifier>(); // Story 4.1, Story 4.5
    builder.Services.AddScoped<IFileValidationService, FileValidationService>();
    builder.Services.AddScoped<ITenantContext, TenantContext>();
    builder.Services.AddScoped<IFileUploadService, FileUploadService>();
    builder.Services.AddScoped<IDocumentStorageService, MinIODocumentStorageService>();
    builder.Services.AddSingleton<IHashService, Sha256HashService>();
    builder.Services.AddScoped<IDocumentHashRepository, DocumentHashRepository>();
    builder.Services.AddScoped<IChunkingStrategy, RAG.Infrastructure.Chunking.SlidingWindowChunker>();
    builder.Services.AddScoped<ISearchEngineClient, RAG.Infrastructure.SearchEngines.ElasticsearchClient>();
    builder.Services.AddScoped<IVectorStoreClient, RAG.Infrastructure.VectorStores.QdrantClient>();

    // Register individual text extractors (not as ITextExtractor to avoid circular dependency)
    builder.Services.AddScoped<RAG.Infrastructure.TextExtraction.TxtTextExtractor>();
    builder.Services.AddScoped<RAG.Infrastructure.TextExtraction.DocxTextExtractor>();
    builder.Services.AddScoped<RAG.Infrastructure.TextExtraction.PdfTextExtractor>();
    // Register composite extractor as the ITextExtractor implementation
    builder.Services.AddScoped<ITextExtractor, RAG.Infrastructure.TextExtraction.CompositeTextExtractor>();

    // Register text cleaning rules loader (singleton for caching)
    builder.Services.AddSingleton<RAG.Application.TextProcessing.TextCleaningRulesLoader>();

    // Register text cleaning strategies
    builder.Services.AddScoped<ITextCleaningStrategy, RAG.Application.TextProcessing.Strategies.UnicodeNormalizationStrategy>();
    builder.Services.AddScoped<ITextCleaningStrategy, RAG.Application.TextProcessing.Strategies.FormArtifactRemovalStrategy>();
    builder.Services.AddScoped<ITextCleaningStrategy, RAG.Application.TextProcessing.Strategies.WordSpacingFixStrategy>();
    builder.Services.AddScoped<ITextCleaningStrategy, RAG.Application.TextProcessing.Strategies.WhitespaceNormalizationStrategy>();
    builder.Services.AddScoped<ITextCleaningStrategy, RAG.Application.TextProcessing.Strategies.RepetitiveContentRemovalStrategy>();
    builder.Services.AddScoped<ITextCleaningStrategy, RAG.Application.TextProcessing.Strategies.TableFormattingCleanupStrategy>();
    builder.Services.AddScoped<ITextCleaningStrategy, RAG.Application.TextProcessing.Strategies.FinalCleanupStrategy>();

    // Register configurable text cleaner
    builder.Services.AddScoped<ITextCleanerService, RAG.Application.TextProcessing.ConfigurableTextCleaner>();

    builder.Services.AddScoped<IDocumentIndexingService, DocumentIndexingService>();
    builder.Services.AddScoped<IDocumentRepository, RAG.Infrastructure.Repositories.DocumentRepository>();
    builder.Services.AddScoped<IDocumentDeletionService, DocumentDeletionService>();
    builder.Services.AddHttpContextAccessor();

    // Register embedding service client with HttpClientFactory and Polly retry policies
    builder.Services.AddHttpClient<IEmbeddingService, RAG.Infrastructure.Clients.EmbeddingServiceClient>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<EmbeddingServiceOptions>>().Value;
        client.BaseAddress = new Uri(options.ServiceUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    })
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(1);
        options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
        options.Retry.OnRetry = args =>
        {
            Log.Warning(
                "Retry {RetryCount} for embedding service after {Delay}s due to {Exception}",
                args.AttemptNumber,
                args.RetryDelay.TotalSeconds,
                args.Outcome.Exception?.Message ?? "transient HTTP error");
            return ValueTask.CompletedTask;
        };
    });

    // Register health check service
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
    Log.Information("MinIO Endpoint: {MinIOEndpoint}, Bucket: {MinIOBucket}", appSettings.MinIO.Endpoint, appSettings.MinIO.BucketName);

    // Add exception handling middleware (must be early in pipeline)
    app.UseMiddleware<ExceptionHandlingMiddleware>();

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

    app.UseAuthentication();
    app.UseTenantContext();
    app.UseAuthorization();

    app.MapControllers();

    // Initialize Elasticsearch index and Qdrant collection on startup
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;

        try
        {
            Log.Information("Initializing Elasticsearch index...");
            var searchEngineClient = services.GetRequiredService<ISearchEngineClient>();
            await searchEngineClient.InitializeIndexAsync();
            Log.Information("Elasticsearch index initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize Elasticsearch index");
        }

        try
        {
            Log.Information("Initializing Qdrant collection...");
            var vectorStoreClient = services.GetRequiredService<IVectorStoreClient>();
            await vectorStoreClient.InitializeCollectionAsync();
            Log.Information("Qdrant collection initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize Qdrant collection");
        }
    }

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

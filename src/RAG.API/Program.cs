using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Minio;
using RAG.API.Authentication;
using RAG.API.Factories;
using RAG.API.Filters;
using RAG.API.Middleware;
using RAG.API.Validators;
using RAG.Application.Interfaces;
using RAG.Application.Reranking;
using RAG.Application.Services;
using RAG.Core.Configuration;
using RAG.Infrastructure.Authentication;
using RAG.Infrastructure.Authorization;
using RAG.Infrastructure.Data;
using RAG.Infrastructure.Middleware;
using RAG.Infrastructure.Repositories;
using RAG.Infrastructure.Retrievers;
using RAG.Infrastructure.Services;
using RAG.Infrastructure.Storage;
using RAG.Infrastructure.RateLimiting;
using AspNetCoreRateLimit;
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
        builder.Configuration.GetSection("LLMProviders:OpenAI"));
    builder.Services.Configure<OllamaSettings>(
        builder.Configuration.GetSection("LLMProviders:Ollama"));
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
    builder.Services.Configure<RAG.Infrastructure.Configuration.OpenAIOptions>(
        builder.Configuration.GetSection("LLMProviders:OpenAI"));
    builder.Services.Configure<RAG.Infrastructure.Configuration.OllamaOptions>(
        builder.Configuration.GetSection("LLMProviders:Ollama"));
    builder.Services.Configure<RAG.Application.Configuration.PromptTemplateSettings>(
        builder.Configuration.GetSection("PromptTemplates"));
    builder.Services.Configure<RAG.Application.Configuration.HallucinationDetectionSettings>(
        builder.Configuration.GetSection("HallucinationDetection"));
    builder.Services.Configure<ValidationSettings>(
        builder.Configuration.GetSection(ValidationSettings.SectionName));

    // Configure Rate Limiting Settings
    builder.Services.Configure<RateLimitSettings>(
        builder.Configuration.GetSection(RateLimitSettings.SectionName));

    // Configure CORS Settings
    builder.Services.Configure<CorsSettings>(
        builder.Configuration.GetSection(CorsSettings.SectionName));

    // Configure Authentication Settings
    builder.Services.Configure<AuthenticationSettings>(
        builder.Configuration.GetSection(AuthenticationSettings.SectionName));

    // Configure authentication based on environment
    if (builder.Environment.EnvironmentName == "Testing")
    {
        // Testing environment: Only TestScheme for integration tests
        builder.Services.AddAuthentication("TestScheme")
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                "TestScheme",
                options => { });

        builder.Services.AddRbacAuthorization();
        builder.Services.AddAuthorization(options => options.AddRbacPolicies());
    }
    else if (builder.Environment.IsDevelopment())
    {
        // Development environment: Support both TestScheme AND Keycloak JWT
        // This allows testing with simple test token OR real Keycloak tokens
        var authSettings = builder.Configuration
            .GetSection(AuthenticationSettings.SectionName)
            .Get<AuthenticationSettings>();

        var authBuilder = builder.Services.AddAuthentication(options =>
        {
            // Use a "selector" scheme that picks the right handler based on the token
            options.DefaultAuthenticateScheme = "DevelopmentSelector";
            options.DefaultChallengeScheme = "DevelopmentSelector";
        });

        // Add policy scheme that forwards to the appropriate handler
        authBuilder.AddPolicyScheme("DevelopmentSelector", "Development Auth Selector", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                // Check the Authorization header to determine which scheme to use
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader))
                {
                    return "TestScheme";
                }

                var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? authHeader.Substring(7)
                    : authHeader;

                // Known test tokens go to TestScheme
                var testTokens = new[] { "admin-token", "user-token", "no-role-token", "cross-tenant-token", "dev-test-token-12345" };
                if (testTokens.Contains(token.ToLowerInvariant()))
                {
                    return "TestScheme";
                }

                // JWT tokens (contain dots) go to JWT Bearer
                if (token.Contains('.'))
                {
                    return JwtBearerDefaults.AuthenticationScheme;
                }

                // Default to TestScheme
                return "TestScheme";
            };
        });

        // Add TestScheme
        authBuilder.AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
            "TestScheme",
            options => { });

        // Also add JWT Bearer for Keycloak if configured
        if (authSettings != null && !string.IsNullOrWhiteSpace(authSettings.Provider))
        {
            var loggerFactory = LoggerFactory.Create(loggingBuilder =>
            {
                loggingBuilder.AddSerilog();
            });
            var authProviderFactory = new AuthenticationProviderFactory(loggerFactory);
            var authProvider = authProviderFactory.Create(authSettings.Provider);

            authProvider.ConfigureAuthentication(authBuilder, builder.Configuration);

            // Register claims transformation if provider has one
            var claimsTransformation = authProvider.GetClaimsTransformation();
            if (claimsTransformation != null)
            {
                builder.Services.AddSingleton<IClaimsTransformation>(claimsTransformation);
            }

            Log.Information("Development mode: Both TestScheme and {Provider} JWT authentication enabled", authSettings.Provider);
        }

        // Add RBAC authorization handlers
        builder.Services.AddRbacAuthorization();

        // Add authorization policies
        builder.Services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.AddRbacPolicies();
        });

        Log.Warning("DEVELOPMENT MODE - Multiple auth schemes enabled");
        Log.Information("Test Token: {TestToken}", TestAuthenticationHandler.TestToken);
        Log.Information("Or use Keycloak JWT token from: http://localhost:8080/realms/rag/protocol/openid-connect/token");
    }
    else
    {
        // Production authentication: ONLY configured provider (Keycloak, Auth0, Azure AD)
        var authSettings = builder.Configuration
            .GetSection(AuthenticationSettings.SectionName)
            .Get<AuthenticationSettings>();

        if (authSettings == null || string.IsNullOrWhiteSpace(authSettings.Provider))
        {
            throw new InvalidOperationException(
                "Authentication configuration is required for production. " +
                "Please configure the 'Authentication' section in appsettings.json.");
        }

        Log.Information("Configuring authentication provider: {Provider}", authSettings.Provider);

        // Create authentication provider using factory
        var loggerFactory = LoggerFactory.Create(loggingBuilder =>
        {
            loggingBuilder.AddSerilog();
        });
        var authProviderFactory = new AuthenticationProviderFactory(loggerFactory);
        var authProvider = authProviderFactory.Create(authSettings.Provider);

        // Configure authentication using the provider
        var authBuilder = builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        });

        authProvider.ConfigureAuthentication(authBuilder, builder.Configuration);

        // Register claims transformation if provider has one
        var claimsTransformation = authProvider.GetClaimsTransformation();
        if (claimsTransformation != null)
        {
            builder.Services.AddSingleton<IClaimsTransformation>(claimsTransformation);
        }

        // Add RBAC authorization handlers and policies
        builder.Services.AddRbacAuthorization();
        builder.Services.AddAuthorization(options => options.AddRbacPolicies());

        Log.Information("Production authentication configured with provider: {Provider}", authSettings.Provider);
    }

    // Add services to the container.
    builder.Services.AddControllers();

    // Configure FluentValidation
    builder.Services.AddValidatorsFromAssemblyContaining<QueryRequestValidator>();
    builder.Services.AddFluentValidationAutoValidation(config =>
    {
        config.DisableDataAnnotationsValidation = true;
    });

    // Configure custom validation response format (RFC 7807 Problem Details)
    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    x => ToCamelCase(x.Key),
                    x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                );

            var correlationId = context.HttpContext.Request.Headers
                .TryGetValue("X-Correlation-ID", out var id) ? id.ToString() : Guid.NewGuid().ToString("N")[..12];

            var problemDetails = ProblemDetailsFactory.CreateValidationProblemDetails(
                errors,
                context.HttpContext.Request.Path,
                correlationId);

            return new BadRequestObjectResult(problemDetails)
            {
                ContentTypes = { "application/problem+json" }
            };
        };
    });

    // Configure API versioning
    builder.Services.Configure<ApiVersionSettings>(
        builder.Configuration.GetSection(ApiVersionSettings.SectionName));

    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "RAG Architecture API",
            Version = "v1",
            Description = @"Production-ready RAG (Retrieval-Augmented Generation) framework for professional chatbots.

## Authentication
This API uses **JWT Bearer** authentication with **Keycloak** as the identity provider.
Supports multi-tenant architecture with role-based access control.

### Available Roles
- **admin**: Full administrative access
- **user**: Standard user access (can query)
- **viewer**: Read-only access

### Test Users (Development)
| Username | Password | Roles |
|----------|----------|-------|
| admin | admin123 | admin, user |
| testuser | testuser123 | user |
| viewer | viewer123 | viewer |

## Features
- Multi-provider LLM support (OpenAI, Ollama)
- Hybrid search (BM25 + Dense retrieval)
- Streaming responses
- Multi-tenant document isolation

## Rate Limiting
This API implements rate limiting to protect resources and ensure fair usage.

### Rate Limit Tiers
| Tier | Limit | Description |
|------|-------|-------------|
| Anonymous | 100/min | Unauthenticated requests |
| Authenticated | 200/min | Users with valid JWT token |
| Admin | 500/min | Users with admin role |

### Rate Limit Headers
All responses include the following headers:
- `X-RateLimit-Limit`: Maximum requests allowed per time window
- `X-RateLimit-Remaining`: Remaining requests in current window
- `X-RateLimit-Reset`: Unix timestamp when the limit resets

### Rate Limit Exceeded (429)
When rate limit is exceeded, the API returns HTTP 429 with RFC 7807 Problem Details:
```json
{
  ""type"": ""https://api.rag.system/errors/rate-limit-exceeded"",
  ""title"": ""Rate limit exceeded"",
  ""status"": 429,
  ""detail"": ""You have exceeded the rate limit. Try again later."",
  ""retryAfter"": ""60""
}
```

## Validation Errors
All endpoints validate request parameters using FluentValidation.
Invalid requests return HTTP 400 with RFC 7807 Problem Details:
```json
{
  ""type"": ""https://api.rag.system/errors/validation-failed"",
  ""title"": ""Validation Failed"",
  ""status"": 400,
  ""errors"": {
    ""query"": [""Query cannot be empty""],
    ""topK"": [""TopK must be between 1 and 100""]
  },
  ""correlationId"": ""abc123def456"",
  ""timestamp"": ""2024-01-15T10:30:00.000Z""
}
```",
            Contact = new Microsoft.OpenApi.Models.OpenApiContact
            {
                Name = "RAG API Support",
                Email = "support@example.com"
            },
            License = new Microsoft.OpenApi.Models.OpenApiLicense
            {
                Name = "MIT License"
            }
        });

        // Add XML comments for better documentation
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }

        // Include XML comments from Application and Core assemblies
        var applicationXmlPath = Path.Combine(AppContext.BaseDirectory, "RAG.Application.xml");
        if (File.Exists(applicationXmlPath))
        {
            options.IncludeXmlComments(applicationXmlPath);
        }

        var coreXmlPath = Path.Combine(AppContext.BaseDirectory, "RAG.Core.xml");
        if (File.Exists(coreXmlPath))
        {
            options.IncludeXmlComments(coreXmlPath);
        }

        // Add support for file uploads in Swagger UI
        options.OperationFilter<SwaggerFileOperationFilter>();

        // Add authorization information to operation descriptions
        options.OperationFilter<AddAuthorizationHeaderOperationFilter>();

        // Add response headers documentation
        options.OperationFilter<AddResponseHeadersOperationFilter>();

        // Handle multiple routes with API versioning
        options.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

        // Configure operation tags for grouping endpoints by controller (ignore API version group)
        options.TagActionsBy(api =>
        {
            var controllerName = api.ActionDescriptor.RouteValues["controller"];
            return controllerName switch
            {
                "Query" => new[] { "Query" },
                "QueryStream" => new[] { "Query" },
                "Documents" => new[] { "Documents" },
                "Retrieval" => new[] { "Retrieval" },
                "HybridRetrieval" => new[] { "Retrieval" },
                "Health" => new[] { "Health" },
                _ => new[] { controllerName ?? "Other" }
            };
        });

        // Add tag descriptions
        options.DocumentFilter<SwaggerTagDescriptionsDocumentFilter>();

        // Remove non-versioned routes from documentation (show only /api/v1/... routes)
        options.DocumentFilter<RemoveNonVersionedRoutesDocumentFilter>();

        // Convert all routes to lowercase for consistency
        options.DocumentFilter<LowercaseRoutesDocumentFilter>();

        // Enable annotations for additional metadata
        options.EnableAnnotations();

        // Add authentication support to Swagger UI
        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = @"JWT Authorization header using the Bearer scheme.

**Production**: Obtain a token from Keycloak:
```
POST http://localhost:8080/realms/rag/protocol/openid-connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=password&client_id=rag-api&client_secret=rag-api-secret&username=testuser&password=testuser123
```

**Testing**: Use token: `dev-test-token-12345`"
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

    // Configure database context
    if (builder.Environment.EnvironmentName == "Testing")
    {
        // In test environment, register InMemory database
        // This satisfies service validation during Build()
        // TestWebApplicationFactory can then replace this if needed via ConfigureTestServices
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase("TestDb"));
    }
    else
    {
        // In non-test environments, use Npgsql with dynamic JSON support
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.EnableDynamicJson();
            var dataSource = dataSourceBuilder.Build();

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(dataSource));
        }
    }

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

    // Register LLM providers (Story 5.2, Story 5.3)
    builder.Services.AddSingleton<RAG.Infrastructure.LLMProviders.OpenAIProvider>();
    builder.Services.AddSingleton<RAG.Infrastructure.LLMProviders.OllamaProvider>();
    // Register a default LLM provider (can be selected via configuration)
    builder.Services.AddSingleton<RAG.Core.Interfaces.ILLMProvider>(sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var defaultProvider = config.GetValue<string>("LLMProviders:Default") ?? "OpenAI";

        return defaultProvider.ToLower() switch
        {
            "ollama" => sp.GetRequiredService<RAG.Infrastructure.LLMProviders.OllamaProvider>(),
            "openai" => sp.GetRequiredService<RAG.Infrastructure.LLMProviders.OpenAIProvider>(),
            _ => sp.GetRequiredService<RAG.Infrastructure.LLMProviders.OpenAIProvider>()
        };
    });
    // Register prompt template engine (Story 5.4)
    builder.Services.AddSingleton<RAG.Application.Interfaces.IPromptTemplateEngine, RAG.Application.Services.PromptTemplateEngine>();
    // Register hallucination detector (Story 5.6)
    builder.Services.AddScoped<RAG.Application.Interfaces.IHallucinationDetector, RAG.Application.Services.HallucinationDetector>();
    // Register token counter (Story 5.3 dependency)
    builder.Services.AddSingleton<RAG.Application.Services.ITokenCounter, RAG.Application.Services.ApproximateTokenCounter>();
    // Register context assembler (Story 5.3)
    builder.Services.AddScoped<RAG.Application.Services.IContextAssembler, RAG.Application.Services.ContextAssembler>();
    // Register response validator (Story 5.7)
    builder.Services.AddScoped<RAG.Application.Interfaces.IResponseValidator, RAG.Application.Services.ResponseValidator>();
    // Register source linker (Story 5.8)
    builder.Services.AddScoped<RAG.Application.Interfaces.ISourceLinker, RAG.Application.Services.SourceLinker>();
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

    // Register Admin services (Story 6.7)
    // Job queue for background processing - jobs are stored in database for persistence
    builder.Services.AddSingleton<System.Threading.Channels.Channel<RAG.Core.Domain.IndexRebuildJob>>(
        System.Threading.Channels.Channel.CreateUnbounded<RAG.Core.Domain.IndexRebuildJob>());
    builder.Services.AddScoped<RAG.Application.Interfaces.IAdminService, RAG.Infrastructure.Services.AdminService>();
    builder.Services.AddScoped<RAG.Application.Interfaces.IAuditLogService, RAG.Infrastructure.Services.AuditLogService>();
    builder.Services.AddHostedService<RAG.Infrastructure.BackgroundServices.IndexRebuildBackgroundService>();

    // Configure Rate Limiting (AspNetCoreRateLimit) - only if configuration exists and enabled
    // Tests can opt-out by setting environment variable DisableRateLimiting=true
    var ipRateLimitSection = builder.Configuration.GetSection("IpRateLimiting");
    var disableRateLimiting = builder.Configuration.GetValue<bool>("DisableRateLimiting");
    var enableEndpointRateLimiting = ipRateLimitSection.GetValue<bool>("EnableEndpointRateLimiting");
    var rateLimitingEnabled = !disableRateLimiting &&
                              ipRateLimitSection.Exists() &&
                              ipRateLimitSection.GetChildren().Any() &&
                              enableEndpointRateLimiting;

    if (rateLimitingEnabled)
    {
        // IP-based rate limiting
        builder.Services.Configure<IpRateLimitOptions>(ipRateLimitSection);
        builder.Services.Configure<IpRateLimitPolicies>(builder.Configuration.GetSection("IpRateLimitPolicies"));

        // Client-based rate limiting
        builder.Services.Configure<ClientRateLimitOptions>(builder.Configuration.GetSection("ClientRateLimiting"));
        builder.Services.Configure<ClientRateLimitPolicies>(builder.Configuration.GetSection("ClientRateLimitPolicies"));

        // Register rate limit stores and configuration
        builder.Services.AddInMemoryRateLimiting();
        builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

        // Register custom role-based client resolver for authenticated users
        builder.Services.AddSingleton<IClientResolveContributor, RoleBasedClientResolveContributor>();
    }

    // Configure CORS (Story 6.9)
    var corsSettings = builder.Configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>()
                       ?? new CorsSettings();

    // Fallback defaults when not configured in appsettings
    var allowedMethods = corsSettings.AllowedMethods.Length > 0
        ? corsSettings.AllowedMethods
        : new[] { "GET", "POST", "PUT", "DELETE", "OPTIONS", "PATCH" };

    var allowedHeaders = corsSettings.AllowedHeaders.Length > 0
        ? corsSettings.AllowedHeaders
        : new[] { "Content-Type", "Authorization", "X-Requested-With", "Accept", "Origin" };

    var exposedHeaders = corsSettings.ExposedHeaders.Length > 0
        ? corsSettings.ExposedHeaders
        : new[] { "X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset", "X-Request-Id", "api-supported-versions", "api-deprecated-versions" };

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            // Configure allowed origins
            if (corsSettings.AllowedOrigins.Length > 0)
            {
                policy.WithOrigins(corsSettings.AllowedOrigins);
            }
            else if (builder.Environment.IsDevelopment())
            {
                // Fallback for development: common localhost ports
                policy.WithOrigins(
                    "http://localhost:3000",
                    "http://localhost:5173",
                    "http://localhost:8080");
            }

            // Configure allowed methods
            policy.WithMethods(allowedMethods);

            // Configure allowed headers
            policy.WithHeaders(allowedHeaders);

            // Configure exposed headers (rate limit, API versioning, request ID)
            policy.WithExposedHeaders(exposedHeaders);

            // Configure credentials support
            if (corsSettings.AllowCredentials)
            {
                policy.AllowCredentials();
            }

            // Configure preflight cache duration
            policy.SetPreflightMaxAge(TimeSpan.FromSeconds(corsSettings.MaxAgeSeconds));
        });
    });

    var app = builder.Build();

    // Validate and log configuration (skip in test environments)
    if (app.Environment.EnvironmentName != "Testing")
    {
        try
        {
            var appSettings = app.Services.GetRequiredService<IOptions<AppSettings>>().Value;
            Log.Information("Configuration loaded successfully");
            Log.Information("Elasticsearch URL: {ElasticsearchUrl}", appSettings.Elasticsearch.Url);
            Log.Information("Qdrant URL: {QdrantUrl}", appSettings.Qdrant.Url);
            Log.Information("Embedding Service URL: {EmbeddingServiceUrl}", appSettings.EmbeddingService.Url);
            Log.Information("OpenAI Model: {OpenAIModel}", appSettings.OpenAI.Model);
            Log.Information("Ollama URL: {OllamaUrl}, Model: {OllamaModel}", appSettings.Ollama.Url, appSettings.Ollama.Model);
            Log.Information("MinIO Endpoint: {MinIOEndpoint}, Bucket: {MinIOBucket}", appSettings.MinIO.Endpoint, appSettings.MinIO.BucketName);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not load or validate AppSettings");
        }
    }

    // Add exception handling middleware (must be early in pipeline)
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // Add rate limiting middleware only if rate limiting is configured
    if (rateLimitingEnabled)
    {
        // Add custom rate limit exceeded response middleware (transforms 429 to RFC 7807)
        app.UseMiddleware<RateLimitExceededMiddleware>();

        // Add IP-based rate limiting middleware (before authentication)
        // Rate limiting is applied based on client IP address
        app.UseIpRateLimiting();
    }

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
        app.UseSwagger(options =>
        {
            options.RouteTemplate = "swagger/{documentName}/swagger.json";
        });

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "RAG API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "RAG Architecture API Documentation";

            // UI Enhancements
            options.DefaultModelsExpandDepth(2);
            options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
            options.EnableDeepLinking();
            options.DisplayRequestDuration();
            options.EnableFilter();
            options.ShowExtensions();

            // Try-it-out
            options.EnableTryItOutByDefault();

            // Persist authorization across page refreshes
            options.ConfigObject.AdditionalItems.Add("persistAuthorization", "true");
        });
    }

    // Add CORS middleware (Story 6.9)
    // Must be after routing setup, before authentication
    app.UseCors();

    app.UseAuthentication();
    app.UseTenantContext();
    app.UseAuthorization();

    app.MapControllers();

    // Initialize Elasticsearch index and Qdrant collection on startup (skip in test environments)
    if (app.Environment.EnvironmentName != "Testing")
    {
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
    }

    app.Run();
    Log.Information("Application stopped cleanly");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw; // Re-throw to make exceptions visible in tests
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program
{
    /// <summary>
    /// Converts a property name to camelCase.
    /// </summary>
    internal static string ToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        if (str.Length == 1) return str.ToLowerInvariant();
        return char.ToLowerInvariant(str[0]) + str[1..];
    }
}

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Application.Interfaces;
using RAG.Infrastructure.Data;
using RAG.Infrastructure.Storage;

namespace RAG.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory for integration tests.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set environment to Testing to skip startup initialization
        builder.UseEnvironment("Testing");

        // Configure test-specific settings BEFORE Program.cs runs
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add minimal test configuration to satisfy Program.cs validation
            var testConfig = new Dictionary<string, string?>
            {
                // Note: No connection string needed - Program.cs skips DbContext registration in Testing environment
                // TestWebApplicationFactory will register InMemoryDatabase instead

                ["Elasticsearch:Url"] = "http://localhost:9200",
                ["Elasticsearch:IndexName"] = "test-index",
                ["Elasticsearch:DefaultPageSize"] = "10",
                ["Qdrant:Url"] = "http://localhost:6333",
                ["Qdrant:CollectionName"] = "test-collection",
                ["Qdrant:VectorSize"] = "384",
                ["EmbeddingService:Url"] = "http://localhost:8000",
                ["EmbeddingService:ServiceUrl"] = "http://localhost:8000",
                ["EmbeddingService:TimeoutSeconds"] = "30",
                ["EmbeddingService:EmbeddingDimensions"] = "384",
                ["LLMProviders:OpenAI:ApiKey"] = "test-key",
                ["LLMProviders:OpenAI:Model"] = "gpt-3.5-turbo",
                ["LLMProviders:OpenAI:MaxTokens"] = "1000",
                ["LLMProviders:OpenAI:Temperature"] = "0.7",
                ["LLMProviders:Ollama:Url"] = "http://localhost:11434",
                ["LLMProviders:Ollama:Model"] = "llama2",
                ["Keycloak:Url"] = "http://localhost:8080",
                ["Keycloak:Realm"] = "test-realm",
                ["MinIO:Endpoint"] = "localhost:9000",
                ["MinIO:AccessKey"] = "test",
                ["MinIO:SecretKey"] = "test",
                ["MinIO:BucketName"] = "test",
                ["MinIO:UseSSL"] = "false",
                ["Chunking:MaxChunkSize"] = "500",
                ["Chunking:OverlapSize"] = "50",
                ["TextCleaning:RemoveFormArtifacts"] = "true",
                ["TextCleaning:NormalizeWhitespace"] = "true",
                ["BM25Settings:K1"] = "1.2",
                ["BM25Settings:B"] = "0.75",
                ["DenseSettings:TopK"] = "10",
                ["RetrievalSettings:MaxResults"] = "10",
                ["HybridSearch:BM25Weight"] = "0.5",
                ["HybridSearch:DenseWeight"] = "0.5",
                ["RRF:K"] = "60",
                ["LLMProviders:Default"] = "OpenAI",
                ["PromptTemplates:SystemPrompt"] = "Test",
                ["HallucinationDetection:Enabled"] = "false"
            };

            config.AddInMemoryCollection(testConfig);
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove the production database context if it exists
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Also remove the ApplicationDbContext registration itself
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ApplicationDbContext));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // Add in-memory database for testing
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase("InMemoryTestDb");
            });

            // Replace MinIO storage with in-memory storage for tests
            services.RemoveAll<IDocumentStorageService>();
            services.AddSingleton<IDocumentStorageService, InMemoryDocumentStorageService>();

            // Add test authentication scheme
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });

            // Build the service provider and ensure database is created
            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
        });
    }
}

/// <summary>
/// Test authentication handler that automatically authenticates requests with a tenant_id claim.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check if Authorization header is present
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            // No authorization header - request should be unauthorized
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Authorization header present - authenticate the request
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim("tenant_id", Guid.NewGuid().ToString())
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

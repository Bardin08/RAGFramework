using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using RAG.API.DTOs;
using RAG.Application.Interfaces;
using RAG.Application.Services;
using RAG.Core.Authorization;
using RAG.Core.Domain;
using RAG.Core.Enums;
using RAG.Core.Interfaces;
using RAG.Infrastructure.Factories;
using System.Security.Cryptography;
using System.Text;

namespace RAG.API.Controllers;

/// <summary>
/// Controller for non-streaming RAG query responses with caching.
/// </summary>
/// <remarks>
/// This endpoint requires JWT Bearer authentication.
/// Obtain a token from Keycloak using the password grant or client credentials flow.
/// Users must have the 'query' role to access this endpoint.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.UserOrAdmin)]
[Produces("application/json")]
public class QueryController : ControllerBase
{
    private readonly ILLMProvider _llmProvider;
    private readonly RetrievalStrategyFactory _retrievalStrategyFactory;
    private readonly ILogger<QueryController> _logger;
    private readonly IContextAssembler _contextAssembler;
    private readonly IPromptTemplateEngine _promptTemplateEngine;
    private readonly IMemoryCache _memoryCache;
    private readonly IResponseValidator? _responseValidator;

    public QueryController(
        ILLMProvider llmProvider,
        RetrievalStrategyFactory retrievalStrategyFactory,
        ILogger<QueryController> logger,
        IContextAssembler contextAssembler,
        IPromptTemplateEngine promptTemplateEngine,
        IMemoryCache memoryCache,
        IResponseValidator? responseValidator = null)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _retrievalStrategyFactory = retrievalStrategyFactory ?? throw new ArgumentNullException(nameof(retrievalStrategyFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextAssembler = contextAssembler ?? throw new ArgumentNullException(nameof(contextAssembler));
        _promptTemplateEngine = promptTemplateEngine ?? throw new ArgumentNullException(nameof(promptTemplateEngine));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _responseValidator = responseValidator;
    }

    /// <summary>
    /// Execute a RAG query and return the complete response with sources.
    /// </summary>
    /// <param name="request">The query request containing the question and optional parameters.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>Complete RAG response with answer, sources, and performance metadata.</returns>
    /// <response code="200">Returns the answer with sources and metadata</response>
    /// <response code="400">Validation errors in request parameters. Returns RFC 7807 Problem Details with field-level errors.</response>
    /// <response code="401">Missing or invalid authentication token</response>
    /// <response code="403">User lacks required permissions</response>
    /// <response code="500">Internal server error during processing</response>
    [HttpPost]
    [ProducesResponseType(typeof(QueryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<QueryResponse>> Query(
        [FromBody] QueryRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var overallStartTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation(
                "Processing query: Query='{Query}', Strategy={Strategy}, Provider={Provider}",
                request.Query.Length > 100 ? request.Query.Substring(0, 100) + "..." : request.Query,
                request.Strategy ?? "default",
                request.Provider ?? "default");

            // Step 1: Perform retrieval
            var retrievalStartTime = DateTime.UtcNow;

            var strategyType = ParseStrategyType(request.Strategy);
            var strategy = _retrievalStrategyFactory.CreateStrategy(strategyType);

            var tenantId = ParseTenantId(request.TenantId);

            var retrievalResults = await strategy.SearchAsync(
                request.Query,
                request.TopK ?? 10,
                tenantId,
                cancellationToken);

            var retrievalTime = DateTime.UtcNow - retrievalStartTime;

            // Step 2: Assemble context from retrieval results
            var contextStartTime = DateTime.UtcNow;
            var context = _contextAssembler.AssembleContext(retrievalResults);
            var contextTime = DateTime.UtcNow - contextStartTime;

            // Step 3: Check cache
            var cacheKey = GenerateCacheKey(request.Query, context);
            if (_memoryCache.TryGetValue<QueryResponse>(cacheKey, out var cachedResponse))
            {
                _logger.LogInformation("Cache hit for query: {CacheKey}", cacheKey);
                cachedResponse!.Metadata.FromCache = true;
                return Ok(cachedResponse);
            }

            // Step 4: Load prompt template
            var templateName = request.Template ?? "rag-answer-generation";
            var renderedPrompt = await _promptTemplateEngine.RenderTemplateAsync(
                templateName,
                new Dictionary<string, string>
                {
                    { "query", request.Query },
                    { "context", context }
                },
                cancellationToken: cancellationToken);

            // Step 5: Generate LLM response
            var generationStartTime = DateTime.UtcNow;

            var generationRequest = new GenerationRequest
            {
                Query = request.Query,
                Context = context,
                Temperature = request.Temperature.HasValue ? (decimal)request.Temperature.Value : (decimal)renderedPrompt.Parameters.Temperature,
                MaxTokens = request.MaxTokens ?? renderedPrompt.Parameters.MaxTokens,
                SystemPrompt = renderedPrompt.SystemPrompt
            };

            var generationResponse = await _llmProvider.GenerateAsync(generationRequest, cancellationToken);
            var generationTime = DateTime.UtcNow - generationStartTime;

            // Step 6: Validate response (if validator is available)
            var confidence = 0.0;
            if (_responseValidator != null)
            {
                var validationResult = _responseValidator.ValidateResponse(
                    generationResponse.Answer,
                    request.Query,
                    retrievalResults);
                confidence = (double)validationResult.RelevanceScore;

                _logger.LogInformation(
                    "Response validation: Confidence={Confidence}, IsValid={IsValid}, Citations={Citations}",
                    confidence,
                    validationResult.IsValid,
                    validationResult.CitationCount);
            }
            else
            {
                // Default confidence if no validator
                confidence = 0.85;
            }

            var totalTime = DateTime.UtcNow - overallStartTime;

            // Step 7: Build response
            var response = new QueryResponse
            {
                Answer = generationResponse.Answer,
                Sources = retrievalResults.Select(r => new SourceDto
                {
                    Id = r.DocumentId.ToString(),
                    Title = r.Source ?? "Unknown",
                    Excerpt = r.Text.Length > 200 ? r.Text.Substring(0, 200) + "..." : r.Text,
                    Score = r.Score
                }).ToList(),
                Metadata = new QueryMetadataDto
                {
                    RetrievalTime = $"{retrievalTime.TotalMilliseconds:F0}ms",
                    GenerationTime = $"{generationTime.TotalMilliseconds:F0}ms",
                    TotalTime = $"{totalTime.TotalMilliseconds:F0}ms",
                    TokensUsed = generationResponse.TokensUsed,
                    Model = generationResponse.Model,
                    Strategy = strategy.GetStrategyName(),
                    Confidence = confidence,
                    FromCache = false
                }
            };

            // Step 8: Cache the response (1 hour TTL)
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            };
            _memoryCache.Set(cacheKey, response, cacheOptions);

            _logger.LogInformation(
                "Query completed: TotalTime={TotalTime}ms, RetrievalTime={RetrievalTime}ms, GenerationTime={GenerationTime}ms, Confidence={Confidence}",
                totalTime.TotalMilliseconds,
                retrievalTime.TotalMilliseconds,
                generationTime.TotalMilliseconds,
                confidence);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing query: {Message}", ex.Message);
            return StatusCode(500, new { message = "An error occurred processing your query", error = ex.Message });
        }
    }

    /// <summary>
    /// Parse strategy type from string or return default.
    /// </summary>
    private RetrievalStrategyType ParseStrategyType(string? strategy)
    {
        if (string.IsNullOrEmpty(strategy))
        {
            return RetrievalStrategyType.Dense; // Default
        }

        if (Enum.TryParse<RetrievalStrategyType>(strategy, true, out var parsedType))
        {
            return parsedType;
        }

        _logger.LogWarning("Invalid strategy type: {Strategy}, using default Dense", strategy);
        return RetrievalStrategyType.Dense;
    }

    /// <summary>
    /// Parse tenant ID from string or return default.
    /// </summary>
    private Guid ParseTenantId(string? tenantId)
    {
        if (string.IsNullOrEmpty(tenantId))
        {
            return Guid.Empty; // Default tenant
        }

        if (Guid.TryParse(tenantId, out var parsedTenantId))
        {
            return parsedTenantId;
        }

        _logger.LogWarning("Invalid tenant ID: {TenantId}, using default", tenantId);
        return Guid.Empty;
    }

    /// <summary>
    /// Generate cache key from query and context using SHA256 hash.
    /// </summary>
    private string GenerateCacheKey(string query, string context)
    {
        var combined = $"{query}|{context}";
        var bytes = Encoding.UTF8.GetBytes(combined);
        var hash = SHA256.HashData(bytes);
        return $"query:{Convert.ToHexString(hash).ToLower()}";
    }
}

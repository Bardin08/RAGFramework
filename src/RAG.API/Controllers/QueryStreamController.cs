using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.API.DTOs;
using RAG.Application.Interfaces;
using RAG.Application.Services;
using RAG.Core.Authorization;
using RAG.Core.Domain;
using RAG.Core.Enums;
using RAG.Core.Interfaces;
using RAG.Infrastructure.Factories;
using System.Text;
using System.Text.Json;

namespace RAG.API.Controllers;

/// <summary>
/// Controller for streaming RAG query responses using Server-Sent Events (SSE).
/// </summary>
/// <remarks>
/// This endpoint requires JWT Bearer authentication.
/// Obtain a token from Keycloak using the password grant or client credentials flow.
/// Users must have the 'query' role to access this endpoint.
///
/// The streaming endpoint returns Server-Sent Events (SSE) with the following event types:
/// - metadata: Retrieval and context assembly timing information
/// - token: Individual tokens as they are generated
/// - done: Final response with sources and total timing
/// - error: Error information if something goes wrong
/// </remarks>
[ApiController]
[Route("api/query")]
[Authorize(Policy = AuthorizationPolicies.UserOrAdmin)]
public class QueryStreamController : ControllerBase
{
    private readonly ILLMProvider _llmProvider;
    private readonly RetrievalStrategyFactory _retrievalStrategyFactory;
    private readonly ILogger<QueryStreamController> _logger;
    private readonly IContextAssembler _contextAssembler;
    private readonly IPromptTemplateEngine _promptTemplateEngine;

    public QueryStreamController(
        ILLMProvider llmProvider,
        RetrievalStrategyFactory retrievalStrategyFactory,
        ILogger<QueryStreamController> logger,
        IContextAssembler contextAssembler,
        IPromptTemplateEngine promptTemplateEngine)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _retrievalStrategyFactory = retrievalStrategyFactory ?? throw new ArgumentNullException(nameof(retrievalStrategyFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextAssembler = contextAssembler ?? throw new ArgumentNullException(nameof(contextAssembler));
        _promptTemplateEngine = promptTemplateEngine ?? throw new ArgumentNullException(nameof(promptTemplateEngine));
    }

    /// <summary>
    /// Streams RAG query responses using Server-Sent Events (SSE).
    /// </summary>
    /// <param name="request">The streaming query request containing the question and optional parameters.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>SSE stream with tokens, metadata, and final results.</returns>
    /// <response code="200">SSE stream with tokens and metadata</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="401">Missing or invalid authentication token</response>
    /// <response code="403">User lacks required permissions</response>
    /// <response code="500">Internal server error during processing</response>
    [HttpPost("stream")]
    [Produces("text/event-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task StreamQuery(
        [FromBody] QueryStreamRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await SendSseEventAsync("error", new { message = "Invalid request", errors = ModelState }, cancellationToken);
            return;
        }

        // Set SSE headers
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no"); // Disable nginx buffering

        var overallStartTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation(
                "Starting streaming query: Query='{Query}', TenantId={TenantId}, Strategy={Strategy}",
                request.Query.Length > 100 ? request.Query.Substring(0, 100) + "..." : request.Query,
                request.TenantId ?? "default",
                request.Strategy ?? "default");

            // Step 1: Perform retrieval
            var retrievalStartTime = DateTime.UtcNow;

            // Parse strategy type from request or use default (Dense)
            var strategyType = RetrievalStrategyType.Dense; // Default
            if (!string.IsNullOrEmpty(request.Strategy) &&
                Enum.TryParse<RetrievalStrategyType>(request.Strategy, true, out var parsedType))
            {
                strategyType = parsedType;
            }

            var strategy = _retrievalStrategyFactory.CreateStrategy(strategyType);

            // Parse tenantId or use default
            var tenantId = Guid.Empty; // Default tenant
            if (!string.IsNullOrEmpty(request.TenantId) && Guid.TryParse(request.TenantId, out var parsedTenantId))
            {
                tenantId = parsedTenantId;
            }

            var retrievalResults = await strategy.SearchAsync(
                request.Query,
                request.TopK ?? 10,
                tenantId,
                cancellationToken);

            var retrievalTime = DateTime.UtcNow - retrievalStartTime;

            // Send retrieval metadata
            await SendSseEventAsync("metadata", new
            {
                retrievalTime = $"{retrievalTime.TotalMilliseconds:F0}ms",
                retrievalStrategy = strategy.GetStrategyName(),
                resultsCount = retrievalResults.Count
            }, cancellationToken);

            // Step 2: Assemble context from retrieval results
            var contextStartTime = DateTime.UtcNow;
            var context = _contextAssembler.AssembleContext(retrievalResults);
            var contextTime = DateTime.UtcNow - contextStartTime;

            // Send context metadata
            await SendSseEventAsync("metadata", new
            {
                contextAssemblyTime = $"{contextTime.TotalMilliseconds:F0}ms",
                contextLength = context.Length
            }, cancellationToken);

            // Step 3: Render prompt template
            var variables = new Dictionary<string, string>
            {
                { "context", context },
                { "query", request.Query }
            };

            var renderedPrompt = await _promptTemplateEngine.RenderTemplateAsync(
                "rag-answer-generation",
                variables,
                cancellationToken: cancellationToken);

            // Step 4: Create generation request
            var generationRequest = new GenerationRequest
            {
                Query = request.Query,
                Context = context,
                SystemPrompt = renderedPrompt.SystemPrompt,
                Temperature = (decimal)renderedPrompt.Parameters.Temperature,
                MaxTokens = renderedPrompt.Parameters.MaxTokens
            };

            // Step 5: Stream LLM response
            var generationStartTime = DateTime.UtcNow;
            var tokenStream = await _llmProvider.GenerateStreamAsync(generationRequest, cancellationToken);

            bool firstToken = true;
            var fullResponse = new StringBuilder();

            await foreach (var token in tokenStream.WithCancellation(cancellationToken))
            {
                if (firstToken)
                {
                    var timeToFirstToken = DateTime.UtcNow - generationStartTime;
                    _logger.LogInformation("Time to first token: {TimeMs}ms", timeToFirstToken.TotalMilliseconds);

                    await SendSseEventAsync("metadata", new
                    {
                        timeToFirstToken = $"{timeToFirstToken.TotalMilliseconds:F0}ms"
                    }, cancellationToken);

                    firstToken = false;
                }

                fullResponse.Append(token);

                // Send token event
                await SendSseEventAsync("token", new { token }, cancellationToken);
            }

            var generationTime = DateTime.UtcNow - generationStartTime;
            var totalTime = DateTime.UtcNow - overallStartTime;

            // Step 5: Send final "done" event with sources and timing
            await SendSseEventAsync("done", new
            {
                sources = retrievalResults.Select(r => new
                {
                    documentId = r.DocumentId,
                    source = r.Source,
                    score = r.Score,
                    text = r.Text.Length > 200 ? r.Text.Substring(0, 200) + "..." : r.Text
                }).ToList(),
                generationTime = $"{generationTime.TotalMilliseconds:F0}ms",
                totalTime = $"{totalTime.TotalMilliseconds:F0}ms",
                totalTokens = fullResponse.Length
            }, cancellationToken);

            _logger.LogInformation(
                "Streaming query completed: TotalTime={TotalTime}ms, GenerationTime={GenerationTime}ms",
                totalTime.TotalMilliseconds,
                generationTime.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Streaming query cancelled by client");
            await SendSseEventAsync("error", new { message = "Request cancelled" }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during streaming query: {Message}", ex.Message);
            await SendSseEventAsync("error", new { message = ex.Message, type = ex.GetType().Name }, CancellationToken.None);
        }
    }

    /// <summary>
    /// Sends an SSE event to the client.
    /// </summary>
    private async Task SendSseEventAsync(string eventType, object data, CancellationToken cancellationToken)
    {
        try
        {
            var jsonData = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var sseMessage = $"event: {eventType}\ndata: {jsonData}\n\n";
            var bytes = Encoding.UTF8.GetBytes(sseMessage);

            await Response.Body.WriteAsync(bytes, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SSE event: {EventType}", eventType);
            throw;
        }
    }
}

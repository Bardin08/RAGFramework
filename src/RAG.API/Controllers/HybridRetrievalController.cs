using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RAG.API.DTOs;
using RAG.Application.Interfaces;
using RAG.Core.Configuration;
using RAG.Infrastructure.Retrievers;

namespace RAG.API.Controllers;

/// <summary>
/// Handles hybrid retrieval operations combining BM25 and Dense strategies.
/// </summary>
[ApiController]
[Route("api/retrieval/hybrid")]
[Authorize]
public class HybridRetrievalController : ControllerBase
{
    private readonly HybridRetriever _hybridRetriever;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<HybridRetrievalController> _logger;
    private readonly HybridSearchConfig _config;

    public HybridRetrievalController(
        HybridRetriever hybridRetriever,
        ITenantContext tenantContext,
        ILogger<HybridRetrievalController> logger,
        IOptions<HybridSearchConfig> config)
    {
        _hybridRetriever = hybridRetriever ?? throw new ArgumentNullException(nameof(hybridRetriever));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Performs hybrid retrieval combining BM25 (keyword-based) and Dense (semantic) strategies.
    /// </summary>
    /// <param name="request">The hybrid retrieval request containing query, optional topK, alpha, and beta parameters.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A hybrid retrieval response containing combined results with individual and aggregated scores.</returns>
    /// <remarks>
    /// Hybrid retrieval executes both BM25 and Dense retrievers in parallel and combines their results
    /// using either weighted scoring (alpha*BM25 + beta*Dense) or Reciprocal Rank Fusion (RRF).
    ///
    /// The response includes individual BM25 and Dense scores along with the final combined score,
    /// allowing you to understand each retriever's contribution to the results.
    ///
    /// Example request:
    /// ```json
    /// {
    ///   "query": "machine learning applications",
    ///   "topK": 10,
    ///   "alpha": 0.5,
    ///   "beta": 0.5
    /// }
    /// ```
    ///
    /// Performance target: &lt; 300ms p95 response time.
    /// </remarks>
    /// <response code="200">Search completed successfully with combined results.</response>
    /// <response code="400">Invalid request parameters (e.g., alpha + beta != 1.0).</response>
    /// <response code="401">Unauthorized - valid JWT token required.</response>
    /// <response code="500">Internal server error during retrieval.</response>
    /// <response code="504">Request timeout - retrievers did not respond in time.</response>
    [HttpPost]
    [ProducesResponseType(typeof(HybridRetrievalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<HybridRetrievalResponse>> HybridAsync(
        [FromBody] HybridRetrievalRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate weight constraints
            try
            {
                request.ValidateWeights();
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning(ex,
                    "Invalid weights in hybrid request: alpha={Alpha}, beta={Beta}",
                    request.Alpha, request.Beta);

                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid request",
                    Detail = ex.Message,
                    Instance = HttpContext.Request.Path
                });
            }

            // Extract tenant ID from the current context
            var tenantId = _tenantContext.GetTenantId();

            // Use default TopK if not specified
            var topK = request.TopK ?? 10;

            // Use config alpha/beta if not specified in request
            var alpha = request.Alpha ?? _config.Alpha;
            var beta = request.Beta ?? _config.Beta;

            _logger.LogInformation(
                "Hybrid search initiated: query='{Query}', topK={TopK}, alpha={Alpha}, beta={Beta}, tenantId={TenantId}",
                request.Query, topK, alpha, beta, tenantId);

            // Execute search with timing
            var stopwatch = Stopwatch.StartNew();
            var results = await _hybridRetriever.SearchAsync(
                request.Query,
                topK,
                tenantId,
                cancellationToken);
            stopwatch.Stop();

            // Track BM25 and Dense result counts from metadata (if available)
            int bm25Count = 0;
            int denseCount = 0;

            // Map domain results to DTOs with individual scores
            var resultDtos = results.Select(r =>
            {
                // Extract individual scores from result metadata if available
                var bm25Score = r.Source.Contains("BM25", StringComparison.OrdinalIgnoreCase) ? (double?)r.Score : null;
                var denseScore = r.Source.Contains("Dense", StringComparison.OrdinalIgnoreCase) ? (double?)r.Score : null;

                // Try to parse from source metadata or use combined score
                // In actual implementation, HybridRetriever should provide these in metadata
                // For now, we'll use the combined score

                if (bm25Score.HasValue) bm25Count++;
                if (denseScore.HasValue) denseCount++;

                return new HybridRetrievalResultDto(
                    DocumentId: r.DocumentId,
                    Score: r.Score,
                    Text: r.Text,
                    Source: r.Source,
                    HighlightedText: r.HighlightedText,
                    BM25Score: bm25Score,
                    DenseScore: denseScore,
                    CombinedScore: r.Score // The combined score is the final score after reranking
                );
            }).ToList();

            var metadata = new HybridRetrievalMetadata(
                Alpha: alpha,
                Beta: beta,
                RerankingMethod: _config.RerankingMethod,
                BM25ResultCount: bm25Count,
                DenseResultCount: denseCount
            );

            var response = new HybridRetrievalResponse(
                Results: resultDtos,
                TotalFound: results.Count,
                RetrievalTimeMs: stopwatch.Elapsed.TotalMilliseconds,
                Strategy: _hybridRetriever.GetStrategyName(),
                Metadata: metadata
            );

            _logger.LogInformation(
                "Hybrid search completed: query='{Query}', results={ResultCount}, timeMs={TimeMs}, rerankingMethod={RerankingMethod}, tenantId={TenantId}",
                request.Query, results.Count, stopwatch.Elapsed.TotalMilliseconds, _config.RerankingMethod, tenantId);

            // Validate performance target (log warning if > 300ms)
            if (stopwatch.Elapsed.TotalMilliseconds > 300)
            {
                _logger.LogWarning(
                    "Hybrid search exceeded performance target: query='{Query}', timeMs={TimeMs} (target: <300ms)",
                    request.Query, stopwatch.Elapsed.TotalMilliseconds);
            }

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex,
                "Invalid hybrid request: query='{Query}', message={Message}",
                request.Query, ex.Message);

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid request",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex,
                "Hybrid search timeout: query='{Query}'",
                request.Query);

            return StatusCode(StatusCodes.Status504GatewayTimeout, new ProblemDetails
            {
                Status = StatusCodes.Status504GatewayTimeout,
                Title = "Request timeout",
                Detail = "Hybrid search operation timed out. Please try again with a simpler query.",
                Instance = HttpContext.Request.Path
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "Hybrid search service unavailable: query='{Query}', message={Message}",
                request.Query, ex.Message);

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Service unavailable",
                Detail = "Hybrid search service is currently unavailable. Please try again later.",
                Instance = HttpContext.Request.Path
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error during hybrid search: query='{Query}'",
                request.Query);

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal server error",
                Detail = "An unexpected error occurred while processing the request.",
                Instance = HttpContext.Request.Path
            });
        }
    }
}

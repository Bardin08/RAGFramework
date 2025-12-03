using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RAG.API.DTOs;
using RAG.Application.Interfaces;
using RAG.Core.Authorization;
using RAG.Core.Configuration;
using RAG.Infrastructure.Retrievers;

namespace RAG.API.Controllers;

/// <summary>
/// Handles retrieval operations for document search.
/// Exceptions are handled by ExceptionHandlingMiddleware.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.UserOrAdmin)]
public class RetrievalController : ControllerBase
{
    private readonly BM25Retriever _bm25Retriever;
    private readonly DenseRetriever _denseRetriever;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<RetrievalController> _logger;
    private readonly BM25Settings _bm25Settings;
    private readonly DenseSettings _denseSettings;

    public RetrievalController(
        BM25Retriever bm25Retriever,
        DenseRetriever denseRetriever,
        ITenantContext tenantContext,
        ILogger<RetrievalController> logger,
        IOptions<BM25Settings> bm25Settings,
        IOptions<DenseSettings> denseSettings)
    {
        _bm25Retriever = bm25Retriever ?? throw new ArgumentNullException(nameof(bm25Retriever));
        _denseRetriever = denseRetriever ?? throw new ArgumentNullException(nameof(denseRetriever));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _bm25Settings = bm25Settings?.Value ?? throw new ArgumentNullException(nameof(bm25Settings));
        _denseSettings = denseSettings?.Value ?? throw new ArgumentNullException(nameof(denseSettings));
    }

    /// <summary>
    /// Performs BM25 keyword-based retrieval for documents.
    /// </summary>
    /// <param name="request">The BM25 retrieval request containing the query and optional topK parameter.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A retrieval response containing matching documents ordered by relevance.</returns>
    /// <remarks>
    /// BM25 is a keyword-based retrieval algorithm that ranks documents based on term frequency and document length.
    /// It provides highlighted text showing matching query terms in context.
    /// </remarks>
    /// <response code="200">Search completed successfully.</response>
    /// <response code="400">Invalid request parameters.</response>
    /// <response code="401">Unauthorized - valid JWT token required.</response>
    /// <response code="500">Internal server error during retrieval.</response>
    [HttpPost("bm25")]
    [ProducesResponseType(typeof(RetrievalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RetrievalResponse>> SearchBM25Async(
        [FromBody] BM25RetrievalRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.GetTenantId();
        var topK = request.TopK ?? _bm25Settings.DefaultTopK;

        _logger.LogInformation(
            "BM25 search initiated: query='{Query}', topK={TopK}, tenantId={TenantId}",
            request.Query, topK, tenantId);

        var stopwatch = Stopwatch.StartNew();
        var results = await _bm25Retriever.SearchAsync(
            request.Query,
            topK,
            tenantId,
            cancellationToken);
        stopwatch.Stop();

        var resultDtos = results.Select(r => new RetrievalResultDto(
            DocumentId: r.DocumentId,
            Score: r.Score,
            Text: r.Text,
            Source: r.Source,
            HighlightedText: r.HighlightedText
        )).ToList();

        var response = new RetrievalResponse(
            Results: resultDtos,
            TotalFound: results.Count,
            RetrievalTimeMs: stopwatch.Elapsed.TotalMilliseconds,
            Strategy: _bm25Retriever.GetStrategyName()
        );

        _logger.LogInformation(
            "BM25 search completed: query='{Query}', results={ResultCount}, timeMs={TimeMs}, tenantId={TenantId}",
            request.Query, results.Count, stopwatch.Elapsed.TotalMilliseconds, tenantId);

        return Ok(response);
    }

    /// <summary>
    /// Performs Dense semantic retrieval for documents using vector embeddings.
    /// </summary>
    /// <param name="request">The Dense retrieval request containing the query, optional topK, and optional threshold.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A retrieval response containing semantically similar documents ordered by relevance.</returns>
    /// <remarks>
    /// Dense retrieval uses vector embeddings to find semantically similar documents, even when exact keywords don't match.
    /// </remarks>
    /// <response code="200">Search completed successfully.</response>
    /// <response code="400">Invalid request parameters.</response>
    /// <response code="401">Unauthorized - valid JWT token required.</response>
    /// <response code="500">Internal server error during retrieval.</response>
    [HttpPost("dense")]
    [ProducesResponseType(typeof(RetrievalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RetrievalResponse>> SearchDenseAsync(
        [FromBody] DenseRetrievalRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.GetTenantId();
        var topK = request.TopK ?? _denseSettings.DefaultTopK;

        _logger.LogInformation(
            "Dense search initiated: query='{Query}', topK={TopK}, threshold={Threshold}, tenantId={TenantId}",
            request.Query, topK, request.Threshold, tenantId);

        var stopwatch = Stopwatch.StartNew();
        var results = await _denseRetriever.SearchAsync(
            request.Query,
            topK,
            tenantId,
            cancellationToken);
        stopwatch.Stop();

        // Apply threshold filtering if specified
        if (request.Threshold.HasValue)
        {
            results = results.Where(r => r.Score >= request.Threshold.Value).ToList();
        }

        var resultDtos = results.Select(r => new RetrievalResultDto(
            DocumentId: r.DocumentId,
            Score: r.Score,
            Text: r.Text,
            Source: r.Source,
            HighlightedText: null // Dense retrieval does not provide highlighting
        )).ToList();

        var response = new RetrievalResponse(
            Results: resultDtos,
            TotalFound: results.Count,
            RetrievalTimeMs: stopwatch.Elapsed.TotalMilliseconds,
            Strategy: _denseRetriever.GetStrategyName()
        );

        _logger.LogInformation(
            "Dense search completed: query='{Query}', results={ResultCount}, timeMs={TimeMs}, tenantId={TenantId}",
            request.Query, results.Count, stopwatch.Elapsed.TotalMilliseconds, tenantId);

        return Ok(response);
    }
}

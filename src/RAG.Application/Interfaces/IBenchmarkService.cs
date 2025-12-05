using RAG.Core.DTOs.Benchmark;

namespace RAG.Application.Interfaces;

/// <summary>
/// Service interface for benchmark operations.
/// </summary>
public interface IBenchmarkService
{
    /// <summary>
    /// Creates a new benchmark job and queues it for execution.
    /// </summary>
    /// <param name="request">Benchmark request configuration.</param>
    /// <param name="userId">User ID of the requester.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Job response with ID and initial status.</returns>
    Task<BenchmarkJobResponse> CreateJobAsync(
        BenchmarkRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of a benchmark job.
    /// </summary>
    /// <param name="jobId">Unique job identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Job status information, or null if not found.</returns>
    Task<BenchmarkJobResponse?> GetJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the results of a completed benchmark job.
    /// </summary>
    /// <param name="jobId">Unique job identifier.</param>
    /// <param name="includeDetails">Whether to include per-sample detailed results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Benchmark results, or null if job not found or not completed.</returns>
    Task<BenchmarkResultsResponse?> GetResultsAsync(
        Guid jobId,
        bool includeDetails = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all benchmark jobs, optionally filtered by status or user.
    /// </summary>
    /// <param name="status">Optional status filter.</param>
    /// <param name="userId">Optional user ID filter.</param>
    /// <param name="limit">Maximum number of results (default 50).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of benchmark jobs.</returns>
    Task<List<BenchmarkJobResponse>> ListJobsAsync(
        string? status = null,
        string? userId = null,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports benchmark results to the specified format.
    /// </summary>
    /// <param name="jobId">Unique job identifier.</param>
    /// <param name="format">Export format (csv, json, md).</param>
    /// <param name="includePerQuery">Whether to include per-query breakdown.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Export data with content type and filename, or null if job not found.</returns>
    Task<ExportResult?> ExportResultsAsync(
        Guid jobId,
        string format,
        bool includePerQuery = false,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an export operation.
/// </summary>
public class ExportResult
{
    /// <summary>
    /// The exported data as bytes.
    /// </summary>
    public required byte[] Data { get; init; }

    /// <summary>
    /// MIME content type for the exported data.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Suggested filename for the download.
    /// </summary>
    public required string FileName { get; init; }
}

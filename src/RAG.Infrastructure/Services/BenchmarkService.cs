using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using RAG.Core.DTOs.Benchmark;
using RAG.Evaluation.Export;
using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;
using RAG.Infrastructure.Data;

namespace RAG.Infrastructure.Services;

/// <summary>
/// Implementation of benchmark service for creating and managing benchmark jobs.
/// </summary>
public class BenchmarkService : IBenchmarkService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly Channel<BenchmarkJob> _jobQueue;
    private readonly ILogger<BenchmarkService> _logger;

    public BenchmarkService(
        ApplicationDbContext dbContext,
        Channel<BenchmarkJob> jobQueue,
        ILogger<BenchmarkService> logger)
    {
        _dbContext = dbContext;
        _jobQueue = jobQueue;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<BenchmarkJobResponse> CreateJobAsync(
        BenchmarkRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating benchmark job for dataset: {Dataset}, User: {UserId}",
            request.Dataset,
            userId);

        // Serialize configuration
        var configJson = request.Configuration != null
            ? JsonSerializer.Serialize(request.Configuration)
            : "{}";

        // Create job entity
        var job = new BenchmarkJob
        {
            Dataset = request.Dataset,
            Configuration = configJson,
            SampleSize = request.SampleSize,
            Status = BenchmarkJobStatus.Queued.ToString(),
            InitiatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Save to database
        _dbContext.BenchmarkJobs.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Queue for background processing
        await _jobQueue.Writer.WriteAsync(job, cancellationToken);

        _logger.LogInformation(
            "Benchmark job created and queued. JobId: {JobId}, Dataset: {Dataset}",
            job.Id,
            job.Dataset);

        return MapToJobResponse(job, request.Configuration);
    }

    /// <inheritdoc/>
    public async Task<BenchmarkJobResponse?> GetJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Looking up benchmark job {JobId}", jobId);

        var job = await _dbContext.BenchmarkJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job == null)
        {
            _logger.LogWarning("Benchmark job not found: {JobId}", jobId);
            return null;
        }

        var config = string.IsNullOrEmpty(job.Configuration)
            ? null
            : JsonSerializer.Deserialize<BenchmarkConfiguration>(job.Configuration);

        return MapToJobResponse(job, config);
    }

    /// <inheritdoc/>
    public async Task<BenchmarkResultsResponse?> GetResultsAsync(
        Guid jobId,
        bool includeDetails = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving results for benchmark job {JobId}", jobId);

        var job = await _dbContext.BenchmarkJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job == null)
        {
            _logger.LogWarning("Benchmark job not found: {JobId}", jobId);
            return null;
        }

        // Only return results if job is completed
        if (job.Status != BenchmarkJobStatus.Completed.ToString())
        {
            _logger.LogWarning(
                "Job {JobId} is not completed. Status: {Status}",
                jobId,
                job.Status);
            return null;
        }

        if (string.IsNullOrEmpty(job.Results))
        {
            _logger.LogWarning("Job {JobId} has no results", jobId);
            return null;
        }

        // Deserialize results
        var results = JsonSerializer.Deserialize<BenchmarkResultsResponse>(job.Results);
        if (results == null)
        {
            _logger.LogError("Failed to deserialize results for job {JobId}", jobId);
            return null;
        }

        // Remove detailed results if not requested
        if (!includeDetails)
        {
            results.DetailedResults = null;
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<List<BenchmarkJobResponse>> ListJobsAsync(
        string? status = null,
        string? userId = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Listing benchmark jobs. Status: {Status}, UserId: {UserId}, Limit: {Limit}",
            status ?? "all",
            userId ?? "all",
            limit);

        var query = _dbContext.BenchmarkJobs.AsNoTracking();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(j => j.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(j => j.InitiatedBy == userId);
        }

        // Order by most recent first
        query = query.OrderByDescending(j => j.CreatedAt);

        // Apply limit
        var jobs = await query.Take(limit).ToListAsync(cancellationToken);

        _logger.LogDebug("Found {Count} benchmark jobs", jobs.Count);

        return jobs.Select(job =>
        {
            var config = string.IsNullOrEmpty(job.Configuration)
                ? null
                : JsonSerializer.Deserialize<BenchmarkConfiguration>(job.Configuration);
            return MapToJobResponse(job, config);
        }).ToList();
    }

    /// <summary>
    /// Maps a BenchmarkJob entity to a BenchmarkJobResponse DTO.
    /// </summary>
    private static BenchmarkJobResponse MapToJobResponse(
        BenchmarkJob job,
        BenchmarkConfiguration? config)
    {
        var response = new BenchmarkJobResponse
        {
            JobId = job.Id,
            Status = job.Status,
            Dataset = job.Dataset,
            Configuration = config,
            TotalSamples = job.TotalSamples,
            ProcessedSamples = job.ProcessedSamples,
            Progress = job.Progress,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            ErrorMessage = job.ErrorMessage,
            InitiatedBy = job.InitiatedBy
        };

        // Calculate estimated time remaining if running
        if (job.Status == BenchmarkJobStatus.Running.ToString() &&
            job.StartedAt.HasValue &&
            job.ProcessedSamples > 0 &&
            job.TotalSamples.HasValue &&
            job.TotalSamples > 0)
        {
            var elapsed = DateTimeOffset.UtcNow - job.StartedAt.Value;
            var avgTimePerSample = elapsed.TotalSeconds / job.ProcessedSamples.Value;
            var samplesRemaining = job.TotalSamples.Value - job.ProcessedSamples.Value;
            var estimatedSecondsRemaining = avgTimePerSample * samplesRemaining;

            response.EstimatedTimeRemaining = TimeSpan.FromSeconds(estimatedSecondsRemaining);
        }

        return response;
    }

    /// <inheritdoc/>
    public async Task<ExportResult?> ExportResultsAsync(
        Guid jobId,
        string format,
        bool includePerQuery = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Exporting benchmark results. JobId: {JobId}, Format: {Format}",
            jobId,
            format);

        // Get the job with results
        var job = await _dbContext.BenchmarkJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job == null)
        {
            _logger.LogWarning("Benchmark job not found: {JobId}", jobId);
            return null;
        }

        if (job.Status != BenchmarkJobStatus.Completed.ToString())
        {
            _logger.LogWarning(
                "Cannot export results for non-completed job {JobId}. Status: {Status}",
                jobId,
                job.Status);
            return null;
        }

        if (string.IsNullOrEmpty(job.Results))
        {
            _logger.LogWarning("Job {JobId} has no results to export", jobId);
            return null;
        }

        // Get the appropriate exporter
        var exporterFactory = new ResultsExporterFactory();
        IResultsExporter exporter;

        try
        {
            exporter = exporterFactory.GetExporter(format);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid export format: {Format}. Error: {Error}", format, ex.Message);
            return null;
        }

        // Parse the stored results
        var benchmarkResults = JsonSerializer.Deserialize<BenchmarkResultsResponse>(job.Results);
        if (benchmarkResults == null)
        {
            _logger.LogError("Failed to deserialize results for job {JobId}", jobId);
            return null;
        }

        // Convert to EvaluationReport for the exporter
        var evaluationReport = ConvertToEvaluationReport(benchmarkResults, job);

        // Export the results
        var exportOptions = new ExportOptions
        {
            IncludePerQueryBreakdown = includePerQuery,
            PrettyPrint = true,
            IncludePercentiles = true,
            IncludeConfiguration = true,
            DatasetName = job.Dataset
        };

        var exportedData = await exporter.ExportAsync(evaluationReport, exportOptions);

        var fileName = $"benchmark-{jobId:N}-{DateTimeOffset.UtcNow:yyyyMMdd}.{exporter.FileExtension}";

        _logger.LogInformation(
            "Successfully exported benchmark results. JobId: {JobId}, Format: {Format}, Size: {Size} bytes",
            jobId,
            format,
            exportedData.Length);

        return new ExportResult
        {
            Data = exportedData,
            ContentType = exporter.ContentType,
            FileName = fileName
        };
    }

    /// <summary>
    /// Converts BenchmarkResultsResponse to EvaluationReport for export.
    /// </summary>
    private static EvaluationReport ConvertToEvaluationReport(
        BenchmarkResultsResponse benchmarkResults,
        BenchmarkJob job)
    {
        // Convert MetricSummary to MetricStatistics
        var statistics = new Dictionary<string, MetricStatistics>();
        foreach (var (metricName, summary) in benchmarkResults.Metrics)
        {
            statistics[metricName] = new MetricStatistics(
                MetricName: summary.MetricName,
                Mean: summary.Mean,
                StandardDeviation: summary.StandardDeviation,
                Min: summary.Min,
                Max: summary.Max,
                SuccessCount: summary.SuccessCount,
                FailureCount: summary.FailureCount);
        }

        // Convert detailed results to EvaluationResult list
        var results = new List<EvaluationResult>();
        if (benchmarkResults.DetailedResults != null)
        {
            foreach (var sampleResult in benchmarkResults.DetailedResults)
            {
                foreach (var (metricName, value) in sampleResult.Metrics)
                {
                    results.Add(new EvaluationResult(
                        metricName,
                        value,
                        DateTimeOffset.UtcNow,
                        new Dictionary<string, object>
                        {
                            ["SampleId"] = sampleResult.SampleId,
                            ["Query"] = sampleResult.Query
                        })
                    {
                        Metadata = new Dictionary<string, object>
                        {
                            ["SampleId"] = sampleResult.SampleId,
                            ["Duration"] = sampleResult.Duration.TotalMilliseconds
                        }
                    });
                }
            }
        }

        // Build configuration dictionary
        var configuration = new Dictionary<string, object>
        {
            ["Dataset"] = benchmarkResults.Dataset,
            ["TotalSamples"] = benchmarkResults.TotalSamples,
            ["SuccessfulSamples"] = benchmarkResults.SuccessfulSamples,
            ["FailedSamples"] = benchmarkResults.FailedSamples
        };

        if (benchmarkResults.Configuration != null)
        {
            configuration["Strategy"] = benchmarkResults.Configuration.RetrievalStrategy ?? "default";
            configuration["TopK"] = benchmarkResults.Configuration.TopK ?? 10;
            configuration["Provider"] = benchmarkResults.Configuration.LlmProvider ?? "default";
        }

        return new EvaluationReport
        {
            RunId = benchmarkResults.JobId,
            StartedAt = benchmarkResults.StartedAt,
            CompletedAt = benchmarkResults.CompletedAt,
            SampleCount = benchmarkResults.TotalSamples,
            Results = results,
            Statistics = statistics,
            Configuration = configuration
        };
    }
}

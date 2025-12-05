using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RAG.Core.Domain;
using RAG.Core.DTOs.Benchmark;
using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;
using RAG.Evaluation.Services;
using RAG.Infrastructure.Data;

namespace RAG.Infrastructure.BackgroundServices;

/// <summary>
/// Background service for processing benchmark jobs.
/// Executes benchmarks using the evaluation framework and stores results in the database.
/// </summary>
public class BenchmarkBackgroundService : BackgroundService
{
    private readonly Channel<BenchmarkJob> _jobQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BenchmarkBackgroundService> _logger;

    public BenchmarkBackgroundService(
        Channel<BenchmarkJob> jobQueue,
        IServiceScopeFactory scopeFactory,
        ILogger<BenchmarkBackgroundService> logger)
    {
        _jobQueue = jobQueue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Benchmark background service started");

        await foreach (var job in _jobQueue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing benchmark job {JobId}", job.Id);
                await UpdateJobStatusAsync(
                    job.Id,
                    BenchmarkJobStatus.Failed.ToString(),
                    errorMessage: ex.Message);
            }
        }

        _logger.LogInformation("Benchmark background service stopped");
    }

    private async Task ProcessJobAsync(BenchmarkJob job, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Processing benchmark job {JobId} for dataset {Dataset}", job.Id, job.Dataset);

        // Update status to Running
        await UpdateJobStatusAsync(
            job.Id,
            BenchmarkJobStatus.Running.ToString(),
            startedAt: DateTimeOffset.UtcNow);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var seedDataLoader = scope.ServiceProvider.GetService<ISeedDataLoader>();
            var groundTruthLoader = scope.ServiceProvider.GetService<IGroundTruthLoader>();
            var metrics = scope.ServiceProvider.GetServices<IEvaluationMetric>();

            if (seedDataLoader == null)
            {
                throw new InvalidOperationException("Seed data loader is not available");
            }

            // Load the dataset
            _logger.LogInformation("Loading dataset {Dataset} for job {JobId}", job.Dataset, job.Id);

            // For benchmarks, we'll try to load ground truth data
            var datasetPath = Path.Combine("data", "seeds", $"{job.Dataset}.json");

            EvaluationDataset? dataset = null;

            // Try to load as ground truth first
            List<EvaluationContext> samples;

            if (groundTruthLoader != null && File.Exists(datasetPath) && groundTruthLoader.CanHandle(datasetPath))
            {
                _logger.LogInformation("Loading ground truth dataset from {Path}", datasetPath);
                var groundTruth = await groundTruthLoader.LoadAsync(datasetPath, stoppingToken);

                // Convert GroundTruthDataset to EvaluationDataset
                samples = groundTruth.Entries.Select(entry => new EvaluationContext
                {
                    SampleId = Guid.NewGuid().ToString(),
                    Query = entry.Query,
                    Response = entry.ExpectedAnswer, // Use expected answer as placeholder
                    GroundTruth = entry.ExpectedAnswer,
                    RelevantDocumentIds = entry.RelevantDocumentIds
                        .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
                        .Where(g => g != Guid.Empty)
                        .ToList(),
                    Parameters = entry.Metadata as IReadOnlyDictionary<string, object> ?? new Dictionary<string, object>()
                }).ToList();
            }
            else
            {
                // Fallback: create a simple dataset from seed data
                // This would require loading the seed data and creating evaluation contexts
                _logger.LogWarning("Ground truth loader not available or dataset not found. Creating minimal dataset.");
                samples = new List<EvaluationContext>();
            }

            // Apply sample size limit if specified
            if (job.SampleSize.HasValue && job.SampleSize.Value < samples.Count)
            {
                _logger.LogInformation(
                    "Limiting samples from {Total} to {Limit}",
                    samples.Count,
                    job.SampleSize.Value);
                samples = samples.Take(job.SampleSize.Value).ToList();
            }

            dataset = new EvaluationDataset
            {
                Name = job.Dataset,
                Version = "1.0",
                Samples = samples
            };

            var totalSamples = dataset.Samples.Count;
            await UpdateJobStatusAsync(job.Id, BenchmarkJobStatus.Running.ToString(), totalSamples: totalSamples);

            // Run the evaluation
            _logger.LogInformation(
                "Running evaluation for job {JobId} with {SampleCount} samples and {MetricCount} metrics",
                job.Id,
                dataset.Samples.Count,
                metrics.Count());

            var evaluationRunner = scope.ServiceProvider.GetRequiredService<EvaluationRunner>();
            var report = await evaluationRunner.RunAsync(dataset, stoppingToken);

            stopwatch.Stop();

            // Convert report to BenchmarkResultsResponse
            var results = ConvertReportToResults(job, report, stopwatch.Elapsed);
            var resultsJson = JsonSerializer.Serialize(results);

            // Update job with results
            await UpdateJobStatusAsync(
                job.Id,
                BenchmarkJobStatus.Completed.ToString(),
                results: resultsJson,
                completedAt: DateTimeOffset.UtcNow,
                processedSamples: totalSamples,
                progress: 100);

            _logger.LogInformation(
                "Benchmark job {JobId} completed successfully in {Duration}ms",
                job.Id,
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            await UpdateJobStatusAsync(job.Id, BenchmarkJobStatus.Failed.ToString(), errorMessage: "Job was cancelled");
            _logger.LogWarning("Benchmark job {JobId} was cancelled", job.Id);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await UpdateJobStatusAsync(
                job.Id,
                BenchmarkJobStatus.Failed.ToString(),
                errorMessage: ex.Message,
                completedAt: DateTimeOffset.UtcNow);
            _logger.LogError(ex, "Benchmark job {JobId} failed", job.Id);
            throw;
        }
    }

    private async Task UpdateJobStatusAsync(
        Guid jobId,
        string status,
        string? results = null,
        string? errorMessage = null,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? completedAt = null,
        int? totalSamples = null,
        int? processedSamples = null,
        int? progress = null)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var job = await dbContext.BenchmarkJobs.FindAsync(jobId);
            if (job != null)
            {
                job.Status = status;
                if (results != null) job.Results = results;
                if (errorMessage != null) job.ErrorMessage = errorMessage;
                if (startedAt.HasValue) job.StartedAt = startedAt.Value;
                if (completedAt.HasValue) job.CompletedAt = completedAt.Value;
                if (totalSamples.HasValue) job.TotalSamples = totalSamples.Value;
                if (processedSamples.HasValue) job.ProcessedSamples = processedSamples.Value;
                if (progress.HasValue) job.Progress = progress.Value;

                await dbContext.SaveChangesAsync();
                _logger.LogDebug("Updated job {JobId} status to {Status}", jobId, status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update job {JobId} status in database", jobId);
        }
    }

    private BenchmarkResultsResponse ConvertReportToResults(
        BenchmarkJob job,
        EvaluationReport report,
        TimeSpan duration)
    {
        var config = string.IsNullOrEmpty(job.Configuration)
            ? null
            : JsonSerializer.Deserialize<BenchmarkConfiguration>(job.Configuration);

        var metrics = new Dictionary<string, MetricSummary>();
        foreach (var (metricName, stats) in report.Statistics)
        {
            metrics[metricName] = new MetricSummary
            {
                MetricName = metricName,
                Mean = stats.Mean,
                StandardDeviation = stats.StandardDeviation,
                Min = stats.Min,
                Max = stats.Max,
                SuccessCount = stats.SuccessCount,
                FailureCount = stats.FailureCount
            };
        }

        // Convert detailed results if available
        var detailedResults = report.Results?
            .GroupBy(r => r.Metadata.TryGetValue("SampleId", out var sid) ? sid.ToString() : "unknown")
            .Select(g =>
            {
                var sampleResults = g.ToList();
                var firstResult = sampleResults.First();

                return new SampleResult
                {
                    SampleId = g.Key ?? "unknown",
                    Query = firstResult.Configuration.TryGetValue("Query", out var q) ? q.ToString() ?? "" : "",
                    Success = sampleResults.All(r => r.IsSuccess),
                    Metrics = sampleResults
                        .Where(r => r.IsSuccess)
                        .ToDictionary(r => r.MetricName, r => r.Value),
                    Duration = firstResult.Metadata != null &&
                               firstResult.Metadata.TryGetValue("DurationMs", out var dms)
                        ? TimeSpan.FromMilliseconds(Convert.ToDouble(dms))
                        : TimeSpan.Zero
                };
            })
            .ToList();

        return new BenchmarkResultsResponse
        {
            JobId = job.Id,
            Dataset = job.Dataset,
            Configuration = config,
            StartedAt = job.StartedAt ?? job.CreatedAt,
            CompletedAt = job.CompletedAt ?? DateTimeOffset.UtcNow,
            Duration = duration,
            TotalSamples = report.SampleCount,
            SuccessfulSamples = report.Results?.Count(r => r.IsSuccess) ?? 0,
            FailedSamples = report.Results?.Count(r => !r.IsSuccess) ?? 0,
            Metrics = metrics,
            DetailedResults = detailedResults,
            InitiatedBy = job.InitiatedBy
        };
    }
}

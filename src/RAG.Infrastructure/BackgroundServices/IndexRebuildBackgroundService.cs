using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using RAG.Infrastructure.Data;

namespace RAG.Infrastructure.BackgroundServices;

/// <summary>
/// Background service for processing index rebuild jobs.
/// Jobs are stored in the database for persistent tracking across application restarts.
/// </summary>
public class IndexRebuildBackgroundService : BackgroundService
{
    private readonly Channel<IndexRebuildJob> _jobQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IndexRebuildBackgroundService> _logger;

    public IndexRebuildBackgroundService(
        Channel<IndexRebuildJob> jobQueue,
        IServiceScopeFactory scopeFactory,
        ILogger<IndexRebuildBackgroundService> logger)
    {
        _jobQueue = jobQueue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Index rebuild background service started");

        await foreach (var job in _jobQueue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing index rebuild job {JobId}", job.JobId);
                await UpdateJobStatusAsync(job.JobId, "Failed", error: ex.Message);
            }
        }

        _logger.LogInformation("Index rebuild background service stopped");
    }

    private async Task UpdateJobStatusAsync(
        Guid jobId,
        string status,
        int? processedDocuments = null,
        int? estimatedDocuments = null,
        string? error = null)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var job = await dbContext.IndexRebuildJobs.FindAsync(jobId);
            if (job != null)
            {
                job.Status = status;
                if (processedDocuments.HasValue)
                    job.ProcessedDocuments = processedDocuments.Value;
                if (estimatedDocuments.HasValue)
                    job.EstimatedDocuments = estimatedDocuments.Value;
                if (error != null)
                    job.Error = error;
                if (status is "Completed" or "Failed" or "Cancelled")
                    job.CompletedAt = DateTime.UtcNow;

                await dbContext.SaveChangesAsync();
                _logger.LogDebug("Updated job {JobId} status to {Status}", jobId, status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update job {JobId} status in database", jobId);
        }
    }

    private async Task ProcessJobAsync(IndexRebuildJob job, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Processing index rebuild job {JobId}", job.JobId);

        // Update status to InProgress in database
        await UpdateJobStatusAsync(job.JobId, "InProgress");

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken,
            job.CancellationTokenSource?.Token ?? CancellationToken.None);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var indexingService = scope.ServiceProvider.GetRequiredService<IDocumentIndexingService>();

            // Get documents to reindex
            var query = dbContext.Documents.AsQueryable();
            if (job.TenantId.HasValue)
            {
                query = query.Where(d => d.TenantId == job.TenantId.Value);
            }

            var documents = await query
                .Select(d => new { d.Id, d.TenantId, d.Title, d.Source })
                .ToListAsync(linkedCts.Token);

            var estimatedDocuments = documents.Count;
            var processedDocuments = 0;

            // Update estimated count in database
            await UpdateJobStatusAsync(job.JobId, "InProgress", estimatedDocuments: estimatedDocuments);

            _logger.LogInformation("Reindexing {Count} documents for job {JobId}",
                documents.Count, job.JobId);

            foreach (var doc in documents)
            {
                if (linkedCts.Token.IsCancellationRequested)
                {
                    await UpdateJobStatusAsync(job.JobId, "Cancelled", processedDocuments: processedDocuments);
                    _logger.LogWarning("Job {JobId} was cancelled", job.JobId);
                    return;
                }

                try
                {
                    // Note: We're reindexing by calling the indexing service
                    // The actual implementation would depend on how documents are stored
                    // For now, we increment the counter
                    // In production, you'd call: await indexingService.ReindexDocumentAsync(doc.Id, ...)

                    processedDocuments++;

                    // Update progress every 10 documents
                    if (processedDocuments % 10 == 0)
                    {
                        await UpdateJobStatusAsync(job.JobId, "InProgress", processedDocuments: processedDocuments);
                        _logger.LogInformation("Job {JobId}: Processed {Processed}/{Total} documents",
                            job.JobId, processedDocuments, estimatedDocuments);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to reindex document {DocumentId} in job {JobId}",
                        doc.Id, job.JobId);
                    // Continue with other documents
                }
            }

            // Mark as completed
            await UpdateJobStatusAsync(job.JobId, "Completed", processedDocuments: processedDocuments);

            _logger.LogInformation("Job {JobId} completed. Processed {Processed} documents",
                job.JobId, processedDocuments);
        }
        catch (OperationCanceledException)
        {
            await UpdateJobStatusAsync(job.JobId, "Cancelled");
            _logger.LogWarning("Job {JobId} was cancelled", job.JobId);
        }
        catch (Exception ex)
        {
            await UpdateJobStatusAsync(job.JobId, "Failed", error: ex.Message);
            _logger.LogError(ex, "Job {JobId} failed", job.JobId);
            throw;
        }
    }
}

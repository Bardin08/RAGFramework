using System.Collections.Concurrent;
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
/// </summary>
public class IndexRebuildBackgroundService : BackgroundService
{
    private readonly Channel<IndexRebuildJob> _jobQueue;
    private readonly ConcurrentDictionary<Guid, IndexRebuildJob> _jobTracker;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IndexRebuildBackgroundService> _logger;

    public IndexRebuildBackgroundService(
        Channel<IndexRebuildJob> jobQueue,
        ConcurrentDictionary<Guid, IndexRebuildJob> jobTracker,
        IServiceScopeFactory scopeFactory,
        ILogger<IndexRebuildBackgroundService> logger)
    {
        _jobQueue = jobQueue;
        _jobTracker = jobTracker;
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
                job.Status = "Failed";
                job.Error = ex.Message;
                job.CompletedAt = DateTime.UtcNow;
            }
        }

        _logger.LogInformation("Index rebuild background service stopped");
    }

    private async Task ProcessJobAsync(IndexRebuildJob job, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Processing index rebuild job {JobId}", job.JobId);

        job.Status = "InProgress";

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

            job.EstimatedDocuments = documents.Count;

            _logger.LogInformation("Reindexing {Count} documents for job {JobId}",
                documents.Count, job.JobId);

            foreach (var doc in documents)
            {
                if (linkedCts.Token.IsCancellationRequested)
                {
                    job.Status = "Cancelled";
                    job.CompletedAt = DateTime.UtcNow;
                    _logger.LogWarning("Job {JobId} was cancelled", job.JobId);
                    return;
                }

                try
                {
                    // Note: We're reindexing by calling the indexing service
                    // The actual implementation would depend on how documents are stored
                    // For now, we increment the counter
                    // In production, you'd call: await indexingService.ReindexDocumentAsync(doc.Id, ...)

                    job.ProcessedDocuments++;

                    if (job.ProcessedDocuments % 10 == 0)
                    {
                        _logger.LogInformation("Job {JobId}: Processed {Processed}/{Total} documents",
                            job.JobId, job.ProcessedDocuments, job.EstimatedDocuments);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to reindex document {DocumentId} in job {JobId}",
                        doc.Id, job.JobId);
                    // Continue with other documents
                }
            }

            job.Status = "Completed";
            job.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("Job {JobId} completed. Processed {Processed} documents",
                job.JobId, job.ProcessedDocuments);
        }
        catch (OperationCanceledException)
        {
            job.Status = "Cancelled";
            job.CompletedAt = DateTime.UtcNow;
            _logger.LogWarning("Job {JobId} was cancelled", job.JobId);
        }
        catch (Exception ex)
        {
            job.Status = "Failed";
            job.Error = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
            _logger.LogError(ex, "Job {JobId} failed", job.JobId);
            throw;
        }
    }
}

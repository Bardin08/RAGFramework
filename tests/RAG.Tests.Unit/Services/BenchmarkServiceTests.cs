using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RAG.Core.Domain;
using RAG.Core.DTOs.Benchmark;
using RAG.Infrastructure.Data;
using RAG.Infrastructure.Services;
using Shouldly;

namespace RAG.Tests.Unit.Services;

/// <summary>
/// Unit tests for BenchmarkService.
/// </summary>
public class BenchmarkServiceTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly Channel<BenchmarkJob> _jobQueue;
    private readonly Mock<ILogger<BenchmarkService>> _loggerMock;
    private readonly BenchmarkService _service;

    public BenchmarkServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);

        // Setup job queue
        _jobQueue = Channel.CreateUnbounded<BenchmarkJob>();

        // Setup logger mock
        _loggerMock = new Mock<ILogger<BenchmarkService>>();

        // Create service
        _service = new BenchmarkService(_dbContext, _jobQueue, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task CreateJobAsync_WithValidRequest_CreatesJobAndQueuesIt()
    {
        // Arrange
        var request = new BenchmarkRequest
        {
            Dataset = "test-dataset",
            Configuration = new BenchmarkConfiguration
            {
                RetrievalStrategy = "Hybrid",
                TopK = 10
            },
            SampleSize = 50
        };
        var userId = "test-user";

        // Act
        var result = await _service.CreateJobAsync(request, userId);

        // Assert
        result.ShouldNotBeNull();
        result.JobId.ShouldNotBe(Guid.Empty);
        result.Status.ShouldBe(BenchmarkJobStatus.Queued.ToString());
        result.Dataset.ShouldBe("test-dataset");
        result.InitiatedBy.ShouldBe(userId);
        result.Configuration.ShouldNotBeNull();
        result.Configuration.RetrievalStrategy.ShouldBe("Hybrid");
        result.Configuration.TopK.ShouldBe(10);

        // Verify job was saved to database
        var savedJob = await _dbContext.BenchmarkJobs.FindAsync(result.JobId);
        savedJob.ShouldNotBeNull();
        savedJob!.Dataset.ShouldBe("test-dataset");
        savedJob.InitiatedBy.ShouldBe(userId);

        // Verify job was queued
        var queuedJob = await _jobQueue.Reader.ReadAsync();
        queuedJob.Id.ShouldBe(result.JobId);
    }

    [Fact]
    public async Task CreateJobAsync_WithoutConfiguration_UsesDefaultConfiguration()
    {
        // Arrange
        var request = new BenchmarkRequest
        {
            Dataset = "test-dataset"
        };
        var userId = "test-user";

        // Act
        var result = await _service.CreateJobAsync(request, userId);

        // Assert
        result.ShouldNotBeNull();
        result.JobId.ShouldNotBe(Guid.Empty);

        var savedJob = await _dbContext.BenchmarkJobs.FindAsync(result.JobId);
        savedJob.ShouldNotBeNull();
        savedJob!.Configuration.ShouldBe("{}");
    }

    [Fact]
    public async Task GetJobAsync_WithExistingJob_ReturnsJobResponse()
    {
        // Arrange
        var job = new BenchmarkJob
        {
            Id = Guid.NewGuid(),
            Dataset = "test-dataset",
            Configuration = "{\"retrievalStrategy\":\"Hybrid\"}",
            Status = BenchmarkJobStatus.Running.ToString(),
            InitiatedBy = "test-user",
            CreatedAt = DateTimeOffset.UtcNow,
            TotalSamples = 100,
            ProcessedSamples = 50,
            Progress = 50
        };

        _dbContext.BenchmarkJobs.Add(job);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetJobAsync(job.Id);

        // Assert
        result.ShouldNotBeNull();
        result!.JobId.ShouldBe(job.Id);
        result.Dataset.ShouldBe("test-dataset");
        result.Status.ShouldBe(BenchmarkJobStatus.Running.ToString());
        result.TotalSamples.ShouldBe(100);
        result.ProcessedSamples.ShouldBe(50);
        result.Progress.ShouldBe(50);
        result.InitiatedBy.ShouldBe("test-user");
    }

    [Fact]
    public async Task GetJobAsync_WithNonExistentJob_ReturnsNull()
    {
        // Arrange
        var nonExistentJobId = Guid.NewGuid();

        // Act
        var result = await _service.GetJobAsync(nonExistentJobId);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetResultsAsync_WithCompletedJob_ReturnsResults()
    {
        // Arrange
        var resultsJson = "{\"JobId\":\"00000000-0000-0000-0000-000000000001\",\"Dataset\":\"test\",\"TotalSamples\":10,\"SuccessfulSamples\":10,\"FailedSamples\":0,\"Metrics\":{\"Precision\":{\"MetricName\":\"Precision\",\"Mean\":0.85,\"StandardDeviation\":0.1,\"Min\":0.7,\"Max\":0.95,\"SuccessCount\":10,\"FailureCount\":0}}}";

        var job = new BenchmarkJob
        {
            Id = Guid.NewGuid(),
            Dataset = "test-dataset",
            Status = BenchmarkJobStatus.Completed.ToString(),
            Results = resultsJson,
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };

        _dbContext.BenchmarkJobs.Add(job);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetResultsAsync(job.Id, includeDetails: false);

        // Assert
        result.ShouldNotBeNull();
        result!.Dataset.ShouldBe("test");
        result.TotalSamples.ShouldBe(10);
        result.SuccessfulSamples.ShouldBe(10);
        result.FailedSamples.ShouldBe(0);
        result.Metrics.ShouldContainKey("Precision");
        result.Metrics["Precision"].Mean.ShouldBe(0.85);
        result.DetailedResults.ShouldBeNull(); // Not included by default
    }

    [Fact]
    public async Task GetResultsAsync_WithIncompleteJob_ReturnsNull()
    {
        // Arrange
        var job = new BenchmarkJob
        {
            Id = Guid.NewGuid(),
            Dataset = "test-dataset",
            Status = BenchmarkJobStatus.Running.ToString(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.BenchmarkJobs.Add(job);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetResultsAsync(job.Id);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetResultsAsync_WithNonExistentJob_ReturnsNull()
    {
        // Arrange
        var nonExistentJobId = Guid.NewGuid();

        // Act
        var result = await _service.GetResultsAsync(nonExistentJobId);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ListJobsAsync_WithNoFilters_ReturnsAllJobs()
    {
        // Arrange
        var jobs = new[]
        {
            new BenchmarkJob
            {
                Id = Guid.NewGuid(),
                Dataset = "dataset1",
                Status = BenchmarkJobStatus.Completed.ToString(),
                InitiatedBy = "user1",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-2)
            },
            new BenchmarkJob
            {
                Id = Guid.NewGuid(),
                Dataset = "dataset2",
                Status = BenchmarkJobStatus.Running.ToString(),
                InitiatedBy = "user2",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1)
            }
        };

        _dbContext.BenchmarkJobs.AddRange(jobs);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ListJobsAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        // Should be ordered by most recent first
        result[0].Dataset.ShouldBe("dataset2");
        result[1].Dataset.ShouldBe("dataset1");
    }

    [Fact]
    public async Task ListJobsAsync_WithStatusFilter_ReturnsFilteredJobs()
    {
        // Arrange
        var jobs = new[]
        {
            new BenchmarkJob
            {
                Id = Guid.NewGuid(),
                Dataset = "dataset1",
                Status = BenchmarkJobStatus.Completed.ToString(),
                CreatedAt = DateTimeOffset.UtcNow
            },
            new BenchmarkJob
            {
                Id = Guid.NewGuid(),
                Dataset = "dataset2",
                Status = BenchmarkJobStatus.Running.ToString(),
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        _dbContext.BenchmarkJobs.AddRange(jobs);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ListJobsAsync(status: BenchmarkJobStatus.Running.ToString());

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Status.ShouldBe(BenchmarkJobStatus.Running.ToString());
    }

    [Fact]
    public async Task ListJobsAsync_WithUserIdFilter_ReturnsFilteredJobs()
    {
        // Arrange
        var jobs = new[]
        {
            new BenchmarkJob
            {
                Id = Guid.NewGuid(),
                Dataset = "dataset1",
                Status = BenchmarkJobStatus.Completed.ToString(),
                InitiatedBy = "user1",
                CreatedAt = DateTimeOffset.UtcNow
            },
            new BenchmarkJob
            {
                Id = Guid.NewGuid(),
                Dataset = "dataset2",
                Status = BenchmarkJobStatus.Running.ToString(),
                InitiatedBy = "user2",
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        _dbContext.BenchmarkJobs.AddRange(jobs);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ListJobsAsync(userId: "user1");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].InitiatedBy.ShouldBe("user1");
    }

    [Fact]
    public async Task ListJobsAsync_WithLimit_ReturnsLimitedResults()
    {
        // Arrange
        var jobs = Enumerable.Range(1, 10).Select(i => new BenchmarkJob
        {
            Id = Guid.NewGuid(),
            Dataset = $"dataset{i}",
            Status = BenchmarkJobStatus.Completed.ToString(),
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-i)
        }).ToArray();

        _dbContext.BenchmarkJobs.AddRange(jobs);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ListJobsAsync(limit: 5);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(5);
    }
}

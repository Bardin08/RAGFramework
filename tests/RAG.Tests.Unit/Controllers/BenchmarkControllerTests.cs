using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using RAG.API.Controllers;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using RAG.Core.DTOs.Benchmark;
using Shouldly;
using System.Security.Claims;

namespace RAG.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for BenchmarkController.
/// </summary>
public class BenchmarkControllerTests
{
    private readonly Mock<IBenchmarkService> _benchmarkServiceMock;
    private readonly Mock<ITenantContext> _tenantContextMock;
    private readonly Mock<ILogger<BenchmarkController>> _loggerMock;
    private readonly BenchmarkController _controller;

    public BenchmarkControllerTests()
    {
        _benchmarkServiceMock = new Mock<IBenchmarkService>();
        _tenantContextMock = new Mock<ITenantContext>();
        _loggerMock = new Mock<ILogger<BenchmarkController>>();

        _controller = new BenchmarkController(
            _benchmarkServiceMock.Object,
            _tenantContextMock.Object,
            _loggerMock.Object);

        // Setup controller context with authenticated user
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "test-user"),
            new Claim(ClaimTypes.NameIdentifier, "test-user-id")
        }, "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        _tenantContextMock.Setup(x => x.GetUserId()).Returns(Guid.NewGuid());
    }

    [Fact]
    public async Task CreateBenchmark_WithValidRequest_ReturnsAcceptedWithJobResponse()
    {
        // Arrange
        var request = new BenchmarkRequest
        {
            Dataset = "test-dataset",
            Configuration = new BenchmarkConfiguration
            {
                RetrievalStrategy = "Hybrid",
                TopK = 10
            }
        };

        var expectedResponse = new BenchmarkJobResponse
        {
            JobId = Guid.NewGuid(),
            Status = BenchmarkJobStatus.Queued.ToString(),
            Dataset = "test-dataset",
            Configuration = request.Configuration,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _benchmarkServiceMock
            .Setup(x => x.CreateJobAsync(It.IsAny<BenchmarkRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.CreateBenchmark(request, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var acceptedResult = result.Result.ShouldBeOfType<AcceptedResult>();
        var response = acceptedResult.Value.ShouldBeOfType<BenchmarkJobResponse>();
        response.JobId.ShouldBe(expectedResponse.JobId);
        response.Status.ShouldBe(BenchmarkJobStatus.Queued.ToString());
        response.Dataset.ShouldBe("test-dataset");

        _benchmarkServiceMock.Verify(
            x => x.CreateJobAsync(request, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetJobStatus_WithExistingJob_ReturnsOkWithJobResponse()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var expectedResponse = new BenchmarkJobResponse
        {
            JobId = jobId,
            Status = BenchmarkJobStatus.Running.ToString(),
            Dataset = "test-dataset",
            Progress = 50,
            TotalSamples = 100,
            ProcessedSamples = 50,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _benchmarkServiceMock
            .Setup(x => x.GetJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetJobStatus(jobId, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var okResult = result.Result.ShouldBeOfType<OkObjectResult>();
        var response = okResult.Value.ShouldBeOfType<BenchmarkJobResponse>();
        response.JobId.ShouldBe(jobId);
        response.Status.ShouldBe(BenchmarkJobStatus.Running.ToString());
        response.Progress.ShouldBe(50);

        _benchmarkServiceMock.Verify(
            x => x.GetJobAsync(jobId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetJobStatus_WithNonExistentJob_ReturnsNotFound()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        _benchmarkServiceMock
            .Setup(x => x.GetJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BenchmarkJobResponse?)null);

        // Act
        var result = await _controller.GetJobStatus(jobId, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetResults_WithCompletedJob_ReturnsOkWithResults()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var expectedResults = new BenchmarkResultsResponse
        {
            JobId = jobId,
            Dataset = "test-dataset",
            TotalSamples = 100,
            SuccessfulSamples = 98,
            FailedSamples = 2,
            Duration = TimeSpan.FromMinutes(5),
            Metrics = new Dictionary<string, MetricSummary>
            {
                ["Precision"] = new MetricSummary
                {
                    MetricName = "Precision",
                    Mean = 0.85,
                    StandardDeviation = 0.1,
                    Min = 0.7,
                    Max = 0.95,
                    SuccessCount = 98,
                    FailureCount = 2
                }
            },
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            CompletedAt = DateTimeOffset.UtcNow
        };

        _benchmarkServiceMock
            .Setup(x => x.GetResultsAsync(jobId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var result = await _controller.GetResults(jobId, false, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var okResult = result.Result.ShouldBeOfType<OkObjectResult>();
        var response = okResult.Value.ShouldBeOfType<BenchmarkResultsResponse>();
        response.JobId.ShouldBe(jobId);
        response.TotalSamples.ShouldBe(100);
        response.SuccessfulSamples.ShouldBe(98);
        response.Metrics.ShouldContainKey("Precision");

        _benchmarkServiceMock.Verify(
            x => x.GetResultsAsync(jobId, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetResults_WithIncludeDetails_CallsServiceWithCorrectParameter()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var expectedResults = new BenchmarkResultsResponse
        {
            JobId = jobId,
            Dataset = "test-dataset",
            TotalSamples = 10,
            DetailedResults = new List<SampleResult>()
        };

        _benchmarkServiceMock
            .Setup(x => x.GetResultsAsync(jobId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var result = await _controller.GetResults(jobId, includeDetails: true, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var okResult = result.Result.ShouldBeOfType<OkObjectResult>();
        var response = okResult.Value.ShouldBeOfType<BenchmarkResultsResponse>();
        response.DetailedResults.ShouldNotBeNull();

        _benchmarkServiceMock.Verify(
            x => x.GetResultsAsync(jobId, true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetResults_WithNonExistentJob_ReturnsNotFound()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        _benchmarkServiceMock
            .Setup(x => x.GetResultsAsync(jobId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BenchmarkResultsResponse?)null);

        // Act
        var result = await _controller.GetResults(jobId, false, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ListJobs_WithNoFilters_ReturnsOkWithAllJobs()
    {
        // Arrange
        var expectedJobs = new List<BenchmarkJobResponse>
        {
            new BenchmarkJobResponse
            {
                JobId = Guid.NewGuid(),
                Status = BenchmarkJobStatus.Completed.ToString(),
                Dataset = "dataset1"
            },
            new BenchmarkJobResponse
            {
                JobId = Guid.NewGuid(),
                Status = BenchmarkJobStatus.Running.ToString(),
                Dataset = "dataset2"
            }
        };

        _benchmarkServiceMock
            .Setup(x => x.ListJobsAsync(null, null, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedJobs);

        // Act
        var result = await _controller.ListJobs(null, null, 50, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var okResult = result.Result.ShouldBeOfType<OkObjectResult>();
        var response = okResult.Value.ShouldBeOfType<List<BenchmarkJobResponse>>();
        response.Count.ShouldBe(2);

        _benchmarkServiceMock.Verify(
            x => x.ListJobsAsync(null, null, 50, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ListJobs_WithStatusFilter_ReturnsFilteredJobs()
    {
        // Arrange
        var status = BenchmarkJobStatus.Completed.ToString();
        var expectedJobs = new List<BenchmarkJobResponse>
        {
            new BenchmarkJobResponse
            {
                JobId = Guid.NewGuid(),
                Status = status,
                Dataset = "dataset1"
            }
        };

        _benchmarkServiceMock
            .Setup(x => x.ListJobsAsync(status, null, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedJobs);

        // Act
        var result = await _controller.ListJobs(status, null, 50, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        var okResult = result.Result.ShouldBeOfType<OkObjectResult>();
        var response = okResult.Value.ShouldBeOfType<List<BenchmarkJobResponse>>();
        response.Count.ShouldBe(1);
        response[0].Status.ShouldBe(status);

        _benchmarkServiceMock.Verify(
            x => x.ListJobsAsync(status, null, 50, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ListJobs_WithLimitAbove100_CapsAt100()
    {
        // Arrange
        var expectedJobs = new List<BenchmarkJobResponse>();

        _benchmarkServiceMock
            .Setup(x => x.ListJobsAsync(null, null, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedJobs);

        // Act
        var result = await _controller.ListJobs(null, null, 150, CancellationToken.None);

        // Assert
        _benchmarkServiceMock.Verify(
            x => x.ListJobsAsync(null, null, 100, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

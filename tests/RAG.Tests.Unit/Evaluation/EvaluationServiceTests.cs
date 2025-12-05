using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RAG.Application.Interfaces;
using RAG.Application.Services;
using RAG.Core.Domain;
using RAG.Core.DTOs.Evaluation;
using RAG.Core.Exceptions;
using Xunit;

namespace RAG.Tests.Unit.Evaluation;

public class EvaluationServiceTests
{
    private readonly Mock<IEvaluationRepository> _mockEvaluationRepo;
    private readonly Mock<IEvaluationRunRepository> _mockRunRepo;
    private readonly Mock<ILogger<EvaluationService>> _mockLogger;
    private readonly EvaluationService _service;

    public EvaluationServiceTests()
    {
        _mockEvaluationRepo = new Mock<IEvaluationRepository>();
        _mockRunRepo = new Mock<IEvaluationRunRepository>();
        _mockLogger = new Mock<ILogger<EvaluationService>>();

        _service = new EvaluationService(
            _mockEvaluationRepo.Object,
            _mockRunRepo.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task CreateEvaluationAsync_WithValidRequest_CreatesEvaluation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateEvaluationRequest
        {
            Name = "Test Evaluation",
            Description = "Test Description",
            Type = "retrieval",
            Config = JsonDocument.Parse("{\"metrics\": [\"precision\", \"recall\"]}").RootElement
        };

        _mockEvaluationRepo
            .Setup(r => r.IsNameUniqueAsync(request.Name, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockEvaluationRepo
            .Setup(r => r.CreateAsync(It.IsAny<Core.Domain.Evaluation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Core.Domain.Evaluation e, CancellationToken ct) => e);

        // Act
        var result = await _service.CreateEvaluationAsync(request, userId);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(request.Name);
        result.Description.Should().Be(request.Description);
        result.Type.Should().Be(request.Type);
        result.CreatedBy.Should().Be(userId);
        result.IsActive.Should().BeTrue();

        _mockEvaluationRepo.Verify(
            r => r.CreateAsync(It.IsAny<Core.Domain.Evaluation>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateEvaluationAsync_WithDuplicateName_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateEvaluationRequest
        {
            Name = "Existing Evaluation",
            Type = "retrieval",
            Config = JsonDocument.Parse("{}").RootElement
        };

        _mockEvaluationRepo
            .Setup(r => r.IsNameUniqueAsync(request.Name, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await _service.Invoking(s => s.CreateEvaluationAsync(request, userId))
            .Should().ThrowAsync<ConflictException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateEvaluationAsync_WithValidRequest_UpdatesEvaluation()
    {
        // Arrange
        var evaluationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingEvaluation = new Core.Domain.Evaluation
        {
            Id = evaluationId,
            Name = "Original Name",
            Type = "retrieval",
            Config = "{}",
            CreatedBy = userId,
            IsActive = true
        };

        var request = new UpdateEvaluationRequest
        {
            Name = "Updated Name",
            Description = "Updated Description"
        };

        _mockEvaluationRepo
            .Setup(r => r.GetByIdAsync(evaluationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEvaluation);

        _mockEvaluationRepo
            .Setup(r => r.IsNameUniqueAsync(request.Name!, evaluationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockEvaluationRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Core.Domain.Evaluation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Core.Domain.Evaluation e, CancellationToken ct) => e);

        // Act
        var result = await _service.UpdateEvaluationAsync(evaluationId, request, userId);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(request.Name);
        result.Description.Should().Be(request.Description);
        result.UpdatedBy.Should().Be(userId);
        result.UpdatedAt.Should().NotBeNull();

        _mockEvaluationRepo.Verify(
            r => r.UpdateAsync(It.IsAny<Core.Domain.Evaluation>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateEvaluationAsync_WithNonExistentEvaluation_ThrowsNotFoundException()
    {
        // Arrange
        var evaluationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new UpdateEvaluationRequest { Name = "New Name" };

        _mockEvaluationRepo
            .Setup(r => r.GetByIdAsync(evaluationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Core.Domain.Evaluation?)null);

        // Act & Assert
        await _service.Invoking(s => s.UpdateEvaluationAsync(evaluationId, request, userId))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteEvaluationAsync_WithExistingEvaluation_ReturnsTrue()
    {
        // Arrange
        var evaluationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _mockEvaluationRepo
            .Setup(r => r.GetByIdAsync(evaluationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Core.Domain.Evaluation { Id = evaluationId });

        _mockEvaluationRepo
            .Setup(r => r.DeleteAsync(evaluationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeleteEvaluationAsync(evaluationId, userId);

        // Assert
        result.Should().BeTrue();
        _mockEvaluationRepo.Verify(
            r => r.DeleteAsync(evaluationId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunEvaluationAsync_WithValidEvaluation_CreatesRun()
    {
        // Arrange
        var evaluationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = "test-tenant";
        var evaluation = new Core.Domain.Evaluation
        {
            Id = evaluationId,
            Name = "Test Evaluation",
            Type = "retrieval",
            Config = "{\"metrics\": [\"precision\"]}",
            IsActive = true
        };

        var request = new RunEvaluationRequest
        {
            Name = "Test Run"
        };

        _mockEvaluationRepo
            .Setup(r => r.GetByIdAsync(evaluationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(evaluation);

        _mockRunRepo
            .Setup(r => r.CreateAsync(It.IsAny<EvaluationRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EvaluationRun run, CancellationToken ct) => run);

        // Act
        var result = await _service.RunEvaluationAsync(evaluationId, request, userId, tenantId);

        // Assert
        result.Should().NotBeNull();
        result.EvaluationId.Should().Be(evaluationId);
        result.Name.Should().Be(request.Name);
        result.Status.Should().Be(EvaluationRunStatus.Queued);
        result.TenantId.Should().Be(tenantId);

        _mockRunRepo.Verify(
            r => r.CreateAsync(It.IsAny<EvaluationRun>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunEvaluationAsync_WithInactiveEvaluation_ThrowsValidationException()
    {
        // Arrange
        var evaluationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var evaluation = new Core.Domain.Evaluation
        {
            Id = evaluationId,
            IsActive = false
        };

        var request = new RunEvaluationRequest();

        _mockEvaluationRepo
            .Setup(r => r.GetByIdAsync(evaluationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(evaluation);

        // Act & Assert
        await _service.Invoking(s => s.RunEvaluationAsync(evaluationId, request, userId))
            .Should().ThrowAsync<ValidationException>()
            .WithMessage("*inactive*");
    }
}

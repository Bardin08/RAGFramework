using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;
using RAG.API.DTOs;
using RAG.API.Validators;
using RAG.Core.Configuration;
using Shouldly;

namespace RAG.Tests.Unit.Validators;

public class HybridRetrievalRequestValidatorTests
{
    private readonly HybridRetrievalRequestValidator _validator;
    private readonly ValidationSettings _settings;

    public HybridRetrievalRequestValidatorTests()
    {
        _settings = new ValidationSettings
        {
            MaxQueryLength = 5000,
            MaxHybridSearchLimit = 100
        };
        _validator = new HybridRetrievalRequestValidator(Options.Create(_settings));
    }

    [Fact]
    public async Task Validate_EmptyQuery_ShouldHaveError()
    {
        // Arrange
        var request = new HybridRetrievalRequest("");

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Query)
            .WithErrorMessage("Query cannot be empty");
    }

    [Fact]
    public async Task Validate_QueryTooLong_ShouldHaveError()
    {
        // Arrange
        var request = new HybridRetrievalRequest(new string('a', _settings.MaxQueryLength + 1));

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Query)
            .WithErrorMessage($"Query cannot exceed {_settings.MaxQueryLength} characters");
    }

    [Fact]
    public async Task Validate_ValidQuery_ShouldNotHaveError()
    {
        // Arrange
        var request = new HybridRetrievalRequest("What is machine learning?");

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Query);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_TopKBelowMinimum_ShouldHaveError(int topK)
    {
        // Arrange
        var request = new HybridRetrievalRequest("test", topK);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TopK);
    }

    [Fact]
    public async Task Validate_TopKAboveMaximum_ShouldHaveError()
    {
        // Arrange
        var request = new HybridRetrievalRequest("test", _settings.MaxHybridSearchLimit + 1);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TopK);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task Validate_TopKInRange_ShouldNotHaveError(int topK)
    {
        // Arrange
        var request = new HybridRetrievalRequest("test", topK);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.TopK);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public async Task Validate_AlphaOutOfRange_ShouldHaveError(double alpha)
    {
        // Arrange
        var request = new HybridRetrievalRequest("test", Alpha: alpha);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Alpha)
            .WithErrorMessage("Alpha must be between 0.0 and 1.0");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public async Task Validate_AlphaInRange_ShouldNotHaveError(double alpha)
    {
        // Arrange
        var request = new HybridRetrievalRequest("test", Alpha: alpha);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Alpha);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public async Task Validate_BetaOutOfRange_ShouldHaveError(double beta)
    {
        // Arrange
        var request = new HybridRetrievalRequest("test", Beta: beta);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Beta)
            .WithErrorMessage("Beta must be between 0.0 and 1.0");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public async Task Validate_BetaInRange_ShouldNotHaveError(double beta)
    {
        // Arrange
        var request = new HybridRetrievalRequest("test", Beta: beta);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Beta);
    }

    [Theory]
    [InlineData(0.3, 0.7)]
    [InlineData(0.5, 0.5)]
    [InlineData(0.0, 1.0)]
    [InlineData(1.0, 0.0)]
    [InlineData(0.25, 0.75)]
    public async Task Validate_AlphaPlusBetaEqualsOne_ShouldNotHaveError(double alpha, double beta)
    {
        // Arrange
        var request = new HybridRetrievalRequest("test", Alpha: alpha, Beta: beta);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0.3, 0.8)]
    [InlineData(0.5, 0.6)]
    [InlineData(0.2, 0.2)]
    public async Task Validate_AlphaPlusBetaNotEqualsOne_ShouldHaveError(double alpha, double beta)
    {
        // Arrange
        var request = new HybridRetrievalRequest("test", Alpha: alpha, Beta: beta);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.Errors.ShouldContain(e => e.ErrorMessage == "Alpha + Beta must equal 1.0");
    }

    [Fact]
    public async Task Validate_OnlyAlphaProvided_ShouldNotHaveWeightSumError()
    {
        // Arrange - only Alpha, no Beta
        var request = new HybridRetrievalRequest("test", Alpha: 0.7);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert - should not have weight sum error (only applies when both provided)
        result.Errors.ShouldNotContain(e => e.ErrorMessage == "Alpha + Beta must equal 1.0");
    }

    [Fact]
    public async Task Validate_OnlyBetaProvided_ShouldNotHaveWeightSumError()
    {
        // Arrange - only Beta, no Alpha
        var request = new HybridRetrievalRequest("test", Beta: 0.3);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert - should not have weight sum error (only applies when both provided)
        result.Errors.ShouldNotContain(e => e.ErrorMessage == "Alpha + Beta must equal 1.0");
    }

    [Fact]
    public async Task Validate_CompleteValidRequest_ShouldNotHaveErrors()
    {
        // Arrange
        var request = new HybridRetrievalRequest("What is neural network?", 10, 0.4, 0.6);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_MinimalValidRequest_ShouldNotHaveErrors()
    {
        // Arrange
        var request = new HybridRetrievalRequest("search query");

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}

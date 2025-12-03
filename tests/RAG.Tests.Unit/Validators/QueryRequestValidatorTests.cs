using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;
using RAG.API.DTOs;
using RAG.API.Validators;
using RAG.Core.Configuration;
using Shouldly;

namespace RAG.Tests.Unit.Validators;

public class QueryRequestValidatorTests
{
    private readonly QueryRequestValidator _validator;
    private readonly ValidationSettings _settings;

    public QueryRequestValidatorTests()
    {
        _settings = new ValidationSettings
        {
            MaxQueryLength = 5000,
            MaxTopK = 100
        };
        _validator = new QueryRequestValidator(Options.Create(_settings));
    }

    [Fact]
    public async Task Validate_EmptyQuery_ShouldHaveError()
    {
        // Arrange
        var request = new QueryRequest { Query = "" };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Query)
            .WithErrorMessage("Query cannot be empty");
    }

    [Fact]
    public async Task Validate_NullQuery_ShouldHaveError()
    {
        // Arrange
        var request = new QueryRequest { Query = null! };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Query);
    }

    [Fact]
    public async Task Validate_WhitespaceOnlyQuery_ShouldHaveError()
    {
        // Arrange
        var request = new QueryRequest { Query = "   " };

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
        var request = new QueryRequest { Query = new string('a', _settings.MaxQueryLength + 1) };

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
        var request = new QueryRequest { Query = "What is the capital of France?" };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Query);
    }

    [Fact]
    public async Task Validate_TopKBelowMinimum_ShouldHaveError()
    {
        // Arrange
        var request = new QueryRequest { Query = "test", TopK = 0 };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TopK)
            .WithErrorMessage($"TopK must be between 1 and {_settings.MaxTopK}");
    }

    [Fact]
    public async Task Validate_TopKAboveMaximum_ShouldHaveError()
    {
        // Arrange
        var request = new QueryRequest { Query = "test", TopK = _settings.MaxTopK + 1 };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TopK)
            .WithErrorMessage($"TopK must be between 1 and {_settings.MaxTopK}");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task Validate_TopKInRange_ShouldNotHaveError(int topK)
    {
        // Arrange
        var request = new QueryRequest { Query = "test", TopK = topK };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.TopK);
    }

    [Fact]
    public async Task Validate_NullTopK_ShouldNotHaveError()
    {
        // Arrange
        var request = new QueryRequest { Query = "test", TopK = null };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.TopK);
    }

    [Theory]
    [InlineData("BM25")]
    [InlineData("Dense")]
    [InlineData("Hybrid")]
    [InlineData("Adaptive")]
    [InlineData("bm25")]
    [InlineData("dense")]
    public async Task Validate_ValidStrategy_ShouldNotHaveError(string strategy)
    {
        // Arrange
        var request = new QueryRequest { Query = "test", Strategy = strategy };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Strategy);
    }

    [Fact]
    public async Task Validate_InvalidStrategy_ShouldHaveError()
    {
        // Arrange
        var request = new QueryRequest { Query = "test", Strategy = "InvalidStrategy" };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Strategy)
            .WithErrorMessage("Strategy must be one of: BM25, Dense, Hybrid, Adaptive");
    }

    [Theory]
    [InlineData("OpenAI")]
    [InlineData("Ollama")]
    [InlineData("openai")]
    [InlineData("ollama")]
    public async Task Validate_ValidProvider_ShouldNotHaveError(string provider)
    {
        // Arrange
        var request = new QueryRequest { Query = "test", Provider = provider };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Provider);
    }

    [Fact]
    public async Task Validate_InvalidProvider_ShouldHaveError()
    {
        // Arrange
        var request = new QueryRequest { Query = "test", Provider = "InvalidProvider" };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Provider)
            .WithErrorMessage("Provider must be one of: OpenAI, Ollama");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public async Task Validate_TemperatureInRange_ShouldNotHaveError(double temperature)
    {
        // Arrange
        var request = new QueryRequest { Query = "test", Temperature = temperature };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Temperature);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    public async Task Validate_TemperatureOutOfRange_ShouldHaveError(double temperature)
    {
        // Arrange
        var request = new QueryRequest { Query = "test", Temperature = temperature };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Temperature)
            .WithErrorMessage("Temperature must be between 0.0 and 1.0");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2000)]
    [InlineData(4000)]
    public async Task Validate_MaxTokensInRange_ShouldNotHaveError(int maxTokens)
    {
        // Arrange
        var request = new QueryRequest { Query = "test", MaxTokens = maxTokens };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.MaxTokens);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4001)]
    [InlineData(10000)]
    public async Task Validate_MaxTokensOutOfRange_ShouldHaveError(int maxTokens)
    {
        // Arrange
        var request = new QueryRequest { Query = "test", MaxTokens = maxTokens };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxTokens)
            .WithErrorMessage("MaxTokens must be between 1 and 4000");
    }

    [Fact]
    public async Task Validate_CompleteValidRequest_ShouldNotHaveErrors()
    {
        // Arrange
        var request = new QueryRequest
        {
            Query = "What is machine learning?",
            TopK = 10,
            Strategy = "Hybrid",
            Provider = "OpenAI",
            Temperature = 0.7,
            MaxTokens = 1000
        };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}

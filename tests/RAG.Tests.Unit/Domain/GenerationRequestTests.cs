using FluentValidation.TestHelper;
using RAG.Core.Domain;
using RAG.Core.Validators;
using Shouldly;

namespace RAG.Tests.Unit.Domain;

public class GenerationRequestTests
{
    private readonly GenerationRequestValidator _validator;

    public GenerationRequestTests()
    {
        _validator = new GenerationRequestValidator();
    }

    [Fact]
    public void Record_WithValidData_CreatesGenerationRequest()
    {
        // Arrange & Act
        var request = new GenerationRequest
        {
            Query = "What is RAG?",
            Context = "RAG stands for Retrieval-Augmented Generation",
            MaxTokens = 500,
            Temperature = 0.7m,
            SystemPrompt = "You are a helpful assistant"
        };

        // Assert
        request.ShouldNotBeNull();
        request.Query.ShouldBe("What is RAG?");
        request.Context.ShouldBe("RAG stands for Retrieval-Augmented Generation");
        request.MaxTokens.ShouldBe(500);
        request.Temperature.ShouldBe(0.7m);
        request.SystemPrompt.ShouldBe("You are a helpful assistant");
    }

    [Fact]
    public void Record_WithDefaults_UsesDefaultValues()
    {
        // Arrange & Act
        var request = new GenerationRequest();

        // Assert
        request.Query.ShouldBe(string.Empty);
        request.Context.ShouldBe(string.Empty);
        request.MaxTokens.ShouldBe(500);
        request.Temperature.ShouldBe(0.7m);
        request.SystemPrompt.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validator_WithInvalidQuery_HasValidationError(string invalidQuery)
    {
        // Arrange
        var request = new GenerationRequest { Query = invalidQuery };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Query)
            .WithErrorMessage("Query cannot be empty");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validator_WithInvalidMaxTokens_HasValidationError(int invalidMaxTokens)
    {
        // Arrange
        var request = new GenerationRequest
        {
            Query = "test",
            MaxTokens = invalidMaxTokens
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxTokens)
            .WithErrorMessage("MaxTokens must be greater than 0");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    public void Validator_WithInvalidTemperature_HasValidationError(double invalidTemperature)
    {
        // Arrange
        var request = new GenerationRequest
        {
            Query = "test",
            Temperature = (decimal)invalidTemperature
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Temperature)
            .WithErrorMessage("Temperature must be between 0.0 and 1.0");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.7)]
    [InlineData(1.0)]
    public void Validator_WithValidTemperature_PassesValidation(double temperature)
    {
        // Arrange
        var request = new GenerationRequest
        {
            Query = "test",
            Temperature = (decimal)temperature
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Temperature);
    }

    [Fact]
    public void Record_WithEmptyContext_IsValid()
    {
        // Arrange & Act
        var request = new GenerationRequest
        {
            Query = "test",
            Context = string.Empty
        };

        // Assert
        request.Context.ShouldBeEmpty();
        var result = _validator.TestValidate(request);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Record_Immutability_WithExpressionWorks()
    {
        // Arrange
        var original = new GenerationRequest
        {
            Query = "original",
            MaxTokens = 100
        };

        // Act
        var modified = original with { Query = "modified" };

        // Assert
        original.Query.ShouldBe("original");
        modified.Query.ShouldBe("modified");
        modified.MaxTokens.ShouldBe(100);
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var request1 = new GenerationRequest
        {
            Query = "test",
            Context = "context",
            MaxTokens = 500,
            Temperature = 0.7m
        };
        var request2 = new GenerationRequest
        {
            Query = "test",
            Context = "context",
            MaxTokens = 500,
            Temperature = 0.7m
        };

        // Act & Assert
        request1.ShouldBe(request2);
        (request1 == request2).ShouldBeTrue();
    }

    [Fact]
    public void RecordEquality_WithDifferentValues_AreNotEqual()
    {
        // Arrange
        var request1 = new GenerationRequest { Query = "query1" };
        var request2 = new GenerationRequest { Query = "query2" };

        // Act & Assert
        request1.ShouldNotBe(request2);
        (request1 != request2).ShouldBeTrue();
    }
}

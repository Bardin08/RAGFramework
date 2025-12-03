using Microsoft.AspNetCore.Http;
using RAG.API.Factories;
using RAG.Core.Constants;
using RAG.Core.Exceptions;
using Shouldly;

namespace RAG.Tests.Unit.Factories;

public class ProblemDetailsFactoryTests
{
    [Fact]
    public void Create_WithBasicParameters_ReturnsProblemDetails()
    {
        // Arrange
        var statusCode = 404;
        var title = "Not Found";
        var detail = "Resource not found";
        var instance = "/api/documents/123";

        // Act
        var result = ProblemDetailsFactory.Create(statusCode, title, detail, instance);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(statusCode);
        result.Title.ShouldBe(title);
        result.Detail.ShouldBe(detail);
        result.Instance.ShouldBe(instance);
        result.Type!.ShouldContain("404");
    }

    [Fact]
    public void Create_WithErrorCode_UsesErrorCodeInType()
    {
        // Arrange
        var errorCode = "not-found";

        // Act
        var result = ProblemDetailsFactory.Create(404, "Not Found", "Detail", "/api/test", errorCode);

        // Assert
        result.Type.ShouldBe($"{ProblemDetailsFactory.ErrorTypeBaseUri}/{errorCode}");
    }

    [Fact]
    public void Create_WithCorrelationId_IncludesCorrelationIdInExtensions()
    {
        // Arrange
        var correlationId = "correlation-123";

        // Act
        var result = ProblemDetailsFactory.Create(404, "Not Found", "Detail", "/api/test", correlationId: correlationId);

        // Assert
        result.Extensions.ShouldContainKey("correlationId");
        result.Extensions["correlationId"].ShouldBe(correlationId);
    }

    [Fact]
    public void Create_Always_IncludesTimestamp()
    {
        // Act
        var result = ProblemDetailsFactory.Create(404, "Not Found", "Detail", "/api/test");

        // Assert
        result.Extensions.ShouldContainKey("timestamp");
        result.Extensions["timestamp"].ShouldNotBeNull();
    }

    [Fact]
    public void Create_WithExtensions_IncludesAllExtensions()
    {
        // Arrange
        var extensions = new Dictionary<string, object>
        {
            ["customField1"] = "value1",
            ["customField2"] = 42
        };

        // Act
        var result = ProblemDetailsFactory.Create(404, "Not Found", "Detail", "/api/test", extensions: extensions);

        // Assert
        result.Extensions.ShouldContainKey("customField1");
        result.Extensions.ShouldContainKey("customField2");
        result.Extensions["customField1"].ShouldBe("value1");
        result.Extensions["customField2"].ShouldBe(42);
    }

    [Fact]
    public void CreateFromException_WithNotFoundException_ReturnsCorrectProblemDetails()
    {
        // Arrange - cast to object to use the (string, object) constructor
        var exception = new NotFoundException("Document", (object)"doc-123");
        var instance = "/api/documents/doc-123";

        // Act
        var result = ProblemDetailsFactory.CreateFromException(exception, 404, instance);

        // Assert
        result.Status.ShouldBe(404);
        result.Title.ShouldBe("Not Found");
        result.Detail.ShouldBe(exception.Message);
        result.Type!.ShouldContain(exception.ErrorCode);
    }

    [Fact]
    public void CreateFromException_IncludesExceptionDetails()
    {
        // Arrange - cast to object to use the (string, object) constructor that sets Details
        var exception = new NotFoundException("Document", (object)"doc-123");

        // Act
        var result = ProblemDetailsFactory.CreateFromException(exception, 404, "/api/test");

        // Assert
        result.Extensions.ShouldContainKey("resourceType");
        result.Extensions.ShouldContainKey("resourceId");
    }

    [Fact]
    public void CreateValidationProblemDetails_Returns400WithErrors()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            ["field1"] = new[] { "Error 1", "Error 2" },
            ["field2"] = new[] { "Error 3" }
        };
        var instance = "/api/documents";

        // Act
        var result = ProblemDetailsFactory.CreateValidationProblemDetails(errors, instance);

        // Assert
        result.Status.ShouldBe(StatusCodes.Status400BadRequest);
        result.Title.ShouldBe("Validation Failed");
        result.Type!.ShouldContain(ErrorCodes.ValidationFailed);
        result.Extensions.ShouldContainKey("errors");
    }

    [Fact]
    public void CreateValidationProblemDetails_WithCorrelationId_IncludesCorrelationId()
    {
        // Arrange
        var errors = new Dictionary<string, string[]> { ["field"] = new[] { "Error" } };
        var correlationId = "corr-123";

        // Act
        var result = ProblemDetailsFactory.CreateValidationProblemDetails(errors, "/api/test", correlationId);

        // Assert
        result.Extensions.ShouldContainKey("correlationId");
        result.Extensions["correlationId"].ShouldBe(correlationId);
    }

    [Fact]
    public void CreateInternalError_WithoutDetails_HidesExceptionInfo()
    {
        // Arrange
        var exception = new InvalidOperationException("Sensitive error message");

        // Act
        var result = ProblemDetailsFactory.CreateInternalError(exception, "/api/test", includeDetails: false);

        // Assert
        result.Status.ShouldBe(500);
        result.Detail!.ShouldNotContain("Sensitive error message");
        result.Extensions.ShouldNotContainKey("exception");
    }

    [Fact]
    public void CreateInternalError_WithDetails_IncludesExceptionInfo()
    {
        // Arrange
        var exception = new InvalidOperationException("Error message");

        // Act
        var result = ProblemDetailsFactory.CreateInternalError(exception, "/api/test", includeDetails: true);

        // Assert
        result.Detail!.ShouldContain("Error message");
        result.Extensions.ShouldContainKey("exception");
    }

    [Fact]
    public void CreateInternalError_WithCorrelationId_IncludesReferenceInDetail()
    {
        // Arrange
        var correlationId = "corr-123";

        // Act
        var result = ProblemDetailsFactory.CreateInternalError(
            new Exception("Error"),
            "/api/test",
            correlationId,
            includeDetails: false);

        // Assert
        result.Detail!.ShouldContain($"Reference: {correlationId}");
    }

    [Fact]
    public void GetErrorTypeUri_ReturnsCorrectUri()
    {
        // Arrange
        var errorCode = "not-found";

        // Act
        var result = ProblemDetailsFactory.GetErrorTypeUri(errorCode);

        // Assert
        result.ShouldBe($"{ProblemDetailsFactory.ErrorTypeBaseUri}/not-found");
    }

    [Theory]
    [InlineData(400, "Bad Request")]
    [InlineData(401, "Unauthorized")]
    [InlineData(403, "Forbidden")]
    [InlineData(404, "Not Found")]
    [InlineData(409, "Conflict")]
    [InlineData(413, "Payload Too Large")]
    [InlineData(429, "Too Many Requests")]
    [InlineData(499, "Client Closed Request")]
    [InlineData(500, "Internal Server Error")]
    [InlineData(503, "Service Unavailable")]
    public void GetTitleForStatusCode_ReturnsCorrectTitle(int statusCode, string expectedTitle)
    {
        // Act
        var result = ProblemDetailsFactory.GetTitleForStatusCode(statusCode);

        // Assert
        result.ShouldBe(expectedTitle);
    }

    [Fact]
    public void GetTitleForStatusCode_UnknownStatus_ReturnsError()
    {
        // Act
        var result = ProblemDetailsFactory.GetTitleForStatusCode(999);

        // Assert
        result.ShouldBe("Error");
    }

    [Fact]
    public void ErrorTypeBaseUri_HasCorrectValue()
    {
        // Assert
        ProblemDetailsFactory.ErrorTypeBaseUri.ShouldBe("https://api.rag.system/errors");
    }
}

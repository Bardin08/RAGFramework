using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using RAG.API.Middleware;
using RAG.Application.Exceptions;
using RAG.Core.Exceptions;
using Shouldly;

namespace RAG.Tests.Unit.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _loggerMock;
    private readonly Mock<IHostEnvironment> _environmentMock;

    public ExceptionHandlingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        _environmentMock = new Mock<IHostEnvironment>();
        _environmentMock.Setup(e => e.EnvironmentName).Returns("Production");
    }

    private ExceptionHandlingMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/test";
        return context;
    }

    private static async Task<ProblemDetails?> GetProblemDetailsFromResponse(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<ProblemDetails>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    [Fact]
    public async Task InvokeAsync_NotFoundException_Returns404WithProblemDetails()
    {
        // Arrange
        var context = CreateHttpContext();
        var exception = new NotFoundException("Document", Guid.NewGuid());
        var middleware = CreateMiddleware(_ => throw exception);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        context.Response.ContentType.ShouldBe("application/problem+json");

        var problemDetails = await GetProblemDetailsFromResponse(context);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(404);
        problemDetails.Title.ShouldBe("Not Found");
        problemDetails.Type.ShouldContain("not-found");
    }

    [Fact]
    public async Task InvokeAsync_ValidationException_Returns400WithErrors()
    {
        // Arrange
        var context = CreateHttpContext();
        var errors = new Dictionary<string, string[]>
        {
            ["field1"] = new[] { "Error 1", "Error 2" },
            ["field2"] = new[] { "Error 3" }
        };
        var exception = new RAG.Core.Exceptions.ValidationException(errors);
        var middleware = CreateMiddleware(_ => throw exception);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);

        var problemDetails = await GetProblemDetailsFromResponse(context);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(400);
        problemDetails.Title.ShouldBe("Validation Failed");
        problemDetails.Extensions.ShouldContainKey("errors");
    }

    [Fact]
    public async Task InvokeAsync_ForbiddenException_Returns403()
    {
        // Arrange
        var context = CreateHttpContext();
        var exception = new RAG.Core.Exceptions.ForbiddenException("Access denied");
        var middleware = CreateMiddleware(_ => throw exception);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);

        var problemDetails = await GetProblemDetailsFromResponse(context);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(403);
        problemDetails.Title.ShouldBe("Forbidden");
    }

    [Fact]
    public async Task InvokeAsync_UnauthorizedException_Returns401()
    {
        // Arrange
        var context = CreateHttpContext();
        var exception = new UnauthorizedException("Authentication required");
        var middleware = CreateMiddleware(_ => throw exception);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);

        var problemDetails = await GetProblemDetailsFromResponse(context);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(401);
    }

    [Fact]
    public async Task InvokeAsync_ConflictException_Returns409()
    {
        // Arrange
        var context = CreateHttpContext();
        var exception = new ConflictException("Resource conflict");
        var middleware = CreateMiddleware(_ => throw exception);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);

        var problemDetails = await GetProblemDetailsFromResponse(context);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(409);
    }

    [Fact]
    public async Task InvokeAsync_TooManyRequestsException_Returns429WithRetryAfter()
    {
        // Arrange
        var context = CreateHttpContext();
        var exception = new TooManyRequestsException("Rate limit exceeded", retryAfterSeconds: 60);
        var middleware = CreateMiddleware(_ => throw exception);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status429TooManyRequests);
        context.Response.Headers["Retry-After"].ToString().ShouldBe("60");

        var problemDetails = await GetProblemDetailsFromResponse(context);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(429);
    }

    [Fact]
    public async Task InvokeAsync_ServiceUnavailableException_Returns503()
    {
        // Arrange
        var context = CreateHttpContext();
        var exception = new ServiceUnavailableException("Service down", "Elasticsearch");
        var middleware = CreateMiddleware(_ => throw exception);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);

        var problemDetails = await GetProblemDetailsFromResponse(context);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(503);
    }

    [Fact]
    public async Task InvokeAsync_FileSizeException_Returns413()
    {
        // Arrange
        var context = CreateHttpContext();
        var exception = new FileSizeException(20_000_000, 10_000_000);
        var middleware = CreateMiddleware(_ => throw exception);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status413PayloadTooLarge);

        var problemDetails = await GetProblemDetailsFromResponse(context);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(413);
    }

    [Fact]
    public async Task InvokeAsync_GenericException_Returns500WithoutStackTrace_InProduction()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns("Production");
        var context = CreateHttpContext();
        var exception = new InvalidOperationException("Something went wrong");
        var middleware = CreateMiddleware(_ => throw exception);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);

        var problemDetails = await GetProblemDetailsFromResponse(context);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(500);
        problemDetails.Detail.ShouldNotContain("Something went wrong"); // Hidden in production
        problemDetails.Extensions.ShouldNotContainKey("exception");
    }

    [Fact]
    public async Task InvokeAsync_GenericException_IncludesStackTrace_InDevelopment()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns("Development");
        var context = CreateHttpContext();
        var exception = new InvalidOperationException("Something went wrong");
        var middleware = CreateMiddleware(_ => throw exception);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);

        var problemDetails = await GetProblemDetailsFromResponse(context);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(500);
        problemDetails.Extensions.ShouldContainKey("exception");
    }

    [Fact]
    public async Task InvokeAsync_OperationCanceledException_Returns499()
    {
        // Arrange
        var context = CreateHttpContext();
        var exception = new OperationCanceledException();
        var middleware = CreateMiddleware(_ => throw exception);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(499);

        var problemDetails = await GetProblemDetailsFromResponse(context);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(499);
        problemDetails.Title.ShouldBe("Client Closed Request");
    }

    [Fact]
    public async Task InvokeAsync_IncludesCorrelationId_InResponse()
    {
        // Arrange
        var context = CreateHttpContext();
        var exception = new NotFoundException("Test", "123");
        var middleware = CreateMiddleware(_ => throw exception);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.ShouldContainKey("X-Correlation-ID");
        context.Response.Headers["X-Correlation-ID"].ToString().ShouldNotBeEmpty();

        var problemDetails = await GetProblemDetailsFromResponse(context);
        problemDetails.ShouldNotBeNull();
        problemDetails.Extensions.ShouldContainKey("correlationId");
    }

    [Fact]
    public async Task InvokeAsync_UsesExistingCorrelationId_FromRequestHeaders()
    {
        // Arrange
        var context = CreateHttpContext();
        var expectedCorrelationId = "my-correlation-id-123";
        context.Request.Headers["X-Correlation-ID"] = expectedCorrelationId;
        var exception = new NotFoundException("Test", "123");
        var middleware = CreateMiddleware(_ => throw exception);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Correlation-ID"].ToString().ShouldBe(expectedCorrelationId);
    }

    [Fact]
    public async Task InvokeAsync_RFC7807Format_IncludesRequiredFields()
    {
        // Arrange
        var context = CreateHttpContext();
        var exception = new NotFoundException("Document", "doc-123");
        var middleware = CreateMiddleware(_ => throw exception);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var problemDetails = await GetProblemDetailsFromResponse(context);
        problemDetails.ShouldNotBeNull();

        // RFC 7807 required fields
        problemDetails.Type.ShouldNotBeNullOrEmpty();
        problemDetails.Title.ShouldNotBeNullOrEmpty();
        problemDetails.Status.ShouldNotBeNull();

        // RFC 7807 recommended fields
        problemDetails.Detail.ShouldNotBeNullOrEmpty();
        problemDetails.Instance.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_SuccessfulRequest_PassesThrough()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task InvokeAsync_LogsWarning_For4xxErrors()
    {
        // Arrange
        var context = CreateHttpContext();
        var exception = new NotFoundException("Document", "123");
        var middleware = CreateMiddleware(_ => throw exception);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_LogsError_For5xxErrors()
    {
        // Arrange
        var context = CreateHttpContext();
        var exception = new InvalidOperationException("Server error");
        var middleware = CreateMiddleware(_ => throw exception);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

using RAG.Application.Exceptions;
using RAG.Core.Constants;
using RAG.Core.Exceptions;
using Shouldly;

namespace RAG.Tests.Unit.Exceptions;

public class CustomExceptionTests
{
    #region NotFoundException Tests

    [Fact]
    public void NotFoundException_WithResourceTypeAndId_SetsCorrectMessage()
    {
        // Arrange & Act - cast to object to use the (string, object) constructor
        var exception = new NotFoundException("Document", (object)"doc-123");

        // Assert
        exception.Message.ShouldContain("Document");
        exception.Message.ShouldContain("doc-123");
        exception.Message.ShouldContain("was not found");
    }

    [Fact]
    public void NotFoundException_WithResourceTypeAndId_SetsCorrectErrorCode()
    {
        // Act - cast to object to use the (string, object) constructor
        var exception = new NotFoundException("Document", (object)"doc-123");

        // Assert
        exception.ErrorCode.ShouldBe(ErrorCodes.NotFound);
    }

    [Fact]
    public void NotFoundException_WithResourceTypeAndId_IncludesDetailsInDictionary()
    {
        // Act
        var exception = new NotFoundException("Document", Guid.Parse("12345678-1234-1234-1234-123456789012"));

        // Assert
        exception.Details.ShouldNotBeNull();
        exception.Details.ShouldContainKey("resourceType");
        exception.Details.ShouldContainKey("resourceId");
        exception.Details["resourceType"].ShouldBe("Document");
    }

    [Fact]
    public void NotFoundException_WithMessageOnly_SetsMessage()
    {
        // Act
        var exception = new NotFoundException("Custom message");

        // Assert
        exception.Message.ShouldBe("Custom message");
        exception.ErrorCode.ShouldBe(ErrorCodes.NotFound);
    }

    #endregion

    #region ValidationException Tests

    [Fact]
    public void ValidationException_WithErrors_StoresErrors()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            ["field1"] = new[] { "Error 1", "Error 2" },
            ["field2"] = new[] { "Error 3" }
        };

        // Act
        var exception = new RAG.Core.Exceptions.ValidationException(errors);

        // Assert
        exception.Errors.ShouldNotBeNull();
        exception.Errors.ShouldContainKey("field1");
        exception.Errors.ShouldContainKey("field2");
        exception.Errors["field1"].Length.ShouldBe(2);
    }

    [Fact]
    public void ValidationException_SetsCorrectErrorCode()
    {
        // Arrange
        var errors = new Dictionary<string, string[]> { ["field"] = new[] { "Error" } };

        // Act
        var exception = new RAG.Core.Exceptions.ValidationException(errors);

        // Assert
        exception.ErrorCode.ShouldBe(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public void ValidationException_WithFieldAndMessage_CreatesFromSingleError()
    {
        // Act
        var exception = new RAG.Core.Exceptions.ValidationException("fieldName", "Error message");

        // Assert
        exception.Errors.ShouldContainKey("fieldName");
        exception.Errors["fieldName"].ShouldContain("Error message");
    }

    #endregion

    #region ForbiddenException Tests

    [Fact]
    public void ForbiddenException_WithMessage_SetsMessage()
    {
        // Act
        var exception = new RAG.Core.Exceptions.ForbiddenException("Access denied");

        // Assert
        exception.Message.ShouldBe("Access denied");
        exception.ErrorCode.ShouldBe(ErrorCodes.Forbidden);
    }

    [Fact]
    public void ForbiddenException_WithResourceInfo_IncludesDetails()
    {
        // Act - using the 3-param constructor: resourceType, resourceId, requiredPermission
        var exception = new RAG.Core.Exceptions.ForbiddenException("Document", "doc-123", "read");

        // Assert
        exception.Details.ShouldNotBeNull();
        exception.Details.ShouldContainKey("resourceType");
        exception.Details.ShouldContainKey("resourceId");
        exception.Details.ShouldContainKey("requiredPermission");
    }

    #endregion

    #region UnauthorizedException Tests

    [Fact]
    public void UnauthorizedException_WithMessage_SetsMessage()
    {
        // Act
        var exception = new UnauthorizedException("Authentication required");

        // Assert
        exception.Message.ShouldBe("Authentication required");
        exception.ErrorCode.ShouldBe(ErrorCodes.Unauthorized);
    }

    [Fact]
    public void UnauthorizedException_DefaultConstructor_SetsDefaultMessage()
    {
        // Act
        var exception = new UnauthorizedException();

        // Assert
        exception.Message.ShouldContain("Authentication");
    }

    #endregion

    #region ConflictException Tests

    [Fact]
    public void ConflictException_WithMessage_SetsMessage()
    {
        // Act
        var exception = new ConflictException("Resource already exists");

        // Assert
        exception.Message.ShouldBe("Resource already exists");
        exception.ErrorCode.ShouldBe(ErrorCodes.Conflict);
    }

    [Fact]
    public void ConflictException_WithResourceInfo_IncludesDetails()
    {
        // Act - using the 3-param constructor: resourceType, resourceId, conflictReason
        var exception = new ConflictException("Document", "doc-123", "Already exists");

        // Assert
        exception.Details.ShouldNotBeNull();
        exception.Details.ShouldContainKey("resourceType");
        exception.Details.ShouldContainKey("resourceId");
        exception.Details.ShouldContainKey("conflictReason");
    }

    #endregion

    #region TooManyRequestsException Tests

    [Fact]
    public void TooManyRequestsException_WithMessage_SetsMessage()
    {
        // Act
        var exception = new TooManyRequestsException("Rate limit exceeded");

        // Assert
        exception.Message.ShouldBe("Rate limit exceeded");
        exception.ErrorCode.ShouldBe(ErrorCodes.RateLimitExceeded);
    }

    [Fact]
    public void TooManyRequestsException_WithRetryAfter_StoresRetryAfterSeconds()
    {
        // Act
        var exception = new TooManyRequestsException("Rate limit exceeded", retryAfterSeconds: 60);

        // Assert
        exception.RetryAfterSeconds.ShouldBe(60);
    }

    [Fact]
    public void TooManyRequestsException_WithoutRetryAfter_ReturnsNull()
    {
        // Act
        var exception = new TooManyRequestsException("Rate limit exceeded");

        // Assert
        exception.RetryAfterSeconds.ShouldBeNull();
    }

    #endregion

    #region ServiceUnavailableException Tests

    [Fact]
    public void ServiceUnavailableException_WithMessage_SetsMessage()
    {
        // Act
        var exception = new ServiceUnavailableException("Service down");

        // Assert
        exception.Message.ShouldBe("Service down");
        exception.ErrorCode.ShouldBe(ErrorCodes.ServiceUnavailable);
    }

    [Fact]
    public void ServiceUnavailableException_WithServiceName_IncludesServiceInDetails()
    {
        // Act - passing serviceName as second parameter
        var exception = new ServiceUnavailableException("Service down", "Elasticsearch");

        // Assert
        exception.ServiceName.ShouldBe("Elasticsearch");
        exception.Details.ShouldNotBeNull();
        exception.Details.ShouldContainKey("serviceName");
    }

    #endregion

    #region RagException Base Class Tests

    [Fact]
    public void RagException_InheritsFromException()
    {
        // Act
        var exception = new NotFoundException("Test", "123");

        // Assert
        exception.ShouldBeAssignableTo<Exception>();
        exception.ShouldBeAssignableTo<RagException>();
    }

    [Fact]
    public void RagException_ErrorCode_IsNeverNull()
    {
        // Act
        var notFound = new NotFoundException("Test");
        var validation = new RAG.Core.Exceptions.ValidationException(new Dictionary<string, string[]>());
        var forbidden = new RAG.Core.Exceptions.ForbiddenException("Test");
        var unauthorized = new UnauthorizedException();
        var conflict = new ConflictException("Test");
        var tooMany = new TooManyRequestsException("Test");
        var unavailable = new ServiceUnavailableException("Test");

        // Assert
        notFound.ErrorCode.ShouldNotBeNullOrEmpty();
        validation.ErrorCode.ShouldNotBeNullOrEmpty();
        forbidden.ErrorCode.ShouldNotBeNullOrEmpty();
        unauthorized.ErrorCode.ShouldNotBeNullOrEmpty();
        conflict.ErrorCode.ShouldNotBeNullOrEmpty();
        tooMany.ErrorCode.ShouldNotBeNullOrEmpty();
        unavailable.ErrorCode.ShouldNotBeNullOrEmpty();
    }

    #endregion

    #region FileSizeException Tests

    [Fact]
    public void FileSizeException_WithSizes_SetsCorrectMessage()
    {
        // Act
        var exception = new FileSizeException(20_000_000, 10_000_000);

        // Assert - message contains formatted sizes (MB)
        exception.Message.ShouldContain("MB");
        exception.Message.ShouldContain("exceeds");
        exception.ErrorCode.ShouldBe(ErrorCodes.FileTooLarge);
    }

    [Fact]
    public void FileSizeException_StoresSizeValues()
    {
        // Act
        var exception = new FileSizeException(20_000_000, 10_000_000);

        // Assert
        exception.FileSize.ShouldBe(20_000_000);
        exception.MaxAllowedSize.ShouldBe(10_000_000);
    }

    #endregion
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using RAG.Infrastructure.Data;
using RAG.Infrastructure.Services;
using Shouldly;

namespace RAG.Tests.Unit.Services.Admin;

/// <summary>
/// Unit tests for AuditLogService.
/// </summary>
public class AuditLogServiceTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly Mock<ILogger<AuditLogService>> _loggerMock;
    private readonly AuditLogService _service;

    public AuditLogServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _loggerMock = new Mock<ILogger<AuditLogService>>();
        _service = new AuditLogService(_dbContext, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    #region LogAsync Tests

    [Fact]
    public async Task LogAsync_CreatesAuditLogEntry_InDatabase()
    {
        // Arrange
        var entry = new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            UserId = "user123",
            UserName = "testuser",
            Action = "ClearCache",
            Resource = "/api/admin/cache/clear",
            Details = "{\"cacheTypes\":[\"all\"]}",
            IpAddress = "192.168.1.1",
            StatusCode = 200,
            DurationMs = 50
        };

        // Act
        await _service.LogAsync(entry);

        // Assert
        var savedEntry = await _dbContext.AuditLogs.FirstOrDefaultAsync(e => e.Id == entry.Id);
        savedEntry.ShouldNotBeNull();
        savedEntry.UserId.ShouldBe("user123");
        savedEntry.Action.ShouldBe("ClearCache");
        savedEntry.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task LogAsync_LogsInformationMessage()
    {
        // Arrange
        var entry = new AuditLogEntry
        {
            UserId = "admin",
            Action = "RebuildIndex",
            Resource = "/api/admin/index/rebuild",
            StatusCode = 202
        };

        // Act
        await _service.LogAsync(entry);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("RebuildIndex")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region GetLogsAsync Tests

    [Fact]
    public async Task GetLogsAsync_ReturnsEmptyResult_WhenNoLogsExist()
    {
        // Act
        var result = await _service.GetLogsAsync(new AuditLogFilter());

        // Assert
        result.ShouldNotBeNull();
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetLogsAsync_ReturnsAllLogs_WhenNoFilterApplied()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var result = await _service.GetLogsAsync(new AuditLogFilter());

        // Assert
        result.TotalCount.ShouldBe(5);
        result.Items.Count.ShouldBe(5);
    }

    [Fact]
    public async Task GetLogsAsync_FiltersById_WhenUserIdProvided()
    {
        // Arrange
        await SeedAuditLogs();

        var filter = new AuditLogFilter { UserId = "user1" };

        // Act
        var result = await _service.GetLogsAsync(filter);

        // Assert
        result.TotalCount.ShouldBe(2);
        result.Items.ShouldAllBe(e => e.UserId == "user1");
    }

    [Fact]
    public async Task GetLogsAsync_FiltersByAction_WhenActionProvided()
    {
        // Arrange
        await SeedAuditLogs();

        var filter = new AuditLogFilter { Action = "ClearCache" };

        // Act
        var result = await _service.GetLogsAsync(filter);

        // Assert
        result.TotalCount.ShouldBe(2);
        result.Items.ShouldAllBe(e => e.Action.Contains("ClearCache"));
    }

    [Fact]
    public async Task GetLogsAsync_FiltersByDateRange_WhenDatesProvided()
    {
        // Arrange
        await SeedAuditLogs();

        var filter = new AuditLogFilter
        {
            FromDate = DateTime.UtcNow.AddHours(-2),
            ToDate = DateTime.UtcNow.AddHours(-1)
        };

        // Act
        var result = await _service.GetLogsAsync(filter);

        // Assert
        result.Items.ShouldAllBe(e => e.Timestamp >= filter.FromDate && e.Timestamp <= filter.ToDate);
    }

    [Fact]
    public async Task GetLogsAsync_SupportsPagination()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var page1 = await _service.GetLogsAsync(new AuditLogFilter(), page: 1, pageSize: 2);
        var page2 = await _service.GetLogsAsync(new AuditLogFilter(), page: 2, pageSize: 2);

        // Assert
        page1.Items.Count.ShouldBe(2);
        page1.Page.ShouldBe(1);
        page1.TotalPages.ShouldBe(3);
        page1.HasNextPage.ShouldBeTrue();
        page1.HasPreviousPage.ShouldBeFalse();

        page2.Items.Count.ShouldBe(2);
        page2.Page.ShouldBe(2);
        page2.HasNextPage.ShouldBeTrue();
        page2.HasPreviousPage.ShouldBeTrue();
    }

    [Fact]
    public async Task GetLogsAsync_OrdersByTimestampDescending()
    {
        // Arrange
        await SeedAuditLogs();

        // Act
        var result = await _service.GetLogsAsync(new AuditLogFilter());

        // Assert
        var timestamps = result.Items.Select(e => e.Timestamp).ToList();
        timestamps.ShouldBe(timestamps.OrderByDescending(t => t).ToList());
    }

    private async Task SeedAuditLogs()
    {
        var logs = new List<AuditLogEntry>
        {
            new() { Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow.AddHours(-5), UserId = "user1", Action = "ClearCache", Resource = "/api/admin/cache" },
            new() { Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow.AddHours(-4), UserId = "user2", Action = "RebuildIndex", Resource = "/api/admin/index" },
            new() { Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow.AddHours(-3), UserId = "user1", Action = "GetStats", Resource = "/api/admin/stats" },
            new() { Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow.AddHours(-2), UserId = "admin", Action = "ClearCache", Resource = "/api/admin/cache" },
            new() { Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow.AddHours(-1), UserId = "admin", Action = "GetHealth", Resource = "/api/admin/health" }
        };

        _dbContext.AuditLogs.AddRange(logs);
        await _dbContext.SaveChangesAsync();
    }

    #endregion
}

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RAG.Evaluation.Plugins;
using Xunit;

namespace RAG.Tests.Unit.Evaluation;

public class PluginLoaderServiceTests
{
    private readonly Mock<ILogger<PluginLoaderService>> _mockLogger;
    private readonly string _testPluginDirectory;

    public PluginLoaderServiceTests()
    {
        _mockLogger = new Mock<ILogger<PluginLoaderService>>();
        _testPluginDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    }

    [Fact]
    public async Task LoadPluginsAsync_WithEmptyDirectory_CreatesDirectoryAndLoadsNothing()
    {
        // Arrange
        var service = new PluginLoaderService(_mockLogger.Object, _testPluginDirectory);

        // Act
        await service.LoadPluginsAsync();

        // Assert
        service.Plugins.Should().BeEmpty();
        Directory.Exists(_testPluginDirectory).Should().BeTrue();

        // Cleanup
        Directory.Delete(_testPluginDirectory);
    }

    [Fact]
    public void GetPlugin_WithNonExistentPlugin_ReturnsNull()
    {
        // Arrange
        var service = new PluginLoaderService(_mockLogger.Object, _testPluginDirectory);

        // Act
        var result = service.GetPlugin("NonExistentPlugin");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void HasPlugin_WithNonExistentPlugin_ReturnsFalse()
    {
        // Arrange
        var service = new PluginLoaderService(_mockLogger.Object, _testPluginDirectory);

        // Act
        var result = service.HasPlugin("NonExistentPlugin");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ReloadPluginsAsync_ClearsAndReloadsPlugins()
    {
        // Arrange
        if (!Directory.Exists(_testPluginDirectory))
        {
            Directory.CreateDirectory(_testPluginDirectory);
        }

        var service = new PluginLoaderService(_mockLogger.Object, _testPluginDirectory);
        await service.LoadPluginsAsync();

        // Act
        await service.ReloadPluginsAsync();

        // Assert
        service.Plugins.Should().BeEmpty();

        // Cleanup
        if (Directory.Exists(_testPluginDirectory))
        {
            Directory.Delete(_testPluginDirectory, true);
        }
    }
}

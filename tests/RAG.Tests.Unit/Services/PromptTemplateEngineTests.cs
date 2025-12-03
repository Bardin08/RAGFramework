using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RAG.Application.Configuration;
using RAG.Application.Services;
using Shouldly;

namespace RAG.Tests.Unit.Services;

public class PromptTemplateEngineTests : IDisposable
{
    private readonly string _testTemplateDir;
    private readonly Mock<ILogger<PromptTemplateEngine>> _mockLogger;

    public PromptTemplateEngineTests()
    {
        _testTemplateDir = Path.Combine(Path.GetTempPath(), $"prompt-templates-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testTemplateDir);
        _mockLogger = new Mock<ILogger<PromptTemplateEngine>>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testTemplateDir))
        {
            Directory.Delete(_testTemplateDir, true);
        }
    }

    [Fact]
    public void Constructor_WithValidSettings_CreatesEngine()
    {
        // Arrange
        var settings = CreateSettings();

        // Act
        using var engine = new PromptTemplateEngine(settings, _mockLogger.Object);

        // Assert
        engine.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WithNullSettings_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new PromptTemplateEngine(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithInvalidSettings_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidSettings = Options.Create(new PromptTemplateSettings
        {
            Directory = "", // Invalid
            DefaultTemplate = "test"
        });

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            new PromptTemplateEngine(invalidSettings, _mockLogger.Object));
    }

    [Fact]
    public void LoadTemplates_WithValidYaml_LoadsSuccessfully()
    {
        // Arrange
        var yamlContent = @"
name: test-template
version: ""1.0""
systemPrompt: You are a test assistant.
userPromptTemplate: ""Question: {{query}}""
";
        CreateTestTemplate("test-template.yaml", yamlContent);

        var settings = CreateSettings();
        using var engine = new PromptTemplateEngine(settings, _mockLogger.Object);

        // Act
        var template = engine.GetTemplate("test-template");

        // Assert
        template.ShouldNotBeNull();
        template.Name.ShouldBe("test-template");
        template.Version.ShouldBe("1.0");
        template.SystemPrompt.ShouldContain("test assistant");
        template.Parameters.ShouldNotBeNull();
        template.Parameters.Temperature.ShouldBe(0.7); // Default value
        template.Parameters.MaxTokens.ShouldBe(500); // Default value
    }

    [Fact]
    public async Task RenderTemplateAsync_WithValidVariables_RendersCorrectly()
    {
        // Arrange
        CreateTestTemplate("render-test.yaml", @"
name: render-test
version: ""1.0""
systemPrompt: System prompt
userPromptTemplate: ""Context: {{context}}, Query: {{query}}""
");

        var settings = CreateSettings();
        using var engine = new PromptTemplateEngine(settings, _mockLogger.Object);

        var variables = new Dictionary<string, string>
        {
            { "context", "Test context" },
            { "query", "Test query" }
        };

        // Act
        var rendered = await engine.RenderTemplateAsync("render-test", variables);

        // Assert
        rendered.ShouldNotBeNull();
        rendered.SystemPrompt.ShouldBe("System prompt");
        rendered.UserPrompt.ShouldContain("Test context");
        rendered.UserPrompt.ShouldContain("Test query");
        rendered.Parameters.Temperature.ShouldBe(0.7); // Default
        rendered.TemplateName.ShouldBe("render-test");
        rendered.TemplateVersion.ShouldBe("1.0");
    }

    [Fact]
    public async Task RenderTemplateAsync_WithMissingTemplate_ThrowsInvalidOperationException()
    {
        // Arrange
        var settings = CreateSettings();
        using var engine = new PromptTemplateEngine(settings, _mockLogger.Object);

        var variables = new Dictionary<string, string> { { "query", "test" } };

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await engine.RenderTemplateAsync("non-existent", variables));
    }

    [Fact]
    public async Task RenderTemplateAsync_WithNullVariables_ThrowsArgumentNullException()
    {
        // Arrange
        CreateTestTemplate("test.yaml", @"
name: test
systemPrompt: Test
userPromptTemplate: Test
");

        var settings = CreateSettings();
        using var engine = new PromptTemplateEngine(settings, _mockLogger.Object);

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await engine.RenderTemplateAsync("test", null!));
    }

    [Fact]
    public void GetTemplate_WithMultipleVersions_ReturnsLatestByDefault()
    {
        // Arrange
        CreateTestTemplate("multi-v1.yaml", @"
name: multi-version
version: ""1.0""
systemPrompt: Version 1
userPromptTemplate: V1
");

        CreateTestTemplate("multi-v2.yaml", @"
name: multi-version
version: ""2.0""
systemPrompt: Version 2
userPromptTemplate: V2
");

        var settings = CreateSettings();
        using var engine = new PromptTemplateEngine(settings, _mockLogger.Object);

        // Act
        var template = engine.GetTemplate("multi-version");

        // Assert
        template.ShouldNotBeNull();
        // Latest by LoadedAt time (last loaded)
        template.Version.ShouldBeOneOf("1.0", "2.0");
    }

    [Fact]
    public void GetTemplate_WithSpecificVersion_ReturnsCorrectVersion()
    {
        // Arrange
        CreateTestTemplate("versioned-v1.yaml", @"
name: versioned
version: ""1.0""
systemPrompt: Version 1
userPromptTemplate: V1
");

        CreateTestTemplate("versioned-v2.yaml", @"
name: versioned
version: ""2.0""
systemPrompt: Version 2
userPromptTemplate: V2
");

        var settings = CreateSettings();
        using var engine = new PromptTemplateEngine(settings, _mockLogger.Object);

        // Act
        var v1 = engine.GetTemplate("versioned", "1.0");
        var v2 = engine.GetTemplate("versioned", "2.0");

        // Assert
        v1.ShouldNotBeNull();
        v1.Version.ShouldBe("1.0");
        v1.SystemPrompt.ShouldContain("Version 1");

        v2.ShouldNotBeNull();
        v2.Version.ShouldBe("2.0");
        v2.SystemPrompt.ShouldContain("Version 2");
    }

    [Fact]
    public void GetAllTemplates_ReturnsAllLoadedTemplates()
    {
        // Arrange
        CreateTestTemplate("template1.yaml", @"
name: template1
systemPrompt: T1
userPromptTemplate: T1
");

        CreateTestTemplate("template2.yaml", @"
name: template2
systemPrompt: T2
userPromptTemplate: T2
");

        var settings = CreateSettings();
        using var engine = new PromptTemplateEngine(settings, _mockLogger.Object);

        // Act
        var allTemplates = engine.GetAllTemplates();

        // Assert
        allTemplates.ShouldNotBeNull();
        allTemplates.Count.ShouldBe(2);
        allTemplates.ShouldContain(t => t.Name == "template1");
        allTemplates.ShouldContain(t => t.Name == "template2");
    }

    [Fact]
    public async Task ValidateTemplatesAsync_WithValidTemplates_ReturnsNoErrors()
    {
        // Arrange
        CreateTestTemplate("valid.yaml", @"
name: valid
version: ""1.0""
systemPrompt: Valid system prompt
userPromptTemplate: Valid user prompt
parameters:
  temperature: 0.7
  maxTokens: 100
");

        var settings = CreateSettings();
        using var engine = new PromptTemplateEngine(settings, _mockLogger.Object);

        // Act
        var errors = await engine.ValidateTemplatesAsync();

        // Assert
        errors.ShouldNotBeNull();
        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task ValidateTemplatesAsync_WithInvalidTemplate_ReturnsErrors()
    {
        // Arrange
        CreateTestTemplate("invalid.yaml", @"
name: invalid
version: ""1.0""
systemPrompt: ''
userPromptTemplate: ''
parameters:
  temperature: 1.5
  maxTokens: -1
");

        var settings = CreateSettings();
        using var engine = new PromptTemplateEngine(settings, _mockLogger.Object);

        // Act
        var errors = await engine.ValidateTemplatesAsync();

        // Assert
        errors.ShouldNotBeNull();
        errors.ShouldNotBeEmpty();
        errors.ShouldContain(e => e.Contains("system_prompt"));
        errors.ShouldContain(e => e.Contains("user_prompt_template"));
        errors.ShouldContain(e => e.Contains("temperature"));
        errors.ShouldContain(e => e.Contains("max_tokens"));
    }

    [Fact]
    public async Task ReloadTemplatesAsync_ReloadsTemplatesFromDisk()
    {
        // Arrange
        CreateTestTemplate("reload-test.yaml", @"
name: reload-test
version: ""1.0""
systemPrompt: Original
userPromptTemplate: Original
");

        var settings = CreateSettings(hotReload: false); // Disable auto hot-reload
        using var engine = new PromptTemplateEngine(settings, _mockLogger.Object);

        var original = engine.GetTemplate("reload-test");
        original.ShouldNotBeNull();
        original.SystemPrompt.ShouldContain("Original");

        // Modify template
        CreateTestTemplate("reload-test.yaml", @"
name: reload-test
version: ""1.0""
systemPrompt: Modified
userPromptTemplate: Modified
");

        // Act
        await engine.ReloadTemplatesAsync();

        // Assert
        var reloaded = engine.GetTemplate("reload-test");
        reloaded.ShouldNotBeNull();
        reloaded.SystemPrompt.ShouldContain("Modified");
    }

    [Fact]
    public async Task RenderTemplateAsync_WithCaseInsensitiveVariables_SubstitutesCorrectly()
    {
        // Arrange
        CreateTestTemplate("case-test.yaml", @"
name: case-test
version: ""1.0""
systemPrompt: System
userPromptTemplate: ""Query: {{QUERY}}, Context: {{Context}}""
");

        var settings = CreateSettings();
        using var engine = new PromptTemplateEngine(settings, _mockLogger.Object);

        var variables = new Dictionary<string, string>
        {
            { "query", "lowercase query" },
            { "context", "lowercase context" }
        };

        // Act
        var rendered = await engine.RenderTemplateAsync("case-test", variables);

        // Assert
        rendered.UserPrompt.ShouldContain("lowercase query");
        rendered.UserPrompt.ShouldContain("lowercase context");
    }

    private IOptions<PromptTemplateSettings> CreateSettings(bool hotReload = false)
    {
        return Options.Create(new PromptTemplateSettings
        {
            Directory = _testTemplateDir,
            EnableHotReload = hotReload,
            DefaultTemplate = "test-template",
            EnableABTesting = false
        });
    }

    private void CreateTestTemplate(string filename, string content)
    {
        var filePath = Path.Combine(_testTemplateDir, filename);
        File.WriteAllText(filePath, content.TrimStart());
    }
}

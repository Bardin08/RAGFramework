using RAG.Evaluation.Export;
using Xunit;
using FluentAssertions;

namespace RAG.Tests.Unit.Evaluation.Export;

public class ResultsExporterFactoryTests
{
    private readonly ResultsExporterFactory _factory;

    public ResultsExporterFactoryTests()
    {
        _factory = new ResultsExporterFactory();
    }

    [Theory]
    [InlineData("csv", typeof(CsvResultsExporter))]
    [InlineData("CSV", typeof(CsvResultsExporter))]
    [InlineData("json", typeof(JsonResultsExporter))]
    [InlineData("JSON", typeof(JsonResultsExporter))]
    [InlineData("md", typeof(MarkdownResultsExporter))]
    [InlineData("markdown", typeof(MarkdownResultsExporter))]
    [InlineData("Markdown", typeof(MarkdownResultsExporter))]
    public void GetExporter_Should_Return_Correct_Exporter_Type(string format, Type expectedType)
    {
        // Act
        var exporter = _factory.GetExporter(format);

        // Assert
        exporter.Should().BeOfType(expectedType);
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("json")]
    [InlineData("md")]
    [InlineData("markdown")]
    public void IsFormatSupported_Should_Return_True_For_Valid_Formats(string format)
    {
        // Act
        var isSupported = _factory.IsFormatSupported(format);

        // Assert
        isSupported.Should().BeTrue();
    }

    [Theory]
    [InlineData("xml")]
    [InlineData("yaml")]
    [InlineData("pdf")]
    [InlineData("")]
    [InlineData(null)]
    public void IsFormatSupported_Should_Return_False_For_Invalid_Formats(string? format)
    {
        // Act
        var isSupported = _factory.IsFormatSupported(format!);

        // Assert
        isSupported.Should().BeFalse();
    }

    [Fact]
    public void GetExporter_Should_Throw_For_Invalid_Format()
    {
        // Act
        Action act = () => _factory.GetExporter("invalid");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Unknown export format*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void GetExporter_Should_Throw_For_Null_Or_Empty_Format(string? format)
    {
        // Act
        Action act = () => _factory.GetExporter(format!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Format cannot be null or empty*");
    }

    [Fact]
    public void GetSupportedFormats_Should_Return_All_Formats()
    {
        // Act
        var formats = _factory.GetSupportedFormats();

        // Assert
        formats.Should().Contain("csv");
        formats.Should().Contain("json");
        formats.Should().Contain("md");
        formats.Should().Contain("markdown");
        formats.Should().HaveCountGreaterOrEqualTo(4);
    }

    [Fact]
    public void GetExporter_Should_Be_Case_Insensitive()
    {
        // Arrange
        var formats = new[] { "CSV", "csv", "Csv", "CsV" };

        // Act & Assert
        foreach (var format in formats)
        {
            var exporter = _factory.GetExporter(format);
            exporter.Should().BeOfType<CsvResultsExporter>();
        }
    }

    [Fact]
    public void GetExporter_Should_Trim_Whitespace()
    {
        // Act
        var exporter = _factory.GetExporter("  csv  ");

        // Assert
        exporter.Should().BeOfType<CsvResultsExporter>();
    }

    [Fact]
    public void GetExporter_Should_Create_New_Instance_Each_Time()
    {
        // Act
        var exporter1 = _factory.GetExporter("csv");
        var exporter2 = _factory.GetExporter("csv");

        // Assert
        exporter1.Should().NotBeSameAs(exporter2);
    }
}

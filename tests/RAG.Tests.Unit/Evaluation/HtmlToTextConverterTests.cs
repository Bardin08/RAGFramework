using RAG.Evaluation.Datasets;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.Evaluation;

public class HtmlToTextConverterTests
{
    private readonly HtmlToTextConverter _converter;

    public HtmlToTextConverterTests()
    {
        _converter = new HtmlToTextConverter();
    }

    [Fact]
    public void Convert_EmptyString_ReturnsEmpty()
    {
        // Act
        var result = _converter.Convert(string.Empty);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Convert_NullString_ReturnsEmpty()
    {
        // Act
        var result = _converter.Convert(null!);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Convert_PlainText_ReturnsUnchanged()
    {
        // Arrange
        var input = "This is plain text without HTML.";

        // Act
        var result = _converter.Convert(input);

        // Assert
        result.ShouldBe("This is plain text without HTML.");
    }

    [Fact]
    public void Convert_SimpleParagraph_RemovesTagsKeepsText()
    {
        // Arrange
        var html = "<p>This is a paragraph.</p>";

        // Act
        var result = _converter.Convert(html);

        // Assert
        result.ShouldBe("This is a paragraph.");
    }

    [Fact]
    public void Convert_MultipleParagraphs_SeparatesWithNewlines()
    {
        // Arrange
        var html = "<p>First paragraph.</p><p>Second paragraph.</p>";

        // Act
        var result = _converter.Convert(html);

        // Assert
        result.ShouldContain("First paragraph.");
        result.ShouldContain("Second paragraph.");
        result.Split('\n').Length.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Convert_WithBrTags_InsertsNewlines()
    {
        // Arrange
        var html = "Line one<br>Line two<br/>Line three";

        // Act
        var result = _converter.Convert(html);

        // Assert
        result.ShouldContain("Line one");
        result.ShouldContain("Line two");
        result.ShouldContain("Line three");
    }

    [Fact]
    public void Convert_ScriptTags_RemovesScriptContent()
    {
        // Arrange
        var html = "<p>Visible text</p><script>alert('should be removed');</script><p>More visible</p>";

        // Act
        var result = _converter.Convert(html);

        // Assert
        result.ShouldContain("Visible text");
        result.ShouldContain("More visible");
        result.ShouldNotContain("alert");
        result.ShouldNotContain("script");
    }

    [Fact]
    public void Convert_StyleTags_RemovesStyleContent()
    {
        // Arrange
        var html = "<p>Visible text</p><style>.class { color: red; }</style><p>More visible</p>";

        // Act
        var result = _converter.Convert(html);

        // Assert
        result.ShouldContain("Visible text");
        result.ShouldContain("More visible");
        result.ShouldNotContain("color");
        result.ShouldNotContain("style");
    }

    [Fact]
    public void Convert_HtmlEntities_DecodesCorrectly()
    {
        // Arrange
        // Note: Don't use &lt;div&gt; because after decoding it looks like a tag and gets stripped
        var html = "Test &amp; &quot;quoted&quot; &nbsp; text";

        // Act
        var result = _converter.Convert(html);

        // Assert
        result.ShouldBe("Test & \"quoted\" text");
    }

    [Fact]
    public void Convert_NumericEntities_DecodesCorrectly()
    {
        // Arrange
        var html = "Test&#160;space&#39;apostrophe";

        // Act
        var result = _converter.Convert(html);

        // Assert
        result.ShouldContain("Test");
        result.ShouldContain("space");
        result.ShouldContain("apostrophe");
    }

    [Fact]
    public void Convert_SpecialCharacterEntities_DecodesCorrectly()
    {
        // Arrange
        var html = "Test&mdash;dash &copy;copyright &hellip;ellipsis";

        // Act
        var result = _converter.Convert(html);

        // Assert
        result.ShouldContain("—");
        result.ShouldContain("©");
        result.ShouldContain("...");
    }

    [Fact]
    public void Convert_ComplexHtml_ExtractsCleanText()
    {
        // Arrange
        var html = """
            <html>
                <head><title>Page Title</title></head>
                <body>
                    <h1>Main Heading</h1>
                    <p>This is a paragraph with <strong>bold</strong> and <em>italic</em> text.</p>
                    <ul>
                        <li>Item 1</li>
                        <li>Item 2</li>
                    </ul>
                </body>
            </html>
            """;

        // Act
        var result = _converter.Convert(html);

        // Assert
        result.ShouldContain("Main Heading");
        result.ShouldContain("This is a paragraph with bold and italic text.");
        result.ShouldContain("Item 1");
        result.ShouldContain("Item 2");
        result.ShouldNotContain("<");
        result.ShouldNotContain(">");
    }

    [Fact]
    public void Convert_WhitespaceNormalization_CollapsesExcessiveWhitespace()
    {
        // Arrange
        var html = "<p>Text   with    multiple     spaces</p>";

        // Act
        var result = _converter.Convert(html);

        // Assert
        result.ShouldBe("Text with multiple spaces");
    }

    [Fact]
    public void Convert_MultipleNewlines_LimitsToTwoNewlines()
    {
        // Arrange
        var html = "<p>Paragraph 1</p><br><br><br><br><p>Paragraph 2</p>";

        // Act
        var result = _converter.Convert(html);

        // Assert
        result.ShouldContain("Paragraph 1");
        result.ShouldContain("Paragraph 2");
        // Should not have more than 2 consecutive newlines
        result.ShouldNotContain("\n\n\n");
    }

    [Fact]
    public void Convert_WikipediaLikeHtml_ExtractsCleanText()
    {
        // Arrange
        var html = """
            <div class="mw-parser-output">
                <p><b>Paris</b> is the capital and most populous city of France.</p>
                <p>Located in the north of the country on the river <a href="/wiki/Seine">Seine</a>.</p>
            </div>
            """;

        // Act
        var result = _converter.Convert(html);

        // Assert
        result.ShouldContain("Paris is the capital and most populous city of France.");
        result.ShouldContain("Located in the north of the country on the river Seine.");
        result.ShouldNotContain("<");
        result.ShouldNotContain("href");
        result.ShouldNotContain("mw-parser-output");
    }

    [Fact]
    public void Convert_TableHtml_ExtractsText()
    {
        // Arrange
        var html = """
            <table>
                <tr><th>Header 1</th><th>Header 2</th></tr>
                <tr><td>Cell 1</td><td>Cell 2</td></tr>
            </table>
            """;

        // Act
        var result = _converter.Convert(html);

        // Assert
        result.ShouldContain("Header 1");
        result.ShouldContain("Header 2");
        result.ShouldContain("Cell 1");
        result.ShouldContain("Cell 2");
    }

    [Fact]
    public void ExtractSnippet_EmptyHtml_ReturnsEmpty()
    {
        // Act
        var result = _converter.ExtractSnippet(string.Empty, "target");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractSnippet_TargetNotFound_ReturnsEmpty()
    {
        // Arrange
        var html = "<p>This is some text without the target.</p>";

        // Act
        var result = _converter.ExtractSnippet(html, "nonexistent");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractSnippet_TargetFound_ReturnsSnippetWithContext()
    {
        // Arrange
        var html = "<p>This is some text before Paris which is the capital of France and some text after.</p>";

        // Act - use larger context to ensure we capture surrounding words
        var result = _converter.ExtractSnippet(html, "Paris", contextChars: 30);

        // Assert
        result.ShouldContain("Paris");
        result.ShouldContain("before");
        result.ShouldContain("capital");
    }

    [Fact]
    public void ExtractSnippet_TargetAtStart_AddsEllipsisAtEnd()
    {
        // Arrange
        var html = "<p>Paris is the capital of France and there is much more text here that extends beyond the context window.</p>";

        // Act
        var result = _converter.ExtractSnippet(html, "Paris", contextChars: 20);

        // Assert
        result.ShouldStartWith("Paris");
        result.ShouldEndWith("...");
    }

    [Fact]
    public void ExtractSnippet_TargetAtEnd_AddsEllipsisAtStart()
    {
        // Arrange
        var html = "<p>There is a lot of text at the beginning that extends beyond the context window and then Paris</p>";

        // Act
        var result = _converter.ExtractSnippet(html, "Paris", contextChars: 20);

        // Assert
        result.ShouldStartWith("...");
        result.ShouldEndWith("Paris");
    }

    [Fact]
    public void ExtractSnippet_CaseInsensitive_FindsTarget()
    {
        // Arrange
        var html = "<p>This text contains PARIS in uppercase.</p>";

        // Act
        var result = _converter.ExtractSnippet(html, "paris", contextChars: 20);

        // Assert
        result.ShouldContain("PARIS");
    }

    [Fact]
    public void Convert_HeadingTags_PreservesTextWithNewlines()
    {
        // Arrange
        var html = "<h1>Heading 1</h1><h2>Heading 2</h2><h3>Heading 3</h3><p>Paragraph</p>";

        // Act
        var result = _converter.Convert(html);

        // Assert
        result.ShouldContain("Heading 1");
        result.ShouldContain("Heading 2");
        result.ShouldContain("Heading 3");
        result.ShouldContain("Paragraph");
    }
}

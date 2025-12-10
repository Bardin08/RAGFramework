using Microsoft.Extensions.Logging.Abstractions;
using RAG.Evaluation.Datasets;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.Evaluation;

public class NaturalQuestionsParserTests
{
    private readonly NaturalQuestionsParser _parser;

    public NaturalQuestionsParserTests()
    {
        _parser = new NaturalQuestionsParser(NullLogger<NaturalQuestionsParser>.Instance);
    }

    [Fact]
    public async Task ParseAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = "/nonexistent/file.jsonl";

        // Act & Assert
        await Should.ThrowAsync<FileNotFoundException>(
            async () => await _parser.ParseAsync(nonExistentPath));
    }

    [Fact]
    public async Task ParseAsync_EmptyFile_ReturnsEmptyList()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, string.Empty);

        try
        {
            // Act
            var result = await _parser.ParseAsync(tempFile);

            // Assert
            result.ShouldNotBeNull();
            result.ShouldBeEmpty();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseAsync_ValidEntry_ParsesCorrectly()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var jsonl = """
            {"question_text":"What is the capital of France?","document_url":"https://en.wikipedia.org/wiki/Paris","document_html":"<html><body>Paris is the capital of France.</body></html>","document_title":"Paris","document_tokens":[{"token":"Paris","html_token":false},{"token":"is","html_token":false},{"token":"the","html_token":false},{"token":"capital","html_token":false}],"annotations":[{"short_answers":[{"start_token":0,"end_token":1}],"long_answer":{"start_token":0,"end_token":4}}]}
            """;

        await File.WriteAllTextAsync(tempFile, jsonl);

        try
        {
            // Act
            var result = await _parser.ParseAsync(tempFile);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(1);

            var entry = result[0];
            entry.QuestionText.ShouldBe("What is the capital of France?");
            entry.DocumentUrl.ShouldBe("https://en.wikipedia.org/wiki/Paris");
            entry.DocumentTitle.ShouldBe("Paris");
            entry.DocumentHtml.ShouldBe("<html><body>Paris is the capital of France.</body></html>");
            entry.HasShortAnswer.ShouldBeTrue();
            entry.ShortAnswers.Count.ShouldBe(1);
            entry.ShortAnswers[0].Text.ShouldBe("Paris");
            entry.LongAnswer.ShouldNotBeNull();
            entry.LongAnswer!.Text.ShouldBe("Paris is the capital");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseAsync_EntryWithNoShortAnswer_ParsesCorrectly()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var jsonl = """
            {"question_text":"What is a test question?","document_url":"https://en.wikipedia.org/wiki/Test","document_html":"<html><body>Test content</body></html>","document_title":"Test","document_tokens":[{"token":"Test","html_token":false},{"token":"content","html_token":false}],"annotations":[{"short_answers":[],"long_answer":{"start_token":0,"end_token":2}}]}
            """;

        await File.WriteAllTextAsync(tempFile, jsonl);

        try
        {
            // Act
            var result = await _parser.ParseAsync(tempFile);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(1);

            var entry = result[0];
            entry.QuestionText.ShouldBe("What is a test question?");
            entry.HasShortAnswer.ShouldBeFalse();
            entry.ShortAnswers.ShouldBeEmpty();
            entry.LongAnswer.ShouldNotBeNull();
            entry.LongAnswer!.Text.ShouldBe("Test content");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseAsync_MultipleEntries_ParsesAll()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var jsonl = """
            {"question_text":"Question 1?","document_url":"https://en.wikipedia.org/wiki/Test1","document_html":"<html>Test1</html>","document_title":"Test1","document_tokens":[{"token":"Test1","html_token":false}],"annotations":[{"short_answers":[{"start_token":0,"end_token":1}],"long_answer":{"start_token":0,"end_token":1}}]}
            {"question_text":"Question 2?","document_url":"https://en.wikipedia.org/wiki/Test2","document_html":"<html>Test2</html>","document_title":"Test2","document_tokens":[{"token":"Test2","html_token":false}],"annotations":[{"short_answers":[{"start_token":0,"end_token":1}],"long_answer":{"start_token":0,"end_token":1}}]}
            {"question_text":"Question 3?","document_url":"https://en.wikipedia.org/wiki/Test3","document_html":"<html>Test3</html>","document_title":"Test3","document_tokens":[{"token":"Test3","html_token":false}],"annotations":[{"short_answers":[{"start_token":0,"end_token":1}],"long_answer":{"start_token":0,"end_token":1}}]}
            """;

        await File.WriteAllTextAsync(tempFile, jsonl);

        try
        {
            // Act
            var result = await _parser.ParseAsync(tempFile);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(3);
            result[0].QuestionText.ShouldBe("Question 1?");
            result[1].QuestionText.ShouldBe("Question 2?");
            result[2].QuestionText.ShouldBe("Question 3?");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseAsync_MalformedJson_SkipsInvalidLines()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var jsonl = """
            {"question_text":"Valid question?","document_url":"https://en.wikipedia.org/wiki/Valid","document_html":"<html>Valid</html>","document_title":"Valid","document_tokens":[{"token":"Valid","html_token":false}],"annotations":[{"short_answers":[{"start_token":0,"end_token":1}],"long_answer":{"start_token":0,"end_token":1}}]}
            {invalid json line}
            {"question_text":"Another valid?","document_url":"https://en.wikipedia.org/wiki/Valid2","document_html":"<html>Valid2</html>","document_title":"Valid2","document_tokens":[{"token":"Valid2","html_token":false}],"annotations":[{"short_answers":[{"start_token":0,"end_token":1}],"long_answer":{"start_token":0,"end_token":1}}]}
            """;

        await File.WriteAllTextAsync(tempFile, jsonl);

        try
        {
            // Act
            var result = await _parser.ParseAsync(tempFile);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(2); // Should skip the malformed line
            result[0].QuestionText.ShouldBe("Valid question?");
            result[1].QuestionText.ShouldBe("Another valid?");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseAsync_EntryWithMultipleShortAnswers_ParsesAll()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var jsonl = """
            {"question_text":"Who are famous scientists?","document_url":"https://en.wikipedia.org/wiki/Scientists","document_html":"<html>Scientists</html>","document_title":"Scientists","document_tokens":[{"token":"Einstein","html_token":false},{"token":"and","html_token":false},{"token":"Newton","html_token":false}],"annotations":[{"short_answers":[{"start_token":0,"end_token":1},{"start_token":2,"end_token":3}],"long_answer":{"start_token":0,"end_token":3}}]}
            """;

        await File.WriteAllTextAsync(tempFile, jsonl);

        try
        {
            // Act
            var result = await _parser.ParseAsync(tempFile);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(1);

            var entry = result[0];
            entry.HasShortAnswer.ShouldBeTrue();
            entry.ShortAnswers.Count.ShouldBe(2);
            entry.ShortAnswers[0].Text.ShouldBe("Einstein");
            entry.ShortAnswers[1].Text.ShouldBe("Newton");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseAsync_EntryWithMissingQuestionText_SkipsEntry()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var jsonl = """
            {"document_url":"https://en.wikipedia.org/wiki/Test","document_html":"<html>Test</html>","document_title":"Test","document_tokens":[{"token":"Test","html_token":false}],"annotations":[{"short_answers":[{"start_token":0,"end_token":1}],"long_answer":{"start_token":0,"end_token":1}}]}
            {"question_text":"Valid question?","document_url":"https://en.wikipedia.org/wiki/Valid","document_html":"<html>Valid</html>","document_title":"Valid","document_tokens":[{"token":"Valid","html_token":false}],"annotations":[{"short_answers":[{"start_token":0,"end_token":1}],"long_answer":{"start_token":0,"end_token":1}}]}
            """;

        await File.WriteAllTextAsync(tempFile, jsonl);

        try
        {
            // Act
            var result = await _parser.ParseAsync(tempFile);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(1); // Should skip the entry without question_text
            result[0].QuestionText.ShouldBe("Valid question?");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseAsync_BlankLines_SkipsBlankLines()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var jsonl = """
            {"question_text":"Question 1?","document_url":"https://en.wikipedia.org/wiki/Test1","document_html":"<html>Test1</html>","document_title":"Test1","document_tokens":[{"token":"Test1","html_token":false}],"annotations":[{"short_answers":[{"start_token":0,"end_token":1}],"long_answer":{"start_token":0,"end_token":1}}]}

            {"question_text":"Question 2?","document_url":"https://en.wikipedia.org/wiki/Test2","document_html":"<html>Test2</html>","document_title":"Test2","document_tokens":[{"token":"Test2","html_token":false}],"annotations":[{"short_answers":[{"start_token":0,"end_token":1}],"long_answer":{"start_token":0,"end_token":1}}]}
            """;

        await File.WriteAllTextAsync(tempFile, jsonl);

        try
        {
            // Act
            var result = await _parser.ParseAsync(tempFile);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(2);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}

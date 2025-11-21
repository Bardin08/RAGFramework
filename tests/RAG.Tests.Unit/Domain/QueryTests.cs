using RAG.Core.Domain;
using RAG.Core.Domain.Enums;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.Domain;

public class QueryTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesQuery()
    {
        // Arrange
        var id = Guid.NewGuid();
        var text = "What is RAG?";
        var language = "en";
        var timestamp = DateTime.UtcNow;
        var queryType = QueryType.ExplicitFact;

        // Act
        var query = new Query(id, text, language, timestamp, queryType);

        // Assert
        query.ShouldNotBeNull();
        query.Id.ShouldBe(id);
        query.Text.ShouldBe(text);
        query.Language.ShouldBe(language);
        query.Timestamp.ShouldBe(timestamp);
        query.QueryType.ShouldBe(queryType);
    }

    [Fact]
    public void Constructor_WithEmptyId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() =>
            new Query(Guid.Empty, "test", "en", DateTime.UtcNow, QueryType.ExplicitFact))
            .Message.ShouldContain("Query ID cannot be empty");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidText_ThrowsArgumentException(string invalidText)
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() =>
            new Query(Guid.NewGuid(), invalidText, "en", DateTime.UtcNow, QueryType.ExplicitFact))
            .Message.ShouldContain("Query text cannot be empty");
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("")]
    public void Constructor_WithInvalidLanguage_ThrowsArgumentException(string invalidLanguage)
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() =>
            new Query(Guid.NewGuid(), "test", invalidLanguage, DateTime.UtcNow, QueryType.ExplicitFact))
            .Message.ShouldContain("Language must be 'en' or 'uk'");
    }

    [Theory]
    [InlineData("en")]
    [InlineData("uk")]
    public void Constructor_WithValidLanguage_CreatesQuery(string language)
    {
        // Arrange & Act
        var query = new Query(Guid.NewGuid(), "test", language, DateTime.UtcNow, QueryType.ExplicitFact);

        // Assert
        query.ShouldNotBeNull();
        query.Language.ShouldBe(language);
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var query1 = new Query(id, "test", "en", timestamp, QueryType.ExplicitFact);
        var query2 = new Query(id, "test", "en", timestamp, QueryType.ExplicitFact);

        // Act & Assert
        query1.ShouldBe(query2);
        (query1 == query2).ShouldBeTrue();
    }

    [Fact]
    public void RecordEquality_WithDifferentValues_AreNotEqual()
    {
        // Arrange
        var query1 = new Query(Guid.NewGuid(), "test1", "en", DateTime.UtcNow, QueryType.ExplicitFact);
        var query2 = new Query(Guid.NewGuid(), "test2", "en", DateTime.UtcNow, QueryType.ExplicitFact);

        // Act & Assert
        query1.ShouldNotBe(query2);
        (query1 != query2).ShouldBeTrue();
    }

    [Fact]
    public void AllQueryTypes_CanBeUsed()
    {
        // Arrange & Act
        var explicitFact = new Query(Guid.NewGuid(), "test", "en", DateTime.UtcNow, QueryType.ExplicitFact);
        var implicitFact = new Query(Guid.NewGuid(), "test", "en", DateTime.UtcNow, QueryType.ImplicitFact);
        var interpretableRationale = new Query(Guid.NewGuid(), "test", "en", DateTime.UtcNow, QueryType.InterpretableRationale);
        var hiddenRationale = new Query(Guid.NewGuid(), "test", "en", DateTime.UtcNow, QueryType.HiddenRationale);

        // Assert
        explicitFact.QueryType.ShouldBe(QueryType.ExplicitFact);
        implicitFact.QueryType.ShouldBe(QueryType.ImplicitFact);
        interpretableRationale.QueryType.ShouldBe(QueryType.InterpretableRationale);
        hiddenRationale.QueryType.ShouldBe(QueryType.HiddenRationale);
    }
}

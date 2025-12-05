using RAG.Evaluation.Metrics.Generation;
using RAG.Evaluation.Models;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.Evaluation;

public class GenerationMetricsTests
{
    [Theory]
    [InlineData("Paris", "paris", true)]
    [InlineData("Paris", "Paris", true)]
    [InlineData("Paris", "London", false)]
    [InlineData("The answer is Paris", "Paris", false)]
    public async Task ExactMatch_VariousCases_ReturnsExpected(
        string response, string groundTruth, bool shouldMatch)
    {
        var metric = new ExactMatchMetric();
        var context = new EvaluationContext
        {
            Query = "test",
            Response = response,
            GroundTruth = groundTruth
        };

        var result = await metric.CalculateAsync(context);

        result.ShouldBe(shouldMatch ? 1.0 : 0.0);
    }

    [Fact]
    public async Task TokenF1_PartialOverlap_CalculatesCorrectly()
    {
        // "the quick brown fox" vs "quick brown dog"
        // Generated tokens: {the, quick, brown, fox}
        // Expected tokens: {quick, brown, dog}
        // Overlap: {quick, brown} = 2
        // Precision: 2/4 = 0.5
        // Recall: 2/3 = 0.667
        // F1 = 2 * 0.5 * 0.667 / (0.5 + 0.667) ≈ 0.571

        var metric = new TokenF1Metric();
        var context = new EvaluationContext
        {
            Query = "test",
            Response = "the quick brown fox",
            GroundTruth = "quick brown dog"
        };

        var result = await metric.CalculateAsync(context);

        result.ShouldBe(0.571, tolerance: 0.01);
    }

    [Fact]
    public async Task TokenF1_ExactMatch_Returns1()
    {
        var metric = new TokenF1Metric();
        var context = new EvaluationContext
        {
            Query = "test",
            Response = "Paris is the capital",
            GroundTruth = "Paris is the capital"
        };

        var result = await metric.CalculateAsync(context);

        result.ShouldBe(1.0, tolerance: 0.001);
    }

    [Fact]
    public async Task TokenF1_NoOverlap_ReturnsZero()
    {
        var metric = new TokenF1Metric();
        var context = new EvaluationContext
        {
            Query = "test",
            Response = "apple orange banana",
            GroundTruth = "car truck bus"
        };

        var result = await metric.CalculateAsync(context);

        result.ShouldBe(0.0);
    }

    [Fact]
    public async Task BleuScore_HighOverlap_ReturnsHighScore()
    {
        var metric = new BleuScoreMetric(maxN: 4);
        var context = new EvaluationContext
        {
            Query = "test",
            Response = "The cat sat on the mat",
            GroundTruth = "The cat sat on the mat"
        };

        var result = await metric.CalculateAsync(context);

        result.ShouldBe(1.0, tolerance: 0.001);
    }

    [Fact]
    public async Task BleuScore_NoOverlap_ReturnsZero()
    {
        var metric = new BleuScoreMetric(maxN: 4);
        var context = new EvaluationContext
        {
            Query = "test",
            Response = "apple orange banana",
            GroundTruth = "car truck bus"
        };

        var result = await metric.CalculateAsync(context);

        result.ShouldBe(0.0);
    }

    [Fact]
    public async Task RougeL_HighOverlap_ReturnsHighScore()
    {
        var metric = new RougeLScoreMetric();
        var context = new EvaluationContext
        {
            Query = "test",
            Response = "The cat sat on the mat",
            GroundTruth = "The cat sat on the mat"
        };

        var result = await metric.CalculateAsync(context);

        result.ShouldBe(1.0, tolerance: 0.001);
    }

    [Fact]
    public async Task RougeL_PartialMatch_CalculatesLCS()
    {
        // LCS of "the cat sat" and "the cat ran" is "the cat" (length 2)
        var metric = new RougeLScoreMetric();
        var context = new EvaluationContext
        {
            Query = "test",
            Response = "the cat sat",
            GroundTruth = "the cat ran"
        };

        var result = await metric.CalculateAsync(context);

        // LCS = 2 tokens, R = 2/3, P = 2/3, F1 = 2/3
        result.ShouldBe(2.0 / 3.0, tolerance: 0.001);
    }

    [Fact]
    public async Task Rouge1_UnigramOverlap_CalculatesCorrectly()
    {
        var metric = new Rouge1ScoreMetric();
        var context = new EvaluationContext
        {
            Query = "test",
            Response = "the quick brown fox",
            GroundTruth = "the quick red fox"
        };

        // Overlap: {the, quick, fox} = 3
        // P = 3/4, R = 3/4, F1 = 3/4 = 0.75
        var result = await metric.CalculateAsync(context);

        result.ShouldBe(0.75, tolerance: 0.001);
    }

    [Fact]
    public void TextNormalizer_Normalize_RemovesPunctuationAndLowercase()
    {
        var result = TextNormalizer.Normalize("Hello, World! How are you?");

        result.ShouldBe("hello world how are you");
    }

    [Fact]
    public void TextNormalizer_Tokenize_SplitsOnWhitespace()
    {
        var tokens = TextNormalizer.Tokenize("hello world test");

        tokens.ShouldBe(new[] { "hello", "world", "test" });
    }

    [Fact]
    public void TextNormalizer_ExtractNGrams_ExtractsCorrectly()
    {
        var tokens = new[] { "the", "quick", "brown", "fox" };
        var bigrams = TextNormalizer.ExtractNGrams(tokens, 2);

        bigrams.ShouldBe(new[] { "the quick", "quick brown", "brown fox" });
    }

    [Fact]
    public void TextNormalizer_LongestCommonSubsequence_CalculatesCorrectly()
    {
        var seq1 = new[] { "the", "cat", "sat" };
        var seq2 = new[] { "the", "cat", "ran" };

        var lcs = TextNormalizer.LongestCommonSubsequence(seq1, seq2);

        lcs.ShouldBe(2); // "the", "cat"
    }
}

using RAG.Evaluation.Experiments;
using Shouldly;
using System.Text.Json;

namespace RAG.Tests.Unit.Evaluation;

public class ConfigurationExperimentTests
{
    [Fact]
    public void IsValid_WithValidConfiguration_ReturnsTrue()
    {
        // Arrange
        var experiment = new ConfigurationExperiment
        {
            Name = "Test Experiment",
            Dataset = "natural-questions",
            BaseConfiguration = new Dictionary<string, object>
            {
                ["topK"] = 10
            },
            Variants = new List<ExperimentVariant>
            {
                new() { Name = "Variant A", Parameters = new VariantParameters() },
                new() { Name = "Variant B", Parameters = new VariantParameters() }
            },
            Metrics = new List<string> { "precision@10", "recall@10" }
        };

        // Act
        var isValid = experiment.IsValid();

        // Assert
        isValid.ShouldBeTrue();
    }

    [Fact]
    public void IsValid_WithEmptyName_ReturnsFalse()
    {
        // Arrange
        var experiment = new ConfigurationExperiment
        {
            Name = "",
            Dataset = "natural-questions",
            Variants = new List<ExperimentVariant>
            {
                new() { Name = "Variant A", Parameters = new VariantParameters() },
                new() { Name = "Variant B", Parameters = new VariantParameters() }
            },
            Metrics = new List<string> { "precision@10" }
        };

        // Act
        var isValid = experiment.IsValid();

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WithSingleVariant_ReturnsFalse()
    {
        // Arrange
        var experiment = new ConfigurationExperiment
        {
            Name = "Test",
            Dataset = "natural-questions",
            Variants = new List<ExperimentVariant>
            {
                new() { Name = "Variant A", Parameters = new VariantParameters() }
            },
            Metrics = new List<string> { "precision@10" }
        };

        // Act
        var isValid = experiment.IsValid();

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WithNoMetrics_ReturnsFalse()
    {
        // Arrange
        var experiment = new ConfigurationExperiment
        {
            Name = "Test",
            Dataset = "natural-questions",
            Variants = new List<ExperimentVariant>
            {
                new() { Name = "Variant A", Parameters = new VariantParameters() },
                new() { Name = "Variant B", Parameters = new VariantParameters() }
            },
            Metrics = new List<string>()
        };

        // Act
        var isValid = experiment.IsValid();

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public void Deserialize_FromJson_CreatesValidObject()
    {
        // Arrange
        var json = @"{
            ""name"": ""Retrieval Strategy Comparison"",
            ""dataset"": ""natural-questions"",
            ""baseConfiguration"": {
                ""topK"": 10,
                ""llmTemperature"": 0.0
            },
            ""variants"": [
                {
                    ""name"": ""BM25-Only"",
                    ""parameters"": {
                        ""retrievalStrategy"": ""bm25""
                    }
                },
                {
                    ""name"": ""Dense-Only"",
                    ""parameters"": {
                        ""retrievalStrategy"": ""dense""
                    }
                }
            ],
            ""metrics"": [""precision@10"", ""recall@10"", ""mrr"", ""f1""],
            ""primaryMetric"": ""f1""
        }";

        // Act
        var experiment = JsonSerializer.Deserialize<ConfigurationExperiment>(json);

        // Assert
        experiment.ShouldNotBeNull();
        experiment.Name.ShouldBe("Retrieval Strategy Comparison");
        experiment.Dataset.ShouldBe("natural-questions");
        experiment.Variants.Count.ShouldBe(2);
        experiment.Variants[0].Name.ShouldBe("BM25-Only");
        experiment.Variants[0].Parameters.RetrievalStrategy.ShouldBe("bm25");
        experiment.Metrics.Count.ShouldBe(4);
        experiment.PrimaryMetric.ShouldBe("f1");
        experiment.IsValid().ShouldBeTrue();
    }

    [Fact]
    public void ExperimentVariant_MergeWithBase_CombinesParameters()
    {
        // Arrange
        var baseConfig = new Dictionary<string, object>
        {
            ["topK"] = 10,
            ["llmTemperature"] = 0.0
        };

        var variant = new ExperimentVariant
        {
            Name = "Test Variant",
            Parameters = new VariantParameters
            {
                RetrievalStrategy = "hybrid",
                HybridAlpha = 0.7
            }
        };

        // Act
        var merged = variant.MergeWithBase(baseConfig);

        // Assert
        merged.Count.ShouldBe(4);
        merged["topK"].ShouldBe(10);
        merged["llmTemperature"].ShouldBe(0.0);
        merged["retrievalStrategy"].ShouldBe("hybrid");
        merged["hybridAlpha"].ShouldBe(0.7);
    }

    [Fact]
    public void ExperimentVariant_MergeWithBase_OverridesBaseValues()
    {
        // Arrange
        var baseConfig = new Dictionary<string, object>
        {
            ["topK"] = 10
        };

        var variant = new ExperimentVariant
        {
            Name = "Test Variant",
            Parameters = new VariantParameters
            {
                TopK = 20
            }
        };

        // Act
        var merged = variant.MergeWithBase(baseConfig);

        // Assert
        merged["topK"].ShouldBe(20); // Variant overrides base
    }

    [Fact]
    public void ExperimentVariant_MergeWithBase_IgnoresNullParameters()
    {
        // Arrange
        var baseConfig = new Dictionary<string, object>
        {
            ["topK"] = 10
        };

        var variant = new ExperimentVariant
        {
            Name = "Test Variant",
            Parameters = new VariantParameters
            {
                RetrievalStrategy = null,
                HybridAlpha = null
            }
        };

        // Act
        var merged = variant.MergeWithBase(baseConfig);

        // Assert
        merged.Count.ShouldBe(1); // Only base parameter
        merged.ContainsKey("retrievalStrategy").ShouldBeFalse();
        merged.ContainsKey("hybridAlpha").ShouldBeFalse();
    }

    [Fact]
    public void ExperimentVariant_IsValid_WithValidName_ReturnsTrue()
    {
        // Arrange
        var variant = new ExperimentVariant
        {
            Name = "Valid Variant"
        };

        // Act
        var isValid = variant.IsValid();

        // Assert
        isValid.ShouldBeTrue();
    }

    [Fact]
    public void ExperimentVariant_IsValid_WithEmptyName_ReturnsFalse()
    {
        // Arrange
        var variant = new ExperimentVariant
        {
            Name = ""
        };

        // Act
        var isValid = variant.IsValid();

        // Assert
        isValid.ShouldBeFalse();
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RAG.Evaluation.Datasets;

/// <summary>
/// Example usage of TriviaQA dataset processing and evaluation.
/// </summary>
public static class TriviaQAExample
{
    /// <summary>
    /// Example: Process a TriviaQA dataset file.
    /// </summary>
    public static async Task ProcessTriviaQADatasetExample(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<TriviaQAProcessingService>>();
        var parserLogger = serviceProvider.GetRequiredService<ILogger<TriviaQAParser>>();

        var parser = new TriviaQAParser(parserLogger);
        var processingService = new TriviaQAProcessingService(parser, logger);

        // Process the dataset
        var result = await processingService.ProcessDatasetAsync(
            rawFilePath: "data/benchmarks/triviaqa/raw/wikipedia-train.json",
            outputBaseDirectory: "data/benchmarks/triviaqa",
            datasetName: "TriviaQA-Wikipedia-Train"
        );

        if (result.Success)
        {
            Console.WriteLine($"Successfully processed TriviaQA dataset:");
            Console.WriteLine($"  Total Questions: {result.TotalQuestions}");
            Console.WriteLine($"  Total Documents: {result.TotalDocuments}");
            Console.WriteLine($"  Valid Ground Truth Entries: {result.ValidGroundTruthEntries}");
            Console.WriteLine($"  Validation Errors: {result.ValidationErrors}");
            Console.WriteLine($"  Documents saved to: {result.DocumentsOutputPath}");
            Console.WriteLine($"  Ground truth saved to: {result.GroundTruthOutputPath}");
            Console.WriteLine($"  Processing time: {result.Duration?.TotalSeconds:F2} seconds");
        }
        else
        {
            Console.WriteLine($"Failed to process dataset: {result.ErrorMessage}");
        }
    }

    /// <summary>
    /// Example: Parse and extract documents from TriviaQA.
    /// </summary>
    public static async Task ParseAndExtractExample(IServiceProvider serviceProvider)
    {
        var parserLogger = serviceProvider.GetRequiredService<ILogger<TriviaQAParser>>();
        var parser = new TriviaQAParser(parserLogger);

        // Parse the TriviaQA file
        var entries = await parser.ParseAsync("data/benchmarks/triviaqa/raw/sample-triviaqa.json");

        Console.WriteLine($"Parsed {entries.Count} TriviaQA entries");

        // Extract documents from entries
        var documents = parser.ExtractDocuments(entries);

        Console.WriteLine($"Extracted {documents.Count} documents:");
        foreach (var doc in documents)
        {
            Console.WriteLine($"  - {doc.DocumentId}: {doc.Title} ({doc.Source})");
        }

        // Convert to ground truth
        var groundTruth = parser.ConvertToGroundTruth(entries, "Sample-TriviaQA");

        Console.WriteLine($"\nGround Truth Dataset:");
        Console.WriteLine($"  Name: {groundTruth.Name}");
        Console.WriteLine($"  Valid Entries: {groundTruth.Entries.Count}");
        Console.WriteLine($"  Validation Errors: {groundTruth.ValidationErrors.Count}");

        // Show first entry with answer aliases
        if (groundTruth.Entries.Count > 0)
        {
            var firstEntry = groundTruth.Entries[0];
            Console.WriteLine($"\nFirst Entry:");
            Console.WriteLine($"  Query: {firstEntry.Query}");
            Console.WriteLine($"  Expected Answer: {firstEntry.ExpectedAnswer}");
            Console.WriteLine($"  Answer Aliases: {string.Join(", ", firstEntry.AnswerAliases ?? Array.Empty<string>())}");
            Console.WriteLine($"  All Valid Answers: {string.Join(", ", firstEntry.GetAllValidAnswers())}");
            Console.WriteLine($"  Relevant Documents: {firstEntry.RelevantDocumentIds.Count}");
        }
    }

    /// <summary>
    /// Example: Load processed documents for indexing.
    /// </summary>
    public static async Task LoadProcessedDocumentsExample(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<TriviaQAProcessingService>>();
        var parserLogger = serviceProvider.GetRequiredService<ILogger<TriviaQAParser>>();

        var parser = new TriviaQAParser(parserLogger);
        var processingService = new TriviaQAProcessingService(parser, logger);

        // Load previously processed documents
        var documents = await processingService.LoadProcessedDocumentsAsync(
            "data/benchmarks/triviaqa/processed/documents"
        );

        Console.WriteLine($"Loaded {documents.Count} processed documents");

        // Group by source
        var bySource = documents.GroupBy(d => d.Source);
        foreach (var group in bySource)
        {
            Console.WriteLine($"  {group.Key}: {group.Count()} documents");
        }

        // Show document details
        foreach (var doc in documents.Take(3))
        {
            Console.WriteLine($"\nDocument: {doc.DocumentId}");
            Console.WriteLine($"  Title: {doc.Title}");
            Console.WriteLine($"  Source: {doc.Source}");
            Console.WriteLine($"  URL: {doc.Url ?? "N/A"}");
            Console.WriteLine($"  Content Length: {doc.Content.Length} characters");
            Console.WriteLine($"  Metadata: {string.Join(", ", doc.Metadata.Keys)}");
        }
    }

    /// <summary>
    /// Example: Answer alias handling in evaluation.
    /// </summary>
    public static void AnswerAliasExample()
    {
        // TriviaQA answer with multiple valid forms
        var answer = new TriviaQAAnswer
        {
            Value = "Paris",
            Aliases = new List<string> { "paris", "Paris, France", "Paree" },
            NormalizedAliases = new List<string> { "paris", "paris, france", "paree" }
        };

        Console.WriteLine("TriviaQA Answer with Aliases:");
        Console.WriteLine($"  Primary Answer: {answer.Value}");
        Console.WriteLine($"  All Valid Answers: {string.Join(", ", answer.GetAllValidAnswers())}");
        Console.WriteLine($"  Normalized Answers: {string.Join(", ", answer.GetAllNormalizedAnswers())}");

        // In evaluation, check if generated answer matches any valid form
        var generatedAnswer = "paris"; // lowercase variant

        var isCorrect = answer.GetAllValidAnswers()
            .Any(valid => valid.Equals(generatedAnswer, StringComparison.OrdinalIgnoreCase));

        Console.WriteLine($"\nGenerated answer '{generatedAnswer}' is {(isCorrect ? "CORRECT" : "INCORRECT")}");
    }

    /// <summary>
    /// Example: Complete workflow from raw data to evaluation.
    /// </summary>
    public static async Task CompleteWorkflowExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("=== TriviaQA Complete Workflow ===\n");

        // Step 1: Process the raw dataset
        Console.WriteLine("Step 1: Processing raw TriviaQA dataset...");
        await ProcessTriviaQADatasetExample(serviceProvider);

        // Step 2: Load processed documents
        Console.WriteLine("\nStep 2: Loading processed documents...");
        await LoadProcessedDocumentsExample(serviceProvider);

        // Step 3: Index documents (pseudo-code, requires DocumentIndexingService)
        Console.WriteLine("\nStep 3: Index documents into RAG system");
        Console.WriteLine("  (Use DocumentIndexingService to index each document)");

        // Step 4: Run evaluation (pseudo-code, requires EvaluationRunner)
        Console.WriteLine("\nStep 4: Run evaluation with ground truth");
        Console.WriteLine("  (Use EvaluationRunner with ground-truth.json)");
        Console.WriteLine("  Metrics: ExactMatch, TokenF1, Precision@K, Recall@K");

        // Step 5: Review results
        Console.WriteLine("\nStep 5: Review evaluation results");
        Console.WriteLine("  Results saved to: ./evaluation-results/triviaqa/");
    }
}

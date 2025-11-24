using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Application.Interfaces;
using RAG.Core.Configuration;

namespace RAG.Application.TextProcessing;

/// <summary>
/// Configurable text cleaner that applies multiple cleaning strategies.
/// </summary>
public class ConfigurableTextCleaner : ITextCleanerService
{
    private readonly IEnumerable<ITextCleaningStrategy> _strategies;
    private readonly TextCleaningSettings _settings;
    private readonly ILogger<ConfigurableTextCleaner> _logger;

    public ConfigurableTextCleaner(
        IEnumerable<ITextCleaningStrategy> strategies,
        IOptions<TextCleaningSettings> settings,
        ILogger<ConfigurableTextCleaner> logger)
    {
        _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _settings.Validate();
    }

    /// <inheritdoc />
    public string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        _logger.LogDebug("Starting text cleaning with {StrategyCount} strategies. Original length: {Length} characters",
            _strategies.Count(), text.Length);

        var cleaned = text;
        var appliedStrategies = new List<string>();

        foreach (var strategy in _strategies.OrderBy(s => GetStrategyOrder(s.Name)))
        {
            if (strategy.IsApplicable(cleaned))
            {
                var before = cleaned.Length;
                cleaned = strategy.Apply(cleaned);
                var after = cleaned.Length;

                appliedStrategies.Add(strategy.Name);

                _logger.LogTrace(
                    "Applied strategy '{StrategyName}': {Before} chars -> {After} chars ({Reduction:F1}% reduction)",
                    strategy.Name, before, after, (1 - (double)after / before) * 100);
            }
        }

        _logger.LogDebug(
            "Text cleaning completed. Applied {AppliedCount} strategies: {Strategies}. " +
            "Original: {OriginalLength} chars -> Cleaned: {CleanedLength} chars ({ReductionPercent:F1}% reduction)",
            appliedStrategies.Count,
            string.Join(", ", appliedStrategies),
            text.Length,
            cleaned.Length,
            (1 - (double)cleaned.Length / text.Length) * 100);

        return cleaned;
    }

    /// <summary>
    /// Determines the order in which strategies should be applied.
    /// </summary>
    private int GetStrategyOrder(string strategyName)
    {
        // Order matters for text cleaning
        return strategyName switch
        {
            "UnicodeNormalization" => 1,
            "FormArtifactRemoval" => 2,
            "WordSpacingFix" => 3,
            "WhitespaceNormalization" => 4,
            "RepetitiveContentRemoval" => 5,
            "TableFormattingCleanup" => 6,
            "FinalCleanup" => 7,
            _ => 99 // Unknown strategies run last
        };
    }
}

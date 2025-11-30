using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Application.Configuration;
using RAG.Application.Interfaces;
using RAG.Application.Models;
using System.Text.RegularExpressions;

namespace RAG.Application.Services;

/// <summary>
/// Detects hallucinations in LLM responses through context grounding validation.
/// Uses n-gram matching to verify claims are supported by retrieved context.
/// </summary>
public class HallucinationDetector : IHallucinationDetector
{
    private readonly HallucinationDetectionSettings _settings;
    private readonly ILogger<HallucinationDetector> _logger;

    // Regex for sentence splitting
    private static readonly Regex SentenceSplitRegex = new(
        @"(?<=[.!?])\s+(?=[A-Z])",
        RegexOptions.Compiled);

    public HallucinationDetector(
        IOptions<HallucinationDetectionSettings> settings,
        ILogger<HallucinationDetector> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<HallucinationResult> DetectAsync(
        string response,
        string context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new ArgumentException("Response cannot be empty", nameof(response));
        }

        if (string.IsNullOrWhiteSpace(context))
        {
            _logger.LogWarning("Empty context provided for hallucination detection");
        }

        var startTime = DateTime.UtcNow;
        var issues = new List<string>();
        decimal groundingScore = 0m;
        var claimGroundings = new List<ClaimGrounding>();

        // Task 2: Context grounding check
        if (_settings.EnableContextGrounding)
        {
            var (score, claims) = await CheckContextGroundingAsync(response, context, cancellationToken);
            groundingScore = score;
            claimGroundings = claims;

            if (groundingScore < _settings.GroundingThreshold)
            {
                issues.Add($"Low context grounding score: {groundingScore:F2} (threshold: {_settings.GroundingThreshold:F2})");
            }
        }
        else
        {
            // If grounding disabled, assume perfect grounding
            groundingScore = 1.0m;
        }

        // Task 5: Calculate overall confidence score
        decimal overallConfidence = CalculateOverallConfidence(
            groundingScore,
            consistencyScore: null, // Self-consistency not implemented yet
            faithfulnessScore: null // LLM-as-judge not implemented yet
        );

        // Classify confidence level
        var confidenceLevel = overallConfidence > 0.85m ? ConfidenceLevel.High :
                            overallConfidence >= 0.7m ? ConfidenceLevel.Medium :
                            ConfidenceLevel.Low;

        // Determine if human review is required
        bool requiresHumanReview = _settings.EnableHumanReview &&
                                   overallConfidence < _settings.MinConfidence;

        var detectionTime = DateTime.UtcNow - startTime;
        _logger.LogInformation(
            "Hallucination detection completed: OverallConfidence={Confidence:F2}, GroundingScore={GroundingScore:F2}, Level={Level}, Time={Time}ms",
            overallConfidence,
            groundingScore,
            confidenceLevel,
            detectionTime.TotalMilliseconds);

        return new HallucinationResult
        {
            OverallConfidence = overallConfidence,
            ConfidenceLevel = confidenceLevel,
            GroundingScore = groundingScore,
            ConsistencyScore = null,
            FaithfulnessScore = null,
            Issues = issues,
            RequiresHumanReview = requiresHumanReview,
            ClaimGroundings = claimGroundings
        };
    }

    /// <summary>
    /// Checks context grounding using n-gram matching.
    /// </summary>
    private async Task<(decimal score, List<ClaimGrounding> claims)> CheckContextGroundingAsync(
        string response,
        string context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            // No context = no grounding
            return (0m, new List<ClaimGrounding>());
        }

        // Split response into sentences/claims
        var claims = SplitIntoClaims(response);

        if (claims.Count == 0)
        {
            return (1.0m, new List<ClaimGrounding>()); // Empty response = perfect grounding
        }

        var claimGroundings = new List<ClaimGrounding>();
        decimal totalScore = 0m;

        foreach (var claim in claims)
        {
            // Calculate n-gram overlap for this claim
            var ngramScore = CalculateNGramOverlap(claim, context);

            var isGrounded = ngramScore >= _settings.GroundingThreshold;

            claimGroundings.Add(new ClaimGrounding
            {
                Claim = claim,
                Score = ngramScore,
                IsGrounded = isGrounded
            });

            totalScore += ngramScore;

            if (!isGrounded)
            {
                _logger.LogDebug(
                    "Low grounding for claim: '{Claim}' (score: {Score:F2})",
                    claim.Length > 100 ? claim.Substring(0, 100) + "..." : claim,
                    ngramScore);
            }
        }

        var averageScore = totalScore / claims.Count;

        await Task.CompletedTask; // Placeholder for async operations (e.g., embedding similarity)

        return (averageScore, claimGroundings);
    }

    /// <summary>
    /// Splits response text into individual claims/sentences.
    /// </summary>
    private List<string> SplitIntoClaims(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        // Split by sentences
        var sentences = SentenceSplitRegex
            .Split(text)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

        return sentences;
    }

    /// <summary>
    /// Calculates n-gram overlap between claim and context.
    /// Uses trigrams and bigrams for matching.
    /// </summary>
    private decimal CalculateNGramOverlap(string claim, string context)
    {
        var claimLower = claim.ToLowerInvariant();
        var contextLower = context.ToLowerInvariant();

        // Extract trigrams from claim
        var trigrams = ExtractNGrams(claimLower, n: 3);
        var bigramsFromTrigrams = ExtractNGrams(claimLower, n: 2);

        if (trigrams.Count == 0 && bigramsFromTrigrams.Count == 0)
        {
            // Very short claim, use word matching
            return CalculateWordOverlap(claimLower, contextLower);
        }

        // Count matching trigrams
        int matchingTrigrams = trigrams.Count(ngram => contextLower.Contains(ngram));

        // Count matching bigrams
        int matchingBigrams = bigramsFromTrigrams.Count(ngram => contextLower.Contains(ngram));

        // Calculate overlap ratio
        // Weight: 60% trigrams, 40% bigrams
        decimal trigramRatio = trigrams.Count > 0 ? (decimal)matchingTrigrams / trigrams.Count : 0m;
        decimal bigramRatio = bigramsFromTrigrams.Count > 0 ? (decimal)matchingBigrams / bigramsFromTrigrams.Count : 0m;

        decimal ngramScore = (trigramRatio * 0.6m) + (bigramRatio * 0.4m);

        return ngramScore;
    }

    /// <summary>
    /// Extracts n-grams from text.
    /// </summary>
    private List<string> ExtractNGrams(string text, int n)
    {
        var words = text.Split(new[] { ' ', '\t', '\n', '\r', '.', ',', '!', '?' },
            StringSplitOptions.RemoveEmptyEntries);

        var ngrams = new List<string>();

        for (int i = 0; i <= words.Length - n; i++)
        {
            var ngram = string.Join(" ", words.Skip(i).Take(n));
            ngrams.Add(ngram);
        }

        return ngrams;
    }

    /// <summary>
    /// Fallback: word overlap for very short claims.
    /// </summary>
    private decimal CalculateWordOverlap(string claim, string context)
    {
        var claimWords = claim.Split(new[] { ' ', '\t', '\n', '\r', '.', ',', '!', '?' },
            StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2) // Filter out very short words
            .Distinct()
            .ToList();

        if (claimWords.Count == 0)
            return 0m;

        int matchingWords = claimWords.Count(word => context.Contains(word));

        return (decimal)matchingWords / claimWords.Count;
    }

    /// <summary>
    /// Calculates overall confidence from individual scores.
    /// </summary>
    private decimal CalculateOverallConfidence(
        decimal groundingScore,
        decimal? consistencyScore,
        decimal? faithfulnessScore)
    {
        decimal confidence = groundingScore * _settings.GroundingWeight;

        if (consistencyScore.HasValue)
        {
            confidence += consistencyScore.Value * _settings.ConsistencyWeight;
        }

        if (faithfulnessScore.HasValue)
        {
            confidence += faithfulnessScore.Value * _settings.FaithfulnessWeight;
        }

        // Normalize if some checks are disabled
        decimal totalWeight = _settings.GroundingWeight;
        if (consistencyScore.HasValue) totalWeight += _settings.ConsistencyWeight;
        if (faithfulnessScore.HasValue) totalWeight += _settings.FaithfulnessWeight;

        if (totalWeight > 0 && totalWeight != 1.0m)
        {
            confidence = confidence / totalWeight;
        }

        return Math.Clamp(confidence, 0m, 1m);
    }
}

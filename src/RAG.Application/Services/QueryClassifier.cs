using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Application.Configuration;
using RAG.Application.Interfaces;
using RAG.Core.Domain.Enums;

namespace RAG.Application.Services;

/// <summary>
/// LLM-based query classifier with heuristic fallback and caching.
/// Classifies user queries into QueryType categories to enable optimal retrieval strategy selection.
/// </summary>
public class QueryClassifier : IQueryClassifier
{
    private readonly ILlmProvider? _llmProvider;
    private readonly IMemoryCache _cache;
    private readonly ILogger<QueryClassifier> _logger;
    private readonly QueryClassificationConfig _config;
    private readonly HeuristicClassifier _fallback;

    public QueryClassifier(
        IMemoryCache cache,
        ILogger<QueryClassifier> logger,
        IOptions<QueryClassificationConfig> config,
        ILlmProvider? llmProvider = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _llmProvider = llmProvider; // Optional - may be null if Epic 5 not implemented yet
        _fallback = new HeuristicClassifier();
    }

    /// <summary>
    /// Classifies a user query into one of the QueryType categories.
    /// Uses LLM classification with caching and heuristic fallback.
    /// </summary>
    public async Task<QueryType> ClassifyQueryAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogWarning("Empty query provided for classification");
            return QueryType.ExplicitFact;
        }

        // 1. Generate cache key (SHA256 hash of normalized query)
        var cacheKey = GenerateCacheKey(query);

        // 2. Check cache
        if (_config.EnableCache && _cache.TryGetValue<QueryType>(cacheKey, out var cachedType))
        {
            _logger.LogDebug("Cache hit for query classification: {CacheKey}", cacheKey);
            return cachedType;
        }

        QueryType queryType;

        // 3. Try LLM classification if provider is available
        if (_llmProvider != null)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMilliseconds(_config.Timeout));

                queryType = await ClassifyWithLlmAsync(query, cts.Token);

                _logger.LogInformation(
                    "LLM classification successful: Query={QueryPreview}, Type={QueryType}",
                    query.Length > 50 ? query.Substring(0, 50) + "..." : query,
                    queryType);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("LLM classification timeout after {Timeout}ms, falling back to heuristics", _config.Timeout);
                queryType = FallbackToHeuristics(query);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LLM classification failed, falling back to heuristics");
                queryType = FallbackToHeuristics(query);
            }
        }
        else
        {
            // LLM provider not available (Epic 5 not implemented), use heuristics
            _logger.LogDebug("LLM provider not available, using heuristic classification");
            queryType = FallbackToHeuristics(query);
        }

        // 4. Store in cache
        if (_config.EnableCache)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _config.CacheDuration
            };
            _cache.Set(cacheKey, queryType, cacheOptions);
            _logger.LogDebug("Cached classification result: {CacheKey} -> {QueryType}", cacheKey, queryType);
        }

        return queryType;
    }

    private async Task<QueryType> ClassifyWithLlmAsync(string query, CancellationToken cancellationToken)
    {
        if (_llmProvider == null)
        {
            throw new InvalidOperationException("LLM provider is not configured");
        }

        // Build prompt for LLM
        var prompt = BuildClassificationPrompt(query);

        // Call LLM
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await _llmProvider.GenerateAsync(prompt, cancellationToken);
        stopwatch.Stop();

        _logger.LogInformation(
            "LLM call completed: Provider={Provider}, Model={Model}, Latency={Latency}ms",
            _config.Provider,
            _config.Model,
            stopwatch.ElapsedMilliseconds);

        // Parse response to QueryType
        return ParseQueryType(response);
    }

    private string BuildClassificationPrompt(string query)
    {
        // Simple inline prompt (YAML template will be added in separate task)
        return $@"You are a query classifier for a RAG system. Classify user queries into exactly one of these four types:

1. explicit_fact - Direct factual questions (who, what, when, where)
2. implicit_fact - Questions requiring inference (why, how)
3. interpretable_rationale - Complex questions requiring reasoning
4. hidden_rationale - Abstract or opinion-based questions

Respond with ONLY the type name, nothing else.

Query: ""{query}""

Classification:";
    }

    private QueryType ParseQueryType(string llmResponse)
    {
        if (string.IsNullOrWhiteSpace(llmResponse))
        {
            _logger.LogWarning("Empty LLM response, falling back to heuristics");
            return QueryType.ExplicitFact;
        }

        // Normalize response (remove whitespace, lowercase, remove underscores)
        var normalized = llmResponse.Trim().ToLowerInvariant().Replace("_", "").Replace("-", "");

        // Try to map to QueryType
        return normalized switch
        {
            var s when s.Contains("explicitfact") || s.Contains("explicit") => QueryType.ExplicitFact,
            var s when s.Contains("implicitfact") || s.Contains("implicit") => QueryType.ImplicitFact,
            var s when s.Contains("interpretablerationale") || s.Contains("interpretable") => QueryType.InterpretableRationale,
            var s when s.Contains("hiddenrationale") || s.Contains("hidden") => QueryType.HiddenRationale,
            _ => ParseLegacyFormat(llmResponse)
        };
    }

    private QueryType ParseLegacyFormat(string llmResponse)
    {
        // Try parsing numeric responses (1, 2, 3, 4)
        var trimmed = llmResponse.Trim();
        return trimmed switch
        {
            "1" => QueryType.ExplicitFact,
            "2" => QueryType.ImplicitFact,
            "3" => QueryType.InterpretableRationale,
            "4" => QueryType.HiddenRationale,
            _ => DefaultFallback(llmResponse)
        };
    }

    private QueryType DefaultFallback(string llmResponse)
    {
        _logger.LogWarning("Unable to parse LLM response: {Response}, defaulting to ExplicitFact", llmResponse);
        return QueryType.ExplicitFact;
    }

    private QueryType FallbackToHeuristics(string query)
    {
        var result = _fallback.Classify(query);
        _logger.LogInformation("Used heuristic classification: Query={QueryPreview}, Type={QueryType}",
            query.Length > 50 ? query.Substring(0, 50) + "..." : query,
            result);
        return result;
    }

    private static string GenerateCacheKey(string query)
    {
        // Normalize query for consistent caching
        var normalized = query.Trim().ToLowerInvariant();

        // Generate SHA256 hash
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return $"qc_{Convert.ToHexString(hashBytes)[..16]}"; // Use first 16 chars of hash
    }
}

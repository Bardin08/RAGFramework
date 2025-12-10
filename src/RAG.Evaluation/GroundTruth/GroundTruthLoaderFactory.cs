using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Evaluation.Configuration;
using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.GroundTruth;

/// <summary>
/// Factory for selecting and using the appropriate ground truth loader.
/// Includes caching of loaded datasets.
/// </summary>
public class GroundTruthLoaderFactory
{
    private readonly IEnumerable<IGroundTruthLoader> _loaders;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GroundTruthLoaderFactory> _logger;
    private readonly EvaluationOptions _options;

    public GroundTruthLoaderFactory(
        IEnumerable<IGroundTruthLoader> loaders,
        IMemoryCache cache,
        ILogger<GroundTruthLoaderFactory> logger,
        IOptions<EvaluationOptions> options)
    {
        _loaders = loaders;
        _cache = cache;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Loads ground truth data from the specified path, using caching.
    /// </summary>
    /// <param name="path">The file path to load from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded ground truth dataset.</returns>
    public async Task<GroundTruthDataset> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateCacheKey(path);

        if (_cache.TryGetValue<GroundTruthDataset>(cacheKey, out var cached) && cached is not null)
        {
            _logger.LogDebug("Ground truth cache hit for: {Path}", path);
            return cached;
        }

        var loader = GetLoaderForPath(path);
        if (loader is null)
        {
            var supportedFormats = GetSupportedFormats();
            return new GroundTruthDataset
            {
                SourcePath = path,
                ValidationErrors = [$"Unsupported file format. Supported formats: {string.Join(", ", supportedFormats)}"]
            };
        }

        var dataset = await loader.LoadAsync(path, cancellationToken);

        // Cache successful loads
        if (dataset.IsValid)
        {
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(_options.GroundTruthCacheMinutes));

            _cache.Set(cacheKey, dataset, cacheOptions);
            _logger.LogDebug("Ground truth cached for: {Path}", path);
        }

        return dataset;
    }

    /// <summary>
    /// Invalidates the cache for a specific path.
    /// </summary>
    public void InvalidateCache(string path)
    {
        var cacheKey = GenerateCacheKey(path);
        _cache.Remove(cacheKey);
        _logger.LogDebug("Ground truth cache invalidated for: {Path}", path);
    }

    /// <summary>
    /// Gets the loader that can handle the specified file path.
    /// </summary>
    public IGroundTruthLoader? GetLoaderForPath(string path)
    {
        return _loaders.FirstOrDefault(l => l.CanHandle(path));
    }

    /// <summary>
    /// Gets all supported file formats.
    /// </summary>
    public IReadOnlyList<string> GetSupportedFormats()
    {
        return _loaders.SelectMany(l => l.SupportedExtensions).Distinct().ToList();
    }

    private static string GenerateCacheKey(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var lastModified = File.Exists(fullPath)
            ? File.GetLastWriteTimeUtc(fullPath).Ticks.ToString()
            : "0";

        return $"groundtruth:{fullPath}:{lastModified}";
    }
}

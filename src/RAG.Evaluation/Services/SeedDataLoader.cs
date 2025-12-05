using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.Services;

/// <summary>
/// Service for loading seed datasets for reproducible evaluations.
/// </summary>
public class SeedDataLoader : ISeedDataLoader
{
    private readonly ISeedDatasetRepository _seedDatasetRepository;
    private readonly IDocumentIndexingService _documentIndexingService;
    private readonly IDocumentStorageService _storageService;
    private readonly ILogger<SeedDataLoader> _logger;

    private const string SeedsDirectory = "data/seeds";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SeedDataLoader(
        ISeedDatasetRepository seedDatasetRepository,
        IDocumentIndexingService documentIndexingService,
        IDocumentStorageService storageService,
        ILogger<SeedDataLoader> logger)
    {
        _seedDatasetRepository = seedDatasetRepository ?? throw new ArgumentNullException(nameof(seedDatasetRepository));
        _documentIndexingService = documentIndexingService ?? throw new ArgumentNullException(nameof(documentIndexingService));
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<SeedDataLoadResult> LoadDatasetByNameAsync(
        string datasetName,
        Guid loadedBy,
        Guid tenantId,
        bool forceReload = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading seed dataset '{DatasetName}' for tenant {TenantId}",
            datasetName, tenantId);

        try
        {
            // Find the dataset file
            var filePath = Path.Combine(SeedsDirectory, $"{datasetName}.json");
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Seed dataset file not found: {FilePath}", filePath);
                return new SeedDataLoadResult
                {
                    Success = false,
                    DatasetName = datasetName,
                    Error = $"Seed dataset file not found: {filePath}"
                };
            }

            // Read and parse the JSON
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return await LoadDatasetFromJsonAsync(json, loadedBy, tenantId, forceReload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load seed dataset '{DatasetName}'", datasetName);
            return new SeedDataLoadResult
            {
                Success = false,
                DatasetName = datasetName,
                Error = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public async Task<SeedDataLoadResult> LoadDatasetFromJsonAsync(
        string datasetJson,
        Guid loadedBy,
        Guid tenantId,
        bool forceReload = false,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Parse the JSON
            var dataset = JsonSerializer.Deserialize<SeedDatasetFile>(datasetJson, JsonOptions);
            if (dataset == null)
            {
                return new SeedDataLoadResult
                {
                    Success = false,
                    DatasetName = "unknown",
                    Error = "Failed to parse seed dataset JSON"
                };
            }

            // Validate the dataset
            var (isValid, errors) = dataset.Validate();
            if (!isValid)
            {
                return new SeedDataLoadResult
                {
                    Success = false,
                    DatasetName = dataset.Name,
                    ValidationErrors = errors,
                    Error = $"Dataset validation failed with {errors.Count} errors"
                };
            }

            // Calculate hash for idempotency
            var hash = ComputeHash(datasetJson);

            // Check if already loaded with same hash
            var existingDataset = await _seedDatasetRepository.GetByNameAsync(dataset.Name, cancellationToken);

            if (existingDataset != null && existingDataset.Hash == hash && !forceReload)
            {
                _logger.LogInformation(
                    "Seed dataset '{DatasetName}' already loaded with same hash. Skipping.",
                    dataset.Name);

                stopwatch.Stop();
                return new SeedDataLoadResult
                {
                    Success = true,
                    DatasetName = dataset.Name,
                    DocumentsLoaded = existingDataset.DocumentsCount,
                    QueriesCount = existingDataset.QueriesCount,
                    WasAlreadyLoaded = true,
                    Hash = hash,
                    Duration = stopwatch.Elapsed
                };
            }

            // Clear existing data if force reload or different hash
            if (existingDataset != null)
            {
                _logger.LogInformation("Removing existing seed dataset '{DatasetName}'", dataset.Name);
                await _seedDatasetRepository.ClearTenantDataAsync(tenantId, cancellationToken);
                await _seedDatasetRepository.DeleteByNameAsync(dataset.Name, cancellationToken);
            }

            // Load documents
            var documentsLoaded = 0;
            foreach (var doc in dataset.Documents)
            {
                try
                {
                    await LoadDocumentAsync(doc, tenantId, loadedBy, cancellationToken);
                    documentsLoaded++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load document '{DocId}'", doc.Id);
                }
            }

            // Create the seed dataset record
            var seedDataset = new SeedDataset(
                id: Guid.NewGuid(),
                name: dataset.Name,
                hash: hash,
                loadedAt: DateTime.UtcNow,
                documentsCount: documentsLoaded,
                queriesCount: dataset.Queries?.Count ?? 0,
                loadedBy: loadedBy,
                version: dataset.Version,
                metadata: dataset.Metadata
            );

            await _seedDatasetRepository.CreateAsync(seedDataset, cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "Successfully loaded seed dataset '{DatasetName}' with {DocumentCount} documents in {Duration}ms",
                dataset.Name, documentsLoaded, stopwatch.ElapsedMilliseconds);

            return new SeedDataLoadResult
            {
                Success = true,
                DatasetName = dataset.Name,
                DocumentsLoaded = documentsLoaded,
                QueriesCount = dataset.Queries?.Count ?? 0,
                Hash = hash,
                Duration = stopwatch.Elapsed,
                Metadata = new Dictionary<string, object>
                {
                    ["version"] = dataset.Version ?? "unknown",
                    ["loadedBy"] = loadedBy.ToString()
                }
            };
        }
        catch (JsonException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to parse seed dataset JSON");
            return new SeedDataLoadResult
            {
                Success = false,
                DatasetName = "unknown",
                Error = $"JSON parsing error: {ex.Message}",
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to load seed dataset");
            return new SeedDataLoadResult
            {
                Success = false,
                DatasetName = "unknown",
                Error = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    /// <inheritdoc />
    public async Task<List<string>> ListAvailableDatasetsAsync(CancellationToken cancellationToken = default)
    {
        var datasets = new List<string>();

        if (!Directory.Exists(SeedsDirectory))
        {
            _logger.LogWarning("Seeds directory not found: {Directory}", SeedsDirectory);
            return datasets;
        }

        var files = Directory.GetFiles(SeedsDirectory, "*.json");
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            datasets.Add(name);
        }

        return await Task.FromResult(datasets);
    }

    /// <inheritdoc />
    public async Task<SeedDataset?> GetLoadedDatasetAsync(
        string datasetName,
        CancellationToken cancellationToken = default)
    {
        return await _seedDatasetRepository.GetByNameAsync(datasetName, cancellationToken);
    }

    /// <summary>
    /// Computes SHA-256 hash of the dataset content.
    /// </summary>
    private static string ComputeHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Loads a single document from seed data.
    /// </summary>
    private async Task LoadDocumentAsync(
        SeedDocument doc,
        Guid tenantId,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        // Create unique document ID based on seed document ID
        var documentId = CreateDocumentId(doc.Id);

        // Save content to storage
        var content = Encoding.UTF8.GetBytes(doc.Content);
        using var stream = new MemoryStream(content);
        var fileName = $"seed-{doc.Id}.txt";

        await _storageService.SaveFileAsync(
            documentId,
            tenantId,
            fileName,
            stream,
            cancellationToken);

        // Index the document
        await _documentIndexingService.IndexDocumentAsync(
            documentId: documentId,
            tenantId: tenantId,
            ownerId: ownerId,
            fileName: fileName,
            title: doc.Title,
            source: doc.Source ?? "seed-data",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates a deterministic GUID from a seed document ID.
    /// </summary>
    private static Guid CreateDocumentId(string seedDocId)
    {
        var uniqueString = $"seed-{seedDocId}";
        var bytes = Encoding.UTF8.GetBytes(uniqueString);
        var hash = MD5.HashData(bytes);
        return new Guid(hash);
    }
}

namespace RAG.Core.Domain;

/// <summary>
/// Represents the persisted results of a configuration experiment run.
/// </summary>
public class ConfigurationExperimentResult
{
    /// <summary>
    /// Unique identifier for this experiment result.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Name of the experiment.
    /// </summary>
    public required string ExperimentName { get; init; }

    /// <summary>
    /// Name of the configuration variant.
    /// </summary>
    public required string VariantName { get; init; }

    /// <summary>
    /// Configuration parameters used for this variant (stored as JSON).
    /// </summary>
    public required string Configuration { get; init; }

    /// <summary>
    /// Metric results for this variant (stored as JSON).
    /// </summary>
    public required string Metrics { get; init; }

    /// <summary>
    /// Composite score calculated for this variant.
    /// </summary>
    public double CompositeScore { get; init; }

    /// <summary>
    /// Whether this variant was the winner of the experiment.
    /// </summary>
    public bool IsWinner { get; init; }

    /// <summary>
    /// Statistical significance information (stored as JSON).
    /// Contains p-values, t-statistics, and effect sizes from comparisons.
    /// </summary>
    public string? StatisticalSignificance { get; init; }

    /// <summary>
    /// When the experiment was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the experiment completed.
    /// </summary>
    public DateTime CompletedAt { get; init; }

    /// <summary>
    /// Who initiated the experiment.
    /// </summary>
    public Guid? InitiatedBy { get; init; }

    /// <summary>
    /// Tenant ID for multi-tenancy isolation.
    /// </summary>
    public Guid? TenantId { get; init; }

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private ConfigurationExperimentResult()
    {
        ExperimentName = string.Empty;
        VariantName = string.Empty;
        Configuration = string.Empty;
        Metrics = string.Empty;
    }

    /// <summary>
    /// Creates a new ConfigurationExperimentResult instance.
    /// </summary>
    public ConfigurationExperimentResult(
        Guid id,
        string experimentName,
        string variantName,
        string configuration,
        string metrics,
        double compositeScore,
        bool isWinner,
        DateTime? createdAt = null,
        DateTime? completedAt = null,
        string? statisticalSignificance = null,
        Guid? initiatedBy = null,
        Guid? tenantId = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("ID cannot be empty", nameof(id));

        if (string.IsNullOrWhiteSpace(experimentName))
            throw new ArgumentException("Experiment name cannot be empty", nameof(experimentName));

        if (string.IsNullOrWhiteSpace(variantName))
            throw new ArgumentException("Variant name cannot be empty", nameof(variantName));

        if (string.IsNullOrWhiteSpace(configuration))
            throw new ArgumentException("Configuration cannot be empty", nameof(configuration));

        if (string.IsNullOrWhiteSpace(metrics))
            throw new ArgumentException("Metrics cannot be empty", nameof(metrics));

        Id = id;
        ExperimentName = experimentName;
        VariantName = variantName;
        Configuration = configuration;
        Metrics = metrics;
        CompositeScore = compositeScore;
        IsWinner = isWinner;
        CreatedAt = createdAt ?? DateTime.UtcNow;
        CompletedAt = completedAt ?? DateTime.UtcNow;
        StatisticalSignificance = statisticalSignificance;
        InitiatedBy = initiatedBy;
        TenantId = tenantId;
    }
}

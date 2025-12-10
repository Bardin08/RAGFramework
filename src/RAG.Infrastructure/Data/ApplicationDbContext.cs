using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RAG.Core.Domain;
using System.Text.Json;
using EvaluationDomain = RAG.Core.Domain.Evaluation;

namespace RAG.Infrastructure.Data;

/// <summary>
/// Database context for the RAG application.
/// </summary>
public class ApplicationDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationDbContext"/> class.
    /// </summary>
    /// <param name="options">The options for this context.</param>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the Documents DbSet.
    /// </summary>
    public DbSet<Document> Documents { get; set; } = null!;

    /// <summary>
    /// Gets or sets the DocumentChunks DbSet.
    /// </summary>
    public DbSet<DocumentChunk> DocumentChunks { get; set; } = null!;

    /// <summary>
    /// Gets or sets the DocumentHashes DbSet.
    /// </summary>
    public DbSet<DocumentHash> DocumentHashes { get; set; } = null!;

    /// <summary>
    /// Gets or sets the AuditLogs DbSet.
    /// </summary>
    public DbSet<AuditLogEntry> AuditLogs { get; set; } = null!;

    /// <summary>
    /// Gets or sets the DocumentAccess DbSet for ACL.
    /// </summary>
    public DbSet<DocumentAccess> DocumentAccess { get; set; } = null!;

    /// <summary>
    /// Gets or sets the AccessAuditLogs DbSet for ACL audit trail.
    /// </summary>
    public DbSet<AccessAuditLog> AccessAuditLogs { get; set; } = null!;

    /// <summary>
    /// Gets or sets the IndexRebuildJobs DbSet for tracking rebuild jobs.
    /// </summary>
    public DbSet<IndexRebuildJob> IndexRebuildJobs { get; set; } = null!;

    /// <summary>
    /// Gets or sets the EvaluationRuns DbSet for tracking evaluation runs.
    /// </summary>
    public DbSet<EvaluationRun> EvaluationRuns { get; set; } = null!;

    /// <summary>
    /// Gets or sets the EvaluationMetricRecords DbSet for storing metric values.
    /// </summary>
    public DbSet<EvaluationMetricRecord> EvaluationMetricRecords { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Evaluations DbSet for evaluation configurations.
    /// </summary>
    public DbSet<EvaluationDomain> Evaluations { get; set; } = null!;

    /// <summary>
    /// Gets or sets the SeedDatasets DbSet for tracking loaded seed datasets.
    /// </summary>
    public DbSet<SeedDataset> SeedDatasets { get; set; } = null!;

    /// <summary>
    /// Gets or sets the BenchmarkJobs DbSet for tracking benchmark runs.
    /// </summary>
    public DbSet<BenchmarkJob> BenchmarkJobs { get; set; } = null!;

    /// <summary>
    /// Gets or sets the ConfigurationExperimentResults DbSet for A/B testing experiments.
    /// </summary>
    public DbSet<ConfigurationExperimentResult> ConfigurationExperimentResults { get; set; } = null!;

    /// <summary>
    /// Configures the model for the database.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("documents");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(500)
                .HasColumnName("title");

            entity.Property(e => e.Content)
                .IsRequired()
                .HasColumnName("content");

            entity.Property(e => e.Source)
                .HasMaxLength(1000)
                .HasColumnName("source");

            entity.Property(e => e.TenantId)
                .IsRequired()
                .HasColumnName("tenant_id");

            // Configure Metadata with JSON value converter for compatibility with both PostgreSQL and InMemory
            var metadataConverter = new ValueConverter<Dictionary<string, object>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>());

            entity.Property(e => e.Metadata)
                .HasConversion(metadataConverter)
                .HasColumnType("jsonb")
                .HasColumnName("metadata");

            // Configure ChunkIds with JSON value converter for compatibility with both PostgreSQL and InMemory
            var chunkIdsConverter = new ValueConverter<List<Guid>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>());

            entity.Property(e => e.ChunkIds)
                .HasConversion(chunkIdsConverter)
                .HasColumnType("jsonb")
                .HasColumnName("chunk_ids");

            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .IsRequired()
                .HasColumnName("updated_at");

            entity.Property(e => e.OwnerId)
                .IsRequired()
                .HasColumnName("owner_id");

            entity.Property(e => e.IsPublic)
                .IsRequired()
                .HasDefaultValue(false)
                .HasColumnName("is_public");

            // Indexes
            entity.HasIndex(e => e.TenantId)
                .HasDatabaseName("idx_documents_tenant_id");

            entity.HasIndex(e => e.Title)
                .HasDatabaseName("idx_documents_title");

            entity.HasIndex(e => e.OwnerId)
                .HasDatabaseName("idx_documents_owner_id");
        });

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.ToTable("document_chunks");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.DocumentId)
                .IsRequired()
                .HasColumnName("document_id");

            entity.Property(e => e.Text)
                .IsRequired()
                .HasColumnName("text");

            entity.Property(e => e.StartIndex)
                .IsRequired()
                .HasColumnName("start_index");

            entity.Property(e => e.EndIndex)
                .IsRequired()
                .HasColumnName("end_index");

            entity.Property(e => e.ChunkIndex)
                .IsRequired()
                .HasColumnName("chunk_index");

            entity.Property(e => e.TenantId)
                .IsRequired()
                .HasColumnName("tenant_id");

            // Configure Metadata with JSON value converter for compatibility with both PostgreSQL and InMemory
            var chunkMetadataConverter = new ValueConverter<Dictionary<string, object>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>());

            entity.Property(e => e.Metadata)
                .HasConversion(chunkMetadataConverter)
                .HasColumnType("jsonb")
                .HasColumnName("metadata");

            // Indexes
            entity.HasIndex(e => e.DocumentId)
                .HasDatabaseName("idx_chunks_document_id");

            entity.HasIndex(e => e.TenantId)
                .HasDatabaseName("idx_chunks_tenant_id");

            entity.HasIndex(e => new { e.DocumentId, e.ChunkIndex })
                .HasDatabaseName("idx_chunks_document_chunk");
        });

        modelBuilder.Entity<DocumentHash>(entity =>
        {
            entity.ToTable("document_hashes");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Hash)
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnName("hash");

            entity.Property(e => e.DocumentId)
                .IsRequired()
                .HasColumnName("document_id");

            entity.Property(e => e.OriginalFileName)
                .IsRequired()
                .HasMaxLength(500)
                .HasColumnName("original_file_name");

            entity.Property(e => e.UploadedAt)
                .IsRequired()
                .HasColumnName("uploaded_at");

            entity.Property(e => e.UploadedBy)
                .IsRequired()
                .HasColumnName("uploaded_by");

            entity.Property(e => e.TenantId)
                .IsRequired()
                .HasColumnName("tenant_id");

            // Indexes
            entity.HasIndex(e => e.DocumentId)
                .HasDatabaseName("idx_document_id");

            entity.HasIndex(e => e.TenantId)
                .HasDatabaseName("idx_tenant_id");

            entity.HasIndex(e => new { e.Hash, e.TenantId })
                .IsUnique()
                .HasDatabaseName("idx_hash_tenant");
        });

        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.ToTable("audit_logs");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Timestamp)
                .IsRequired()
                .HasColumnName("timestamp");

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("user_id");

            entity.Property(e => e.UserName)
                .HasMaxLength(255)
                .HasColumnName("user_name");

            entity.Property(e => e.Action)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("action");

            entity.Property(e => e.Resource)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("resource");

            entity.Property(e => e.Details)
                .HasColumnType("jsonb")
                .HasColumnName("details");

            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .HasColumnName("ip_address");

            entity.Property(e => e.StatusCode)
                .HasColumnName("status_code");

            entity.Property(e => e.DurationMs)
                .HasColumnName("duration_ms");

            // Indexes for common queries
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("idx_audit_logs_user");

            entity.HasIndex(e => e.Action)
                .HasDatabaseName("idx_audit_logs_action");

            entity.HasIndex(e => e.Timestamp)
                .HasDatabaseName("idx_audit_logs_timestamp");
        });

        modelBuilder.Entity<DocumentAccess>(entity =>
        {
            entity.ToTable("document_access");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.DocumentId)
                .IsRequired()
                .HasColumnName("document_id");

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasColumnName("user_id");

            entity.Property(e => e.Permission)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasColumnName("permission_type");

            entity.Property(e => e.GrantedBy)
                .IsRequired()
                .HasColumnName("granted_by");

            entity.Property(e => e.GrantedAt)
                .IsRequired()
                .HasColumnName("granted_at");

            // Foreign key to documents with cascade delete
            entity.HasOne<Document>()
                .WithMany()
                .HasForeignKey(e => e.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint: one access entry per document-user pair
            entity.HasIndex(e => new { e.DocumentId, e.UserId })
                .IsUnique()
                .HasDatabaseName("idx_document_access_unique");

            entity.HasIndex(e => e.DocumentId)
                .HasDatabaseName("idx_document_access_document");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("idx_document_access_user");

            entity.HasIndex(e => e.Permission)
                .HasDatabaseName("idx_document_access_permission");
        });

        modelBuilder.Entity<AccessAuditLog>(entity =>
        {
            entity.ToTable("access_audit_log");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Timestamp)
                .IsRequired()
                .HasColumnName("timestamp");

            entity.Property(e => e.ActorUserId)
                .IsRequired()
                .HasColumnName("actor_user_id");

            entity.Property(e => e.Action)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("action");

            entity.Property(e => e.DocumentId)
                .IsRequired()
                .HasColumnName("document_id");

            entity.Property(e => e.TargetUserId)
                .HasColumnName("target_user_id");

            entity.Property(e => e.PermissionType)
                .HasMaxLength(20)
                .HasColumnName("permission_type");

            entity.Property(e => e.Details)
                .HasColumnType("jsonb")
                .HasColumnName("details");

            entity.HasIndex(e => e.DocumentId)
                .HasDatabaseName("idx_access_audit_document");

            entity.HasIndex(e => e.Timestamp)
                .HasDatabaseName("idx_access_audit_timestamp");

            entity.HasIndex(e => e.ActorUserId)
                .HasDatabaseName("idx_access_audit_actor");
        });

        modelBuilder.Entity<IndexRebuildJob>(entity =>
        {
            entity.ToTable("index_rebuild_jobs");

            entity.HasKey(e => e.JobId);

            entity.Property(e => e.JobId)
                .HasColumnName("id");

            entity.Property(e => e.TenantId)
                .HasColumnName("tenant_id");

            entity.Property(e => e.IncludeEmbeddings)
                .IsRequired()
                .HasColumnName("include_embeddings");

            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("status");

            entity.Property(e => e.EstimatedDocuments)
                .IsRequired()
                .HasColumnName("estimated_documents");

            entity.Property(e => e.ProcessedDocuments)
                .IsRequired()
                .HasColumnName("processed_documents");

            entity.Property(e => e.StartedAt)
                .IsRequired()
                .HasColumnName("started_at");

            entity.Property(e => e.CompletedAt)
                .HasColumnName("completed_at");

            entity.Property(e => e.Error)
                .HasColumnName("error");

            entity.Property(e => e.InitiatedBy)
                .HasColumnName("initiated_by");

            // Ignore the CancellationTokenSource - it's not persisted
            entity.Ignore(e => e.CancellationTokenSource);

            // Indexes
            entity.HasIndex(e => e.Status)
                .HasDatabaseName("idx_rebuild_jobs_status");

            entity.HasIndex(e => e.TenantId)
                .HasDatabaseName("idx_rebuild_jobs_tenant");

            entity.HasIndex(e => e.StartedAt)
                .HasDatabaseName("idx_rebuild_jobs_started_at");
        });

        modelBuilder.Entity<EvaluationRun>(entity =>
        {
            entity.ToTable("evaluation_runs");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.EvaluationId)
                .HasColumnName("evaluation_id");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("name");

            entity.Property(e => e.Description)
                .HasColumnName("description");

            entity.Property(e => e.StartedAt)
                .IsRequired()
                .HasColumnName("started_at");

            entity.Property(e => e.FinishedAt)
                .HasColumnName("finished_at");

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasColumnName("status");

            entity.Property(e => e.Progress)
                .IsRequired()
                .HasColumnName("progress");

            entity.Property(e => e.Configuration)
                .IsRequired()
                .HasColumnType("jsonb")
                .HasColumnName("configuration");

            entity.Property(e => e.ResultsSummary)
                .HasColumnType("jsonb")
                .HasColumnName("results_summary");

            entity.Property(e => e.ErrorMessage)
                .HasColumnName("error_message");

            entity.Property(e => e.TotalQueries)
                .IsRequired()
                .HasColumnName("total_queries");

            entity.Property(e => e.CompletedQueries)
                .IsRequired()
                .HasColumnName("completed_queries");

            entity.Property(e => e.FailedQueries)
                .IsRequired()
                .HasColumnName("failed_queries");

            entity.Property(e => e.InitiatedBy)
                .HasMaxLength(255)
                .HasColumnName("initiated_by");

            entity.Property(e => e.TenantId)
                .HasMaxLength(100)
                .HasColumnName("tenant_id");

            // Indexes
            entity.HasIndex(e => e.EvaluationId)
                .HasDatabaseName("idx_eval_runs_evaluation");

            entity.HasIndex(e => e.Status)
                .HasDatabaseName("idx_eval_runs_status");

            entity.HasIndex(e => e.StartedAt)
                .HasDatabaseName("idx_eval_runs_started_at");

            entity.HasIndex(e => e.TenantId)
                .HasDatabaseName("idx_eval_runs_tenant");
        });

        modelBuilder.Entity<EvaluationMetricRecord>(entity =>
        {
            entity.ToTable("evaluation_metrics");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.RunId)
                .IsRequired()
                .HasColumnName("run_id");

            entity.Property(e => e.MetricName)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("metric_name");

            entity.Property(e => e.MetricValue)
                .IsRequired()
                .HasColumnName("metric_value");

            entity.Property(e => e.Metadata)
                .HasColumnType("jsonb")
                .HasColumnName("metadata");

            entity.Property(e => e.RecordedAt)
                .IsRequired()
                .HasColumnName("recorded_at");

            entity.Property(e => e.SampleId)
                .HasMaxLength(255)
                .HasColumnName("sample_id");

            // Foreign key to evaluation runs
            entity.HasOne(e => e.Run)
                .WithMany()
                .HasForeignKey(e => e.RunId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(e => e.RunId)
                .HasDatabaseName("idx_eval_metrics_run");

            entity.HasIndex(e => e.MetricName)
                .HasDatabaseName("idx_eval_metrics_name");

            entity.HasIndex(e => e.RecordedAt)
                .HasDatabaseName("idx_eval_metrics_recorded");

            entity.HasIndex(e => new { e.RunId, e.MetricName })
                .HasDatabaseName("idx_eval_metrics_run_name");
        });

        modelBuilder.Entity<EvaluationDomain>(entity =>
        {
            entity.ToTable("evaluations");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("name");

            entity.Property(e => e.Description)
                .HasColumnName("description");

            entity.Property(e => e.Type)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("type");

            entity.Property(e => e.Config)
                .IsRequired()
                .HasColumnType("jsonb")
                .HasColumnName("config");

            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasColumnName("created_at");

            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasColumnName("created_by");

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true)
                .HasColumnName("is_active");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");

            entity.Property(e => e.UpdatedBy)
                .HasColumnName("updated_by");

            // Unique constraint on name
            entity.HasIndex(e => e.Name)
                .IsUnique()
                .HasDatabaseName("idx_evaluations_name_unique");

            entity.HasIndex(e => e.Type)
                .HasDatabaseName("idx_evaluations_type");

            entity.HasIndex(e => e.IsActive)
                .HasDatabaseName("idx_evaluations_active");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("idx_evaluations_created_at");
        });

        modelBuilder.Entity<SeedDataset>(entity =>
        {
            entity.ToTable("seed_datasets");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("name");

            entity.Property(e => e.Version)
                .HasMaxLength(50)
                .HasColumnName("version");

            entity.Property(e => e.Hash)
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnName("hash");

            entity.Property(e => e.LoadedAt)
                .IsRequired()
                .HasDefaultValueSql("NOW()")
                .HasColumnName("loaded_at");

            entity.Property(e => e.DocumentsCount)
                .IsRequired()
                .HasColumnName("documents_count");

            entity.Property(e => e.QueriesCount)
                .IsRequired()
                .HasColumnName("queries_count");

            entity.Property(e => e.LoadedBy)
                .IsRequired()
                .HasColumnName("loaded_by");

            // Configure Metadata with JSON value converter for compatibility with both PostgreSQL and InMemory
            var seedMetadataConverter = new ValueConverter<Dictionary<string, object>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>());

            entity.Property(e => e.Metadata)
                .HasConversion(seedMetadataConverter)
                .HasColumnType("jsonb")
                .HasColumnName("metadata");

            // Unique constraint on name - only one dataset with a given name can be loaded
            entity.HasIndex(e => e.Name)
                .IsUnique()
                .HasDatabaseName("idx_seed_datasets_name_unique");

            entity.HasIndex(e => e.Hash)
                .HasDatabaseName("idx_seed_datasets_hash");

            entity.HasIndex(e => e.LoadedAt)
                .HasDatabaseName("idx_seed_datasets_loaded_at");

            entity.HasIndex(e => e.LoadedBy)
                .HasDatabaseName("idx_seed_datasets_loaded_by");
        });

        modelBuilder.Entity<BenchmarkJob>(entity =>
        {
            entity.ToTable("benchmark_jobs");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("status");

            entity.Property(e => e.Dataset)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("dataset");

            entity.Property(e => e.Configuration)
                .IsRequired()
                .HasColumnType("jsonb")
                .HasColumnName("configuration");

            entity.Property(e => e.SampleSize)
                .HasColumnName("sample_size");

            entity.Property(e => e.Results)
                .HasColumnType("jsonb")
                .HasColumnName("results");

            entity.Property(e => e.Progress)
                .IsRequired()
                .HasColumnName("progress");

            entity.Property(e => e.TotalSamples)
                .HasColumnName("total_samples");

            entity.Property(e => e.ProcessedSamples)
                .HasColumnName("processed_samples");

            entity.Property(e => e.ErrorMessage)
                .HasColumnName("error_message");

            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasColumnName("created_at");

            entity.Property(e => e.StartedAt)
                .HasColumnName("started_at");

            entity.Property(e => e.CompletedAt)
                .HasColumnName("completed_at");

            entity.Property(e => e.InitiatedBy)
                .HasMaxLength(255)
                .HasColumnName("initiated_by");

            // Indexes
            entity.HasIndex(e => e.Status)
                .HasDatabaseName("idx_benchmark_jobs_status");

            entity.HasIndex(e => e.Dataset)
                .HasDatabaseName("idx_benchmark_jobs_dataset");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("idx_benchmark_jobs_created_at");

            entity.HasIndex(e => e.InitiatedBy)
                .HasDatabaseName("idx_benchmark_jobs_initiated_by");
        });

        modelBuilder.Entity<ConfigurationExperimentResult>(entity =>
        {
            entity.ToTable("configuration_experiment_results");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.ExperimentName)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("experiment_name");

            entity.Property(e => e.VariantName)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("variant_name");

            entity.Property(e => e.Configuration)
                .IsRequired()
                .HasColumnType("jsonb")
                .HasColumnName("configuration");

            entity.Property(e => e.Metrics)
                .IsRequired()
                .HasColumnType("jsonb")
                .HasColumnName("metrics");

            entity.Property(e => e.CompositeScore)
                .IsRequired()
                .HasColumnName("composite_score");

            entity.Property(e => e.IsWinner)
                .IsRequired()
                .HasDefaultValue(false)
                .HasColumnName("is_winner");

            entity.Property(e => e.StatisticalSignificance)
                .HasColumnType("jsonb")
                .HasColumnName("statistical_significance");

            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasColumnName("created_at");

            entity.Property(e => e.CompletedAt)
                .IsRequired()
                .HasColumnName("completed_at");

            entity.Property(e => e.InitiatedBy)
                .HasColumnName("initiated_by");

            entity.Property(e => e.TenantId)
                .HasColumnName("tenant_id");

            // Indexes
            entity.HasIndex(e => e.ExperimentName)
                .HasDatabaseName("idx_config_exp_name");

            entity.HasIndex(e => new { e.ExperimentName, e.VariantName })
                .HasDatabaseName("idx_config_exp_name_variant");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("idx_config_exp_created_at");

            entity.HasIndex(e => e.TenantId)
                .HasDatabaseName("idx_config_exp_tenant");

            entity.HasIndex(e => e.IsWinner)
                .HasDatabaseName("idx_config_exp_winner");
        });
    }
}

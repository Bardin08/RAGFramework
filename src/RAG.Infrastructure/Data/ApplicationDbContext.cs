using Microsoft.EntityFrameworkCore;
using RAG.Core.Domain;

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

            entity.Property(e => e.Metadata)
                .HasColumnType("jsonb")
                .HasColumnName("metadata");

            entity.Property(e => e.ChunkIds)
                .HasColumnType("jsonb")
                .HasColumnName("chunk_ids");

            // Indexes
            entity.HasIndex(e => e.TenantId)
                .HasDatabaseName("idx_documents_tenant_id");

            entity.HasIndex(e => e.Title)
                .HasDatabaseName("idx_documents_title");
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

            entity.Property(e => e.Metadata)
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
    }
}

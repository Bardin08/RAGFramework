using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RAG.Core.Domain;

namespace RAG.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for DocumentHash entity.
/// </summary>
public class DocumentHashConfiguration : IEntityTypeConfiguration<DocumentHash>
{
    public void Configure(EntityTypeBuilder<DocumentHash> builder)
    {
        // Table name
        builder.ToTable("document_hashes");

        // Primary key
        builder.HasKey(dh => dh.Id);

        // Property configurations
        builder.Property(dh => dh.Hash)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnName("hash");

        builder.Property(dh => dh.DocumentId)
            .IsRequired()
            .HasColumnName("document_id");

        builder.Property(dh => dh.OriginalFileName)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("original_file_name");

        builder.Property(dh => dh.UploadedAt)
            .IsRequired()
            .HasColumnName("uploaded_at");

        builder.Property(dh => dh.UploadedBy)
            .IsRequired()
            .HasColumnName("uploaded_by");

        builder.Property(dh => dh.TenantId)
            .IsRequired()
            .HasColumnName("tenant_id");

        // Unique constraint on (hash, tenant_id) for deduplication within tenant
        builder.HasIndex(dh => new { dh.Hash, dh.TenantId })
            .IsUnique()
            .HasDatabaseName("idx_hash_tenant");

        // Additional indexes for query performance
        builder.HasIndex(dh => dh.DocumentId)
            .HasDatabaseName("idx_document_id");

        builder.HasIndex(dh => dh.TenantId)
            .HasDatabaseName("idx_tenant_id");
    }
}

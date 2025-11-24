using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAG.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentAndChunkTablesWithTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    start_index = table.Column<int>(type: "integer", nullable: false),
                    end_index = table.Column<int>(type: "integer", nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    metadata = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_chunks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    metadata = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: false),
                    chunk_ids = table.Column<List<Guid>>(type: "jsonb", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_chunks_document_chunk",
                table: "document_chunks",
                columns: new[] { "document_id", "chunk_index" });

            migrationBuilder.CreateIndex(
                name: "idx_chunks_document_id",
                table: "document_chunks",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "idx_chunks_tenant_id",
                table: "document_chunks",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_documents_tenant_id",
                table: "documents",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_documents_title",
                table: "documents",
                column: "title");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_chunks");

            migrationBuilder.DropTable(
                name: "documents");
        }
    }
}

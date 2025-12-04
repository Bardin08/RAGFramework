using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAG.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexRebuildJobsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "index_rebuild_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    include_embeddings = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    estimated_documents = table.Column<int>(type: "integer", nullable: false),
                    processed_documents = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    initiated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_index_rebuild_jobs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_rebuild_jobs_started_at",
                table: "index_rebuild_jobs",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "idx_rebuild_jobs_status",
                table: "index_rebuild_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_rebuild_jobs_tenant",
                table: "index_rebuild_jobs",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "index_rebuild_jobs");
        }
    }
}

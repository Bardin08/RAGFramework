using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAG.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBenchmarkJobsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "benchmark_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    dataset = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    configuration = table.Column<string>(type: "jsonb", nullable: false),
                    sample_size = table.Column<int>(type: "integer", nullable: true),
                    results = table.Column<string>(type: "jsonb", nullable: true),
                    progress = table.Column<int>(type: "integer", nullable: false),
                    total_samples = table.Column<int>(type: "integer", nullable: true),
                    processed_samples = table.Column<int>(type: "integer", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    initiated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_benchmark_jobs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_benchmark_jobs_created_at",
                table: "benchmark_jobs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_benchmark_jobs_dataset",
                table: "benchmark_jobs",
                column: "dataset");

            migrationBuilder.CreateIndex(
                name: "idx_benchmark_jobs_initiated_by",
                table: "benchmark_jobs",
                column: "initiated_by");

            migrationBuilder.CreateIndex(
                name: "idx_benchmark_jobs_status",
                table: "benchmark_jobs",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "benchmark_jobs");
        }
    }
}

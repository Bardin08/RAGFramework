using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAG.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evaluation_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    progress = table.Column<int>(type: "integer", nullable: false),
                    configuration = table.Column<string>(type: "jsonb", nullable: false),
                    results_summary = table.Column<string>(type: "jsonb", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    total_queries = table.Column<int>(type: "integer", nullable: false),
                    completed_queries = table.Column<int>(type: "integer", nullable: false),
                    failed_queries = table.Column<int>(type: "integer", nullable: false),
                    initiated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "evaluations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    config = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_metrics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    metric_value = table.Column<decimal>(type: "numeric", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sample_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_metrics", x => x.id);
                    table.ForeignKey(
                        name: "FK_evaluation_metrics_evaluation_runs_run_id",
                        column: x => x.run_id,
                        principalTable: "evaluation_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_eval_metrics_name",
                table: "evaluation_metrics",
                column: "metric_name");

            migrationBuilder.CreateIndex(
                name: "idx_eval_metrics_recorded",
                table: "evaluation_metrics",
                column: "recorded_at");

            migrationBuilder.CreateIndex(
                name: "idx_eval_metrics_run",
                table: "evaluation_metrics",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "idx_eval_metrics_run_name",
                table: "evaluation_metrics",
                columns: new[] { "run_id", "metric_name" });

            migrationBuilder.CreateIndex(
                name: "idx_eval_runs_evaluation",
                table: "evaluation_runs",
                column: "evaluation_id");

            migrationBuilder.CreateIndex(
                name: "idx_eval_runs_started_at",
                table: "evaluation_runs",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "idx_eval_runs_status",
                table: "evaluation_runs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_eval_runs_tenant",
                table: "evaluation_runs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_evaluations_active",
                table: "evaluations",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_evaluations_created_at",
                table: "evaluations",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_evaluations_name_unique",
                table: "evaluations",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_evaluations_type",
                table: "evaluations",
                column: "type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evaluation_metrics");

            migrationBuilder.DropTable(
                name: "evaluations");

            migrationBuilder.DropTable(
                name: "evaluation_runs");
        }
    }
}

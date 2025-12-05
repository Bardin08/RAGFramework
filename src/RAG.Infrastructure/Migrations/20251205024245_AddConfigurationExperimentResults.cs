using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAG.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigurationExperimentResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "configuration_experiment_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    experiment_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    variant_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    configuration = table.Column<string>(type: "jsonb", nullable: false),
                    metrics = table.Column<string>(type: "jsonb", nullable: false),
                    composite_score = table.Column<double>(type: "double precision", nullable: false),
                    is_winner = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    statistical_significance = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    initiated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuration_experiment_results", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_config_exp_created_at",
                table: "configuration_experiment_results",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_config_exp_name",
                table: "configuration_experiment_results",
                column: "experiment_name");

            migrationBuilder.CreateIndex(
                name: "idx_config_exp_name_variant",
                table: "configuration_experiment_results",
                columns: new[] { "experiment_name", "variant_name" });

            migrationBuilder.CreateIndex(
                name: "idx_config_exp_tenant",
                table: "configuration_experiment_results",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_config_exp_winner",
                table: "configuration_experiment_results",
                column: "is_winner");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuration_experiment_results");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAG.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSeedDatasetsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "seed_datasets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    loaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    documents_count = table.Column<int>(type: "integer", nullable: false),
                    queries_count = table.Column<int>(type: "integer", nullable: false),
                    loaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seed_datasets", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_seed_datasets_hash",
                table: "seed_datasets",
                column: "hash");

            migrationBuilder.CreateIndex(
                name: "idx_seed_datasets_loaded_at",
                table: "seed_datasets",
                column: "loaded_at");

            migrationBuilder.CreateIndex(
                name: "idx_seed_datasets_loaded_by",
                table: "seed_datasets",
                column: "loaded_by");

            migrationBuilder.CreateIndex(
                name: "idx_seed_datasets_name_unique",
                table: "seed_datasets",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "seed_datasets");
        }
    }
}

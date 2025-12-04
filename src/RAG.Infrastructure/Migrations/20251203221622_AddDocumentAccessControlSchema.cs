using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAG.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentAccessControlSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_public",
                table: "documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "documents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "access_audit_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    permission_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    details = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_audit_log", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "document_access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_access", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_access_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_documents_owner_id",
                table: "documents",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "idx_access_audit_actor",
                table: "access_audit_log",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_access_audit_document",
                table: "access_audit_log",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "idx_access_audit_timestamp",
                table: "access_audit_log",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "idx_document_access_document",
                table: "document_access",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "idx_document_access_permission",
                table: "document_access",
                column: "permission_type");

            migrationBuilder.CreateIndex(
                name: "idx_document_access_unique",
                table: "document_access",
                columns: new[] { "document_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_document_access_user",
                table: "document_access",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_audit_log");

            migrationBuilder.DropTable(
                name: "document_access");

            migrationBuilder.DropIndex(
                name: "idx_documents_owner_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "is_public",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "documents");
        }
    }
}

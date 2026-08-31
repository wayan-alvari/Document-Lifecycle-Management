using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentLifecycle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    public_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    actor = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    action = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    entity_type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    entity_public_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    occurred_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    details_json = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_0900_ai_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_audit_events", x => x.id);
                    table.ForeignKey(
                        name: "f_k_audit_events_demo_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "demo_workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "document_categories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    public_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_document_categories", x => x.id);
                    table.ForeignKey(
                        name: "f_k_document_categories_demo_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "demo_workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "document_owners",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    public_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    display_name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    contact = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_document_owners", x => x.id);
                    table.ForeignKey(
                        name: "f_k_document_owners_demo_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "demo_workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    public_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    recipient_role = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, collation: "utf8mb4_0900_ai_ci"),
                    recipient_user_id = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true, collation: "utf8mb4_0900_ai_ci"),
                    message = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    link = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    deduplication_key = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    read_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_notifications", x => x.id);
                    table.ForeignKey(
                        name: "f_k_notifications_demo_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "demo_workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "managed_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    public_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    code = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    category_id = table.Column<long>(type: "bigint", nullable: false),
                    owner_id = table.Column<long>(type: "bigint", nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    state = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    archive_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, collation: "utf8mb4_0900_ai_ci"),
                    archived_by = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true, collation: "utf8mb4_0900_ai_ci"),
                    archived_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_by = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_managed_documents", x => x.id);
                    table.ForeignKey(
                        name: "f_k_managed_documents_demo_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "demo_workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_managed_documents_document_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "document_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "f_k_managed_documents_document_owners_owner_id",
                        column: x => x.owner_id,
                        principalTable: "document_owners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "document_revisions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    public_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    workspace_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    managed_document_id = table.Column<long>(type: "bigint", nullable: false),
                    revision_number = table.Column<int>(type: "int", nullable: false),
                    change_note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    original_filename = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    stored_filename = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    media_type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    sha256_hash = table.Column<string>(type: "char(64)", nullable: false, collation: "ascii_general_ci"),
                    uploaded_by = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false, collation: "utf8mb4_0900_ai_ci"),
                    uploaded_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_document_revisions", x => x.id);
                    table.ForeignKey(
                        name: "f_k_document_revisions_managed_documents_managed_document_id",
                        column: x => x.managed_document_id,
                        principalTable: "managed_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "i_x_audit_events_workspace_id_entity_type_entity_public_id",
                table: "audit_events",
                columns: new[] { "workspace_id", "entity_type", "entity_public_id" });

            migrationBuilder.CreateIndex(
                name: "i_x_audit_events_workspace_id_occurred_at_utc",
                table: "audit_events",
                columns: new[] { "workspace_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "i_x_audit_events_workspace_id_public_id",
                table: "audit_events",
                columns: new[] { "workspace_id", "public_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_document_categories_workspace_id_name",
                table: "document_categories",
                columns: new[] { "workspace_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_document_categories_workspace_id_public_id",
                table: "document_categories",
                columns: new[] { "workspace_id", "public_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_document_owners_workspace_id_display_name",
                table: "document_owners",
                columns: new[] { "workspace_id", "display_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_document_owners_workspace_id_public_id",
                table: "document_owners",
                columns: new[] { "workspace_id", "public_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_document_revisions_managed_document_id_revision_number",
                table: "document_revisions",
                columns: new[] { "managed_document_id", "revision_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_document_revisions_workspace_id_public_id",
                table: "document_revisions",
                columns: new[] { "workspace_id", "public_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_document_revisions_workspace_id_stored_filename",
                table: "document_revisions",
                columns: new[] { "workspace_id", "stored_filename" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_managed_documents_category_id",
                table: "managed_documents",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "i_x_managed_documents_owner_id",
                table: "managed_documents",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "i_x_managed_documents_workspace_id_category_id",
                table: "managed_documents",
                columns: new[] { "workspace_id", "category_id" });

            migrationBuilder.CreateIndex(
                name: "i_x_managed_documents_workspace_id_code",
                table: "managed_documents",
                columns: new[] { "workspace_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_managed_documents_workspace_id_expiry_date",
                table: "managed_documents",
                columns: new[] { "workspace_id", "expiry_date" });

            migrationBuilder.CreateIndex(
                name: "i_x_managed_documents_workspace_id_owner_id",
                table: "managed_documents",
                columns: new[] { "workspace_id", "owner_id" });

            migrationBuilder.CreateIndex(
                name: "i_x_managed_documents_workspace_id_public_id",
                table: "managed_documents",
                columns: new[] { "workspace_id", "public_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_managed_documents_workspace_id_state",
                table: "managed_documents",
                columns: new[] { "workspace_id", "state" });

            migrationBuilder.CreateIndex(
                name: "i_x_notifications_workspace_id_deduplication_key",
                table: "notifications",
                columns: new[] { "workspace_id", "deduplication_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_notifications_workspace_id_public_id",
                table: "notifications",
                columns: new[] { "workspace_id", "public_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_notifications_workspace_id_recipient_role_read_at_utc",
                table: "notifications",
                columns: new[] { "workspace_id", "recipient_role", "read_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "document_revisions");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "managed_documents");

            migrationBuilder.DropTable(
                name: "document_categories");

            migrationBuilder.DropTable(
                name: "document_owners");
        }
    }
}

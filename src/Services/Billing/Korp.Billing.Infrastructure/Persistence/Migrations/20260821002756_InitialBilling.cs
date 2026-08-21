using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korp.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "invoice_number_seq");

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    causation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload_hash = table.Column<string>(type: "char(64)", nullable: false),
                    processed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_messages", x => x.message_id);
                    table.CheckConstraint("ck_inbox_messages_schema_version", "schema_version > 0");
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('invoice_number_seq')"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_issuance_in_progress = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invoices", x => x.id);
                    table.CheckConstraint("ck_invoices_number_positive", "number > 0");
                    table.CheckConstraint("ck_invoices_status", "status IN ('open', 'closed')");
                    table.CheckConstraint("ck_invoices_status_timestamps", "(status = 'open' AND closed_at_utc IS NULL) OR (status = 'closed' AND closed_at_utc IS NOT NULL AND is_issuance_in_progress = false)");
                    table.CheckConstraint("ck_invoices_timestamps", "updated_at_utc >= created_at_utc AND (closed_at_utc IS NULL OR closed_at_utc >= created_at_utc)");
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    causation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lock_id = table.Column<Guid>(type: "uuid", nullable: true),
                    locked_until_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                    table.CheckConstraint("ck_outbox_messages_attempt_count", "attempt_count >= 0");
                    table.CheckConstraint("ck_outbox_messages_lease_consistency", "(lock_id IS NULL) = (locked_until_utc IS NULL)");
                    table.CheckConstraint("ck_outbox_messages_published_lease", "published_at_utc IS NULL OR (lock_id IS NULL AND locked_until_utc IS NULL)");
                    table.CheckConstraint("ck_outbox_messages_schema_version", "schema_version > 0");
                });

            migrationBuilder.CreateTable(
                name: "invoice_issuance_processes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    outcome_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    outcome_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invoice_issuance_processes", x => x.id);
                    table.CheckConstraint("ck_invoice_issuance_processes_outcome", "(status IN ('pending', 'awaiting_stock') AND finished_at_utc IS NULL AND outcome_code IS NULL AND outcome_description IS NULL) OR (status = 'completed' AND finished_at_utc IS NOT NULL AND outcome_code IS NULL AND outcome_description IS NULL) OR (status IN ('rejected', 'manual_intervention') AND finished_at_utc IS NOT NULL AND outcome_code IS NOT NULL)");
                    table.CheckConstraint("ck_invoice_issuance_processes_status", "status IN ('pending', 'awaiting_stock', 'completed', 'rejected', 'manual_intervention')");
                    table.CheckConstraint("ck_invoice_issuance_processes_timestamps", "updated_at_utc >= created_at_utc AND (finished_at_utc IS NULL OR finished_at_utc >= created_at_utc)");
                    table.ForeignKey(
                        name: "fk_invoice_issuance_processes_invoices",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    product_description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invoice_items", x => x.id);
                    table.CheckConstraint("ck_invoice_items_product_code", "product_code <> '' AND product_code = btrim(product_code) AND product_code = upper(product_code)");
                    table.CheckConstraint("ck_invoice_items_product_description", "product_description <> '' AND product_description = btrim(product_description)");
                    table.CheckConstraint("ck_invoice_items_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_invoice_items_invoices",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inbox_messages_processed_at_utc",
                table: "inbox_messages",
                column: "processed_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_invoice_issuance_processes_invoice_id_created_at_utc",
                table: "invoice_issuance_processes",
                columns: new[] { "invoice_id", "created_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_invoice_issuance_processes_status_updated_at_utc",
                table: "invoice_issuance_processes",
                columns: new[] { "status", "updated_at_utc" });

            migrationBuilder.CreateIndex(
                name: "uq_invoice_issuance_processes_active_invoice",
                table: "invoice_issuance_processes",
                column: "invoice_id",
                unique: true,
                filter: "status IN ('pending', 'awaiting_stock')");

            migrationBuilder.CreateIndex(
                name: "uq_invoice_issuance_processes_idempotency_key",
                table: "invoice_issuance_processes",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_invoice_items_invoice_id_product_id",
                table: "invoice_items",
                columns: new[] { "invoice_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invoices_created_at_utc_id",
                table: "invoices",
                columns: new[] { "created_at_utc", "id" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "uq_invoices_number",
                table: "invoices",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                table: "outbox_messages",
                columns: new[] { "next_attempt_at_utc", "occurred_at_utc" },
                filter: "published_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_published_at_utc",
                table: "outbox_messages",
                column: "published_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages");

            migrationBuilder.DropTable(
                name: "invoice_issuance_processes");

            migrationBuilder.DropTable(
                name: "invoice_items");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropSequence(
                name: "invoice_number_seq");
        }
    }
}

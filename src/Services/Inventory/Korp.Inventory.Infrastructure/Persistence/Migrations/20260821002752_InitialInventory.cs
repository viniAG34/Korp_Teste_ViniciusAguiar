using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Korp.Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    balance = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.id);
                    table.CheckConstraint("ck_products_balance_non_negative", "balance >= 0");
                    table.CheckConstraint("ck_products_code_format", "code <> '' AND code = btrim(code) AND code = upper(code) AND code ~ '^[A-Z0-9._-]+$'");
                    table.CheckConstraint("ck_products_description", "description <> '' AND description = btrim(description)");
                    table.CheckConstraint("ck_products_timestamps", "updated_at_utc >= created_at_utc");
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    balance_before = table.Column<int>(type: "integer", nullable: false),
                    balance_after = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_movements", x => x.id);
                    table.CheckConstraint("ck_stock_movements_balances", "balance_before >= 0 AND balance_after >= 0 AND balance_after = balance_before - quantity");
                    table.CheckConstraint("ck_stock_movements_quantity", "quantity > 0");
                    table.CheckConstraint("ck_stock_movements_type", "type = 'invoice_deduction'");
                    table.ForeignKey(
                        name: "fk_stock_movements_products",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inbox_messages_processed_at_utc",
                table: "inbox_messages",
                column: "processed_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                table: "outbox_messages",
                columns: new[] { "next_attempt_at_utc", "occurred_at_utc" },
                filter: "published_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_published_at_utc",
                table: "outbox_messages",
                column: "published_at_utc");

            migrationBuilder.CreateIndex(
                name: "uq_products_code",
                table: "products",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_product_id_created_at_utc",
                table: "stock_movements",
                columns: new[] { "product_id", "created_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "uq_stock_movements_event_id_product_id",
                table: "stock_movements",
                columns: new[] { "event_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_stock_movements_invoice_id_product_id",
                table: "stock_movements",
                columns: new[] { "invoice_id", "product_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropTable(
                name: "products");
        }
    }
}

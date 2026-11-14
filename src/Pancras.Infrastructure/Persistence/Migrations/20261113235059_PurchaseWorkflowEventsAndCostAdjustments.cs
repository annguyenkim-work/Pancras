using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseWorkflowEventsAndCostAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "exchange_rate_on",
                table: "purchase_order",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "exchange_rate_source",
                table: "purchase_order",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "costed_amount",
                table: "purchase_line",
                type: "numeric(18,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "received_quantity",
                table: "purchase_line",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "purchase_cost_adjustment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,0)", nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    applied_to_stock = table.Column<decimal>(type: "numeric(18,0)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_cost_adjustment", x => x.id);
                    table.ForeignKey(
                        name: "fk_purchase_cost_adjustment_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_event",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_event", x => x.id);
                    table.ForeignKey(
                        name: "fk_purchase_event_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_cost_adjustment_purchase_order_id",
                table: "purchase_cost_adjustment",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_event_purchase_order_id",
                table: "purchase_event",
                column: "purchase_order_id");

            // Đơn cũ đã nhập kho (status 2 = Received) được nhận đủ: ghi số đã nhận bằng số đặt để có thể điều chỉnh chi phí sau này.
            migrationBuilder.Sql(
                "UPDATE purchase_line SET received_quantity = quantity " +
                "WHERE purchase_order_id IN (SELECT id FROM purchase_order WHERE status = 2);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_cost_adjustment");

            migrationBuilder.DropTable(
                name: "purchase_event");

            migrationBuilder.DropColumn(
                name: "exchange_rate_on",
                table: "purchase_order");

            migrationBuilder.DropColumn(
                name: "exchange_rate_source",
                table: "purchase_order");

            migrationBuilder.DropColumn(
                name: "costed_amount",
                table: "purchase_line");

            migrationBuilder.DropColumn(
                name: "received_quantity",
                table: "purchase_line");
        }
    }
}

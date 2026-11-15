using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "purchase_order",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receive_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    container_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    bill_of_lading = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    port = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expected_on = table.Column<DateOnly>(type: "date", nullable: true),
                    exchange_rate = table.Column<decimal>(type: "numeric(18,0)", nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_order", x => x.id);
                    table.ForeignKey(
                        name: "fk_purchase_order_stock_locations_receive_location_id",
                        column: x => x.receive_location_id,
                        principalTable: "stock_location",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_purchase_order_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_charge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,0)", nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    charge_group = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_charge", x => x.id);
                    table.ForeignKey(
                        name: "fk_purchase_charge_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    fob_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    duty_rate = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    supplemental_duty = table.Column<decimal>(type: "numeric(18,0)", nullable: false),
                    special_consumption_tax = table.Column<decimal>(type: "numeric(18,0)", nullable: false),
                    environment_tax = table.Column<decimal>(type: "numeric(18,0)", nullable: false),
                    declared_customs_value = table.Column<decimal>(type: "numeric(18,0)", nullable: true),
                    declared_duty = table.Column<decimal>(type: "numeric(18,0)", nullable: true),
                    declared_vat = table.Column<decimal>(type: "numeric(18,0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_line", x => x.id);
                    table.ForeignKey(
                        name: "fk_purchase_line_catalog_item_catalog_item_id",
                        column: x => x.catalog_item_id,
                        principalTable: "catalog_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_purchase_line_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_charge_share",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_charge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_charge_share", x => x.id);
                    table.ForeignKey(
                        name: "fk_purchase_charge_share_purchase_charge_purchase_charge_id",
                        column: x => x.purchase_charge_id,
                        principalTable: "purchase_charge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_charge_purchase_order_id",
                table: "purchase_charge",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_charge_share_purchase_charge_id",
                table: "purchase_charge_share",
                column: "purchase_charge_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_line_catalog_item_id",
                table: "purchase_line",
                column: "catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_line_purchase_order_id",
                table: "purchase_line",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_normalized_code",
                table: "purchase_order",
                column: "normalized_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_receive_location_id",
                table: "purchase_order",
                column: "receive_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_supplier_id",
                table: "purchase_order",
                column: "supplier_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_charge_share");

            migrationBuilder.DropTable(
                name: "purchase_line");

            migrationBuilder.DropTable(
                name: "purchase_charge");

            migrationBuilder.DropTable(
                name: "purchase_order");
        }
    }
}

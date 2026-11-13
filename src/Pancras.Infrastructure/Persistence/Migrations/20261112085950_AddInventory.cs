using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stock_location",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_location", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "disposal_slip",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    document_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    image_ref = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_disposal_slip", x => x.id);
                    table.ForeignKey(
                        name: "fk_disposal_slip_catalog_item_catalog_item_id",
                        column: x => x.catalog_item_id,
                        principalTable: "catalog_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disposal_slip_stock_locations_stock_location_id",
                        column: x => x.stock_location_id,
                        principalTable: "stock_location",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quantity_adjustment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delta = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    document_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    image_ref = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quantity_adjustment", x => x.id);
                    table.ForeignKey(
                        name: "fk_quantity_adjustment_catalog_item_catalog_item_id",
                        column: x => x.catalog_item_id,
                        principalTable: "catalog_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quantity_adjustment_stock_locations_stock_location_id",
                        column: x => x.stock_location_id,
                        principalTable: "stock_location",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_balance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    on_hand = table.Column<int>(type: "integer", nullable: false),
                    reserved = table.Column<int>(type: "integer", nullable: false),
                    in_transit = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_balance", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_balance_catalog_item_catalog_item_id",
                        column: x => x.catalog_item_id,
                        principalTable: "catalog_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_balance_stock_locations_stock_location_id",
                        column: x => x.stock_location_id,
                        principalTable: "stock_location",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_movement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_movement", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_movement_catalog_item_catalog_item_id",
                        column: x => x.catalog_item_id,
                        principalTable: "catalog_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movement_stock_location_stock_location_id",
                        column: x => x.stock_location_id,
                        principalTable: "stock_location",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_notice",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_notice", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_notice_stock_location_stock_location_id",
                        column: x => x.stock_location_id,
                        principalTable: "stock_location",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stocktake_sheet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_quantity = table.Column<int>(type: "integer", nullable: false),
                    counted_quantity = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    document_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    image_ref = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stocktake_sheet", x => x.id);
                    table.ForeignKey(
                        name: "fk_stocktake_sheet_catalog_item_catalog_item_id",
                        column: x => x.catalog_item_id,
                        principalTable: "catalog_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stocktake_sheet_stock_location_stock_location_id",
                        column: x => x.stock_location_id,
                        principalTable: "stock_location",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transfer_slip",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transfer_slip", x => x.id);
                    table.ForeignKey(
                        name: "fk_transfer_slip_stock_location_destination_location_id",
                        column: x => x.destination_location_id,
                        principalTable: "stock_location",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transfer_slip_stock_location_source_location_id",
                        column: x => x.source_location_id,
                        principalTable: "stock_location",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transfer_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_slip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transfer_line", x => x.id);
                    table.ForeignKey(
                        name: "fk_transfer_line_catalog_item_catalog_item_id",
                        column: x => x.catalog_item_id,
                        principalTable: "catalog_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transfer_line_transfer_slips_transfer_slip_id",
                        column: x => x.transfer_slip_id,
                        principalTable: "transfer_slip",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_disposal_slip_catalog_item_id",
                table: "disposal_slip",
                column: "catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_disposal_slip_stock_location_id",
                table: "disposal_slip",
                column: "stock_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_quantity_adjustment_catalog_item_id",
                table: "quantity_adjustment",
                column: "catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_quantity_adjustment_stock_location_id",
                table: "quantity_adjustment",
                column: "stock_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_balance_catalog_item_id_stock_location_id",
                table: "stock_balance",
                columns: new[] { "catalog_item_id", "stock_location_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_balance_stock_location_id",
                table: "stock_balance",
                column: "stock_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movement_catalog_item_id",
                table: "stock_movement",
                column: "catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movement_stock_location_id",
                table: "stock_movement",
                column: "stock_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_notice_stock_location_id",
                table: "stock_notice",
                column: "stock_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stocktake_sheet_catalog_item_id",
                table: "stocktake_sheet",
                column: "catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_stocktake_sheet_stock_location_id",
                table: "stocktake_sheet",
                column: "stock_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_transfer_line_catalog_item_id",
                table: "transfer_line",
                column: "catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_transfer_line_transfer_slip_id",
                table: "transfer_line",
                column: "transfer_slip_id");

            migrationBuilder.CreateIndex(
                name: "ix_transfer_slip_destination_location_id",
                table: "transfer_slip",
                column: "destination_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_transfer_slip_source_location_id",
                table: "transfer_slip",
                column: "source_location_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "disposal_slip");

            migrationBuilder.DropTable(
                name: "quantity_adjustment");

            migrationBuilder.DropTable(
                name: "stock_balance");

            migrationBuilder.DropTable(
                name: "stock_movement");

            migrationBuilder.DropTable(
                name: "stock_notice");

            migrationBuilder.DropTable(
                name: "stocktake_sheet");

            migrationBuilder.DropTable(
                name: "transfer_line");

            migrationBuilder.DropTable(
                name: "transfer_slip");

            migrationBuilder.DropTable(
                name: "stock_location");
        }
    }
}

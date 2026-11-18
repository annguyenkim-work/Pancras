using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogDiscountPercents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "dealer_discount_percent",
                table: "catalog_item",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "distributor_discount_percent",
                table: "catalog_item",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "project_discount_percent",
                table: "catalog_item",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_catalog_item_dealer_discount_percent",
                table: "catalog_item",
                sql: "dealer_discount_percent IS NULL OR (dealer_discount_percent >= 0 AND dealer_discount_percent <= 100)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_catalog_item_distributor_discount_percent",
                table: "catalog_item",
                sql: "distributor_discount_percent IS NULL OR (distributor_discount_percent >= 0 AND distributor_discount_percent <= 100)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_catalog_item_project_discount_percent",
                table: "catalog_item",
                sql: "project_discount_percent IS NULL OR (project_discount_percent >= 0 AND project_discount_percent <= 100)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_catalog_item_dealer_discount_percent",
                table: "catalog_item");

            migrationBuilder.DropCheckConstraint(
                name: "ck_catalog_item_distributor_discount_percent",
                table: "catalog_item");

            migrationBuilder.DropCheckConstraint(
                name: "ck_catalog_item_project_discount_percent",
                table: "catalog_item");

            migrationBuilder.DropColumn(
                name: "dealer_discount_percent",
                table: "catalog_item");

            migrationBuilder.DropColumn(
                name: "distributor_discount_percent",
                table: "catalog_item");

            migrationBuilder.DropColumn(
                name: "project_discount_percent",
                table: "catalog_item");
        }
    }
}

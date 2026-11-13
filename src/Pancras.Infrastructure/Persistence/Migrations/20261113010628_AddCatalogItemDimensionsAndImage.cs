using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogItemDimensionsAndImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "height_mm",
                table: "catalog_item",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_path",
                table: "catalog_item",
                type: "character varying(240)",
                maxLength: 240,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "length_mm",
                table: "catalog_item",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "width_mm",
                table: "catalog_item",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "height_mm",
                table: "catalog_item");

            migrationBuilder.DropColumn(
                name: "image_path",
                table: "catalog_item");

            migrationBuilder.DropColumn(
                name: "length_mm",
                table: "catalog_item");

            migrationBuilder.DropColumn(
                name: "width_mm",
                table: "catalog_item");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogItemIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "catalog_item",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_active",
                table: "catalog_item");
        }
    }
}

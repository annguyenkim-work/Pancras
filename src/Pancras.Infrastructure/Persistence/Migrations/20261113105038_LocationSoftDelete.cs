using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LocationSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_stock_location_name",
                table: "stock_location");

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "stock_location",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_stock_location_name",
                table: "stock_location",
                column: "name",
                unique: true,
                filter: "NOT is_deleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_stock_location_name",
                table: "stock_location");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "stock_location");

            migrationBuilder.CreateIndex(
                name: "ix_stock_location_name",
                table: "stock_location",
                column: "name",
                unique: true);
        }
    }
}

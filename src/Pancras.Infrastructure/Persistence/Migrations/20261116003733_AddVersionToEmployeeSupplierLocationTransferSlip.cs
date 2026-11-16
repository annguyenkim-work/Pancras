using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionToEmployeeSupplierLocationTransferSlip : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "transfer_slip",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "supplier",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "stock_location",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "employee",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "version",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "version",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "version",
                table: "stock_location");

            migrationBuilder.DropColumn(
                name: "version",
                table: "employee");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropPurchaseLineDeclaredColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "declared_customs_value",
                table: "purchase_line");

            migrationBuilder.DropColumn(
                name: "declared_duty",
                table: "purchase_line");

            migrationBuilder.DropColumn(
                name: "declared_vat",
                table: "purchase_line");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "declared_customs_value",
                table: "purchase_line",
                type: "numeric(18,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "declared_duty",
                table: "purchase_line",
                type: "numeric(18,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "declared_vat",
                table: "purchase_line",
                type: "numeric(18,0)",
                nullable: true);
        }
    }
}

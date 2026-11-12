using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SoftDeleteSuppliers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_supplier_normalized_code",
                table: "supplier");

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "supplier",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_normalized_code",
                table: "supplier",
                column: "normalized_code",
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_supplier_normalized_code",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "supplier");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_normalized_code",
                table: "supplier",
                column: "normalized_code",
                unique: true);
        }
    }
}

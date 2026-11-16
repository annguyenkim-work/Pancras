using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Thống nhất cờ trạng thái thành <c>is_active</c>: nhà cung cấp (đảo từ is_inactive) và kho/showroom (đảo từ is_deleted).
    /// Chép giá trị sang cột mới trước khi bỏ cột cũ nên không mất dữ liệu.
    /// </summary>
    public partial class UnifyIsActiveForSupplierAndLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_supplier_normalized_code",
                table: "supplier");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "supplier",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "stock_location",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql("UPDATE supplier SET is_active = NOT is_inactive;");
            migrationBuilder.Sql("UPDATE stock_location SET is_active = NOT is_deleted;");

            migrationBuilder.DropColumn(
                name: "is_inactive",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "stock_location");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_normalized_code",
                table: "supplier",
                column: "normalized_code",
                unique: true,
                filter: "is_active = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_supplier_normalized_code",
                table: "supplier");

            migrationBuilder.AddColumn<bool>(
                name: "is_inactive",
                table: "supplier",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "stock_location",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE supplier SET is_inactive = NOT is_active;");
            migrationBuilder.Sql("UPDATE stock_location SET is_deleted = NOT is_active;");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "stock_location");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_normalized_code",
                table: "supplier",
                column: "normalized_code",
                unique: true,
                filter: "is_inactive = false");
        }
    }
}

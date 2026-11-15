using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupplierInactiveBankAndOrderSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_supplier_normalized_code",
                table: "supplier");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                table: "supplier",
                newName: "is_inactive");

            migrationBuilder.AddColumn<string>(
                name: "bank_name",
                table: "supplier",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "swift_code",
                table: "supplier",
                type: "character varying(11)",
                maxLength: 11,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "supplier_code",
                table: "purchase_order",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "supplier_name",
                table: "purchase_order",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Đơn mua đã có từ trước chưa có bản chụp: lấy mã và tên nhà cung cấp hiện tại làm bản chụp ban đầu.
            migrationBuilder.Sql(
                "UPDATE purchase_order SET supplier_code = s.code, supplier_name = s.name FROM supplier s WHERE s.id = purchase_order.supplier_id;");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_normalized_code",
                table: "supplier",
                column: "normalized_code",
                unique: true,
                filter: "is_inactive = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_supplier_normalized_code",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "bank_name",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "swift_code",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "supplier_code",
                table: "purchase_order");

            migrationBuilder.DropColumn(
                name: "supplier_name",
                table: "purchase_order");

            migrationBuilder.RenameColumn(
                name: "is_inactive",
                table: "supplier",
                newName: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_normalized_code",
                table: "supplier",
                column: "normalized_code",
                unique: true,
                filter: "is_deleted = false");
        }
    }
}

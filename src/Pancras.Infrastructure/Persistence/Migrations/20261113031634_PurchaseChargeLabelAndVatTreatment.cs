using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseChargeLabelAndVatTreatment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Đơn cũ giữ cách tính cũ (VAT không vào giá vốn) để số liệu lịch sử không đổi;
            // đơn mới mặc định tính VAT vào giá vốn (xem CreatePurchaseOrderCommand.VatDeductible).
            migrationBuilder.AddColumn<bool>(
                name: "vat_deductible",
                table: "purchase_order",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "label",
                table: "purchase_charge",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "vat_deductible",
                table: "purchase_order");

            migrationBuilder.DropColumn(
                name: "label",
                table: "purchase_charge");
        }
    }
}

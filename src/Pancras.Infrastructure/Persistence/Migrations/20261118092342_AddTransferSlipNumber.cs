using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferSlipNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "transfer_slip_number_seq");

            // Cột thêm nullable để gán số cho phiếu cũ theo thứ tự thời gian tạo (Id là UUID v7, sắp theo Id = sắp theo thời gian),
            // rồi mới đặt NOT NULL và mặc định lấy từ sequence cho phiếu mới.
            migrationBuilder.AddColumn<int>(
                name: "number",
                table: "transfer_slip",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE transfer_slip AS t SET number = s.rn FROM (SELECT id, ROW_NUMBER() OVER (ORDER BY id) AS rn FROM transfer_slip) AS s WHERE t.id = s.id;");
            migrationBuilder.Sql(
                "SELECT setval('transfer_slip_number_seq', COALESCE((SELECT MAX(number) FROM transfer_slip), 0) + 1, false);");

            migrationBuilder.AlterColumn<int>(
                name: "number",
                table: "transfer_slip",
                type: "integer",
                nullable: false,
                defaultValueSql: "nextval('transfer_slip_number_seq')",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_transfer_slip_number",
                table: "transfer_slip",
                column: "number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_transfer_slip_number",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "number",
                table: "transfer_slip");

            migrationBuilder.DropSequence(
                name: "transfer_slip_number_seq");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditStampsForAllModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "transfer_slip",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "transfer_slip",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "transfer_slip",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "transfer_slip",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "supplier",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "supplier",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "supplier",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "supplier",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "stocktake_sheet",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "stocktake_sheet",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "stocktake_sheet",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "stocktake_sheet",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "stock_location",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "stock_location",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "stock_location",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "stock_location",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "quantity_adjustment",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "quantity_adjustment",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "quantity_adjustment",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "quantity_adjustment",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "employee",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "employee",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "employee",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "employee",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "disposal_slip",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "disposal_slip",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "disposal_slip",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "disposal_slip",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "catalog_item",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "catalog_item",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "catalog_item",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "catalog_item",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Dữ liệu cũ chưa có thời điểm tạo. Mã bản ghi là UUID v7 (ký tự thứ 15 là '7'), 48 bit đầu là mili giây từ 1970 lúc
            // bản ghi được tạo: dùng nó điền created_at cho đúng. Bản ghi mã khác thì lấy thời điểm áp migration.
            // created_by để trống: không biết ai tạo; cột updated_at/by để trống: chưa ghi nhận lần sửa nào.
            foreach (var table in new[] { "catalog_item", "disposal_slip", "employee", "quantity_adjustment", "stock_location", "stocktake_sheet", "supplier", "transfer_slip" })
            {
                migrationBuilder.Sql(
                    $"UPDATE {table} SET created_at = to_timestamp((('x' || substr(replace(id::text, '-', ''), 1, 12))::bit(48))::bigint / 1000.0) " +
                    "WHERE created_at < '2000-01-01' AND substr(id::text, 15, 1) = '7';");
                migrationBuilder.Sql($"UPDATE {table} SET created_at = now() WHERE created_at < '2000-01-01';");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_at",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "stocktake_sheet");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "stocktake_sheet");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "stocktake_sheet");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "stocktake_sheet");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "stock_location");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "stock_location");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "stock_location");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "stock_location");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "quantity_adjustment");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "quantity_adjustment");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "quantity_adjustment");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "quantity_adjustment");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "disposal_slip");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "disposal_slip");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "disposal_slip");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "disposal_slip");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "catalog_item");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "catalog_item");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "catalog_item");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "catalog_item");
        }
    }
}

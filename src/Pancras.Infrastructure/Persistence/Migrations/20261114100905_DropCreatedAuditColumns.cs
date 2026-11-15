using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropCreatedAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Giữ lần ghi cuối hiện có. Bản ghi chưa từng sửa thì lấy người tạo và lúc tạo sang updated_*.
            migrationBuilder.Sql(
                """
                UPDATE catalog_item SET updated_at = created_at WHERE updated_at IS NULL;
                UPDATE catalog_item SET updated_by = created_by WHERE updated_by = '' AND created_by <> '';
                UPDATE employee SET updated_at = created_at WHERE updated_at IS NULL;
                UPDATE employee SET updated_by = created_by WHERE updated_by = '' AND created_by <> '';
                UPDATE stock_location SET updated_at = created_at WHERE updated_at IS NULL;
                UPDATE stock_location SET updated_by = created_by WHERE updated_by = '' AND created_by <> '';
                UPDATE stocktake_sheet SET updated_at = created_at WHERE updated_at IS NULL;
                UPDATE stocktake_sheet SET updated_by = created_by WHERE updated_by = '' AND created_by <> '';
                UPDATE disposal_slip SET updated_at = created_at WHERE updated_at IS NULL;
                UPDATE disposal_slip SET updated_by = created_by WHERE updated_by = '' AND created_by <> '';
                UPDATE quantity_adjustment SET updated_at = created_at WHERE updated_at IS NULL;
                UPDATE quantity_adjustment SET updated_by = created_by WHERE updated_by = '' AND created_by <> '';
                UPDATE transfer_slip SET updated_at = created_at WHERE updated_at IS NULL;
                UPDATE transfer_slip SET updated_by = created_by WHERE updated_by = '' AND created_by <> '';
                UPDATE purchase_order SET updated_at = created_at WHERE updated_at IS NULL;
                UPDATE purchase_order SET updated_by = created_by WHERE updated_by = '' AND created_by <> '';
                UPDATE supplier SET updated_at = created_at WHERE updated_at IS NULL;
                UPDATE supplier SET updated_by = created_by WHERE updated_by = '' AND created_by <> '';
                """);

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "stocktake_sheet");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "stocktake_sheet");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "stock_location");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "stock_location");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "quantity_adjustment");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "quantity_adjustment");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "purchase_order");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "purchase_order");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "disposal_slip");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "disposal_slip");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "catalog_item");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "catalog_item");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "transfer_slip",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "supplier",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "stocktake_sheet",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "stock_location",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "quantity_adjustment",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "purchase_order",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "employee",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "disposal_slip",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "catalog_item",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "transfer_slip",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

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

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "supplier",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

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

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "stocktake_sheet",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

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

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "stock_location",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

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

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "quantity_adjustment",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

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

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "purchase_order",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "purchase_order",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "purchase_order",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "employee",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

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

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "disposal_slip",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

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

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "catalog_item",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

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
        }
    }
}

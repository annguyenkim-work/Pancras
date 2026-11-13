using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseOrderAuditStamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "received_at",
                table: "purchase_order",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "received_by",
                table: "purchase_order",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "purchase_order",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "purchase_order",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Đơn cũ chưa có thời điểm tạo: lấy từ chính mã đơn. Mã đơn là UUID v7, 48 bit đầu là mili giây từ 1970 lúc đơn được tạo.
            migrationBuilder.Sql(
                "UPDATE purchase_order SET created_at = to_timestamp((('x' || substr(replace(id::text, '-', ''), 1, 12))::bit(48))::bigint / 1000.0) WHERE created_at < '2000-01-01';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_at",
                table: "purchase_order");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "purchase_order");

            migrationBuilder.DropColumn(
                name: "received_at",
                table: "purchase_order");

            migrationBuilder.DropColumn(
                name: "received_by",
                table: "purchase_order");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "purchase_order");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "purchase_order");
        }
    }
}

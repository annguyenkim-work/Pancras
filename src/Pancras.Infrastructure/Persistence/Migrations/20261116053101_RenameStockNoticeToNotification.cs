using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameStockNoticeToNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Đổi tên bảng giữ nguyên dữ liệu (bản EF tự sinh sẽ xóa bảng rồi tạo lại).
            migrationBuilder.RenameTable(name: "stock_notice", newName: "notification");
            migrationBuilder.Sql("ALTER TABLE notification RENAME CONSTRAINT pk_stock_notice TO pk_notification;");
            migrationBuilder.Sql("ALTER TABLE notification RENAME CONSTRAINT fk_stock_notice_stock_location_stock_location_id TO fk_notification_stock_locations_stock_location_id;");
            migrationBuilder.RenameIndex(name: "ix_stock_notice_stock_location_id", table: "notification", newName: "ix_notification_stock_location_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(name: "ix_notification_stock_location_id", table: "notification", newName: "ix_stock_notice_stock_location_id");
            migrationBuilder.Sql("ALTER TABLE notification RENAME CONSTRAINT fk_notification_stock_locations_stock_location_id TO fk_stock_notice_stock_location_stock_location_id;");
            migrationBuilder.Sql("ALTER TABLE notification RENAME CONSTRAINT pk_notification TO pk_stock_notice;");
            migrationBuilder.RenameTable(name: "notification", newName: "stock_notice");
        }
    }
}

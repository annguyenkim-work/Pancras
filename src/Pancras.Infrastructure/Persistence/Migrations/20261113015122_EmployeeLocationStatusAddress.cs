using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeLocationStatusAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "branch",
                table: "employee");

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "employee",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "employee",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "stock_location_id",
                table: "employee",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_location_name",
                table: "stock_location",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_employee_active_lead_per_location",
                table: "employee",
                column: "stock_location_id",
                unique: true,
                filter: "role = 'Trưởng Showroom/Kho' AND is_active");

            migrationBuilder.AddForeignKey(
                name: "fk_employee_stock_locations_stock_location_id",
                table: "employee",
                column: "stock_location_id",
                principalTable: "stock_location",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_employee_stock_locations_stock_location_id",
                table: "employee");

            migrationBuilder.DropIndex(
                name: "ix_stock_location_name",
                table: "stock_location");

            migrationBuilder.DropIndex(
                name: "ux_employee_active_lead_per_location",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "address",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "stock_location_id",
                table: "employee");

            migrationBuilder.AddColumn<string>(
                name: "branch",
                table: "employee",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
    }
}

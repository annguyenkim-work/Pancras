using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewStockAndEmployeeProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "recipient_employee_id",
                table: "stock_notice",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "seen",
                table: "stock_notice",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "step",
                table: "stock_notice",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "stock_balance",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "branch",
                table: "employee",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "department",
                table: "employee",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "employee",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "job_title",
                table: "employee",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "employee",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "recipient_employee_id",
                table: "stock_notice");

            migrationBuilder.DropColumn(
                name: "seen",
                table: "stock_notice");

            migrationBuilder.DropColumn(
                name: "step",
                table: "stock_notice");

            migrationBuilder.DropColumn(
                name: "version",
                table: "stock_balance");

            migrationBuilder.DropColumn(
                name: "branch",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "department",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "email",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "job_title",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "employee");
        }
    }
}

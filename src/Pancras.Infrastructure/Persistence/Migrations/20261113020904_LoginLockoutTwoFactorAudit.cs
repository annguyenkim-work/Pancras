using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LoginLockoutTwoFactorAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "failed_login_count",
                table: "employee",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "last_totp_step",
                table: "employee",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_until",
                table: "employee",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recovery_code_hashes",
                table: "employee",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "totp_secret",
                table: "employee",
                type: "character varying(600)",
                maxLength: 600,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "two_factor_enabled",
                table: "employee",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "audit_entry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_code = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    action = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    target_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    target_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_entry", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_actor_employee_id",
                table: "audit_entry",
                column: "actor_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_occurred_at",
                table: "audit_entry",
                column: "occurred_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_entry");

            migrationBuilder.DropColumn(
                name: "failed_login_count",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "last_totp_step",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "locked_until",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "recovery_code_hashes",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "totp_secret",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "two_factor_enabled",
                table: "employee");
        }
    }
}

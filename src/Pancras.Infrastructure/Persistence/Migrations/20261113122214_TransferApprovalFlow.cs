using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TransferApprovalFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "approved_at",
                table: "transfer_slip",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "approved_by",
                table: "transfer_slip",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "carrier",
                table: "transfer_slip",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "receive_note",
                table: "transfer_slip",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "received_at",
                table: "transfer_slip",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "received_by",
                table: "transfer_slip",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "resolution_note",
                table: "transfer_slip",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "resolved_at",
                table: "transfer_slip",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resolved_by",
                table: "transfer_slip",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "seal_number",
                table: "transfer_slip",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "shipped_at",
                table: "transfer_slip",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "shipped_by",
                table: "transfer_slip",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "received_quantity",
                table: "transfer_line",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "transfer_attachment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_slip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    stored_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    uploaded_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transfer_attachment", x => x.id);
                    table.ForeignKey(
                        name: "fk_transfer_attachment_transfer_slips_transfer_slip_id",
                        column: x => x.transfer_slip_id,
                        principalTable: "transfer_slip",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_transfer_attachment_transfer_slip_id",
                table: "transfer_attachment",
                column: "transfer_slip_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transfer_attachment");

            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "approved_by",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "carrier",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "receive_note",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "received_at",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "received_by",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "resolution_note",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "resolved_at",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "resolved_by",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "seal_number",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "shipped_at",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "shipped_by",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "received_quantity",
                table: "transfer_line");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TransferHistoryReservationReturn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "requested_by_id",
                table: "transfer_slip",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "stock_reserved",
                table: "transfer_slip",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "transfer_slip_id",
                table: "quantity_adjustment",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "transfer_event",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_slip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    actor_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transfer_event", x => x.id);
                    table.ForeignKey(
                        name: "fk_transfer_event_transfer_slips_transfer_slip_id",
                        column: x => x.transfer_slip_id,
                        principalTable: "transfer_slip",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_transfer_event_transfer_slip_id",
                table: "transfer_event",
                column: "transfer_slip_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transfer_event");

            migrationBuilder.DropColumn(
                name: "requested_by_id",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "stock_reserved",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "transfer_slip_id",
                table: "quantity_adjustment");
        }
    }
}

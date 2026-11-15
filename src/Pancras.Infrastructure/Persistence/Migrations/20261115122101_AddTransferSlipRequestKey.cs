using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferSlipRequestKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "request_key",
                table: "transfer_slip",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_transfer_slip_request_key",
                table: "transfer_slip",
                column: "request_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_transfer_slip_request_key",
                table: "transfer_slip");

            migrationBuilder.DropColumn(
                name: "request_key",
                table: "transfer_slip");
        }
    }
}

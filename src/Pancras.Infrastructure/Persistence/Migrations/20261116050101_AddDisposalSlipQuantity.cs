using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDisposalSlipQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "quantity",
                table: "disposal_slip",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "quantity",
                table: "disposal_slip");
        }
    }
}

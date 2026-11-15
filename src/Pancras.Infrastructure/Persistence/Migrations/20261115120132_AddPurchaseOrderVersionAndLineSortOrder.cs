using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pancras.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseOrderVersionAndLineSortOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "purchase_order",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "purchase_line",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE purchase_line AS line
                SET sort_order = ranked.row_number - 1
                FROM (
                    SELECT id, ROW_NUMBER() OVER (PARTITION BY purchase_order_id ORDER BY id) AS row_number
                    FROM purchase_line
                ) AS ranked
                WHERE line.id = ranked.id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "version",
                table: "purchase_order");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "purchase_line");
        }
    }
}

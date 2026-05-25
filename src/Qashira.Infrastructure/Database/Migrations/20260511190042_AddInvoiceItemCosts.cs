using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qashira.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceItemCosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TotalCost",
                table: "InvoiceItems",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "InvoiceItems",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                UPDATE InvoiceItems
                SET UnitCost = COALESCE((
                    SELECT PurchasePrice
                    FROM Products
                    WHERE Products.Id = InvoiceItems.ProductId
                ), 0),
                TotalCost = Quantity * COALESCE((
                    SELECT PurchasePrice
                    FROM Products
                    WHERE Products.Id = InvoiceItems.ProductId
                ), 0)
                WHERE ProductId IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalCost",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "InvoiceItems");
        }
    }
}

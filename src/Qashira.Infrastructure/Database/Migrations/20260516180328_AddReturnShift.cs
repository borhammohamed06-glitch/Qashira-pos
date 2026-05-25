using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qashira.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnShift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ShiftId",
                table: "Returns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE Returns
                SET ShiftId = (
                    SELECT Invoices.ShiftId
                    FROM Invoices
                    WHERE Invoices.Id = Returns.InvoiceId
                )
                WHERE ShiftId = 0
                  AND EXISTS (
                    SELECT 1
                    FROM Invoices
                    WHERE Invoices.Id = Returns.InvoiceId
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Returns_ShiftId",
                table: "Returns",
                column: "ShiftId");

            migrationBuilder.AddForeignKey(
                name: "FK_Returns_Shifts_ShiftId",
                table: "Returns",
                column: "ShiftId",
                principalTable: "Shifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Returns_Shifts_ShiftId",
                table: "Returns");

            migrationBuilder.DropIndex(
                name: "IX_Returns_ShiftId",
                table: "Returns");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                table: "Returns");
        }
    }
}

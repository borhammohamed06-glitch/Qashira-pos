using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qashira.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSuspendedInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SuspendedInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HoldNumber = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CashierId = table.Column<int>(type: "INTEGER", nullable: false),
                    ShiftId = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuspendedInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SuspendedInvoices_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SuspendedInvoices_Users_CashierId",
                        column: x => x.CashierId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SuspendedInvoiceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SuspendedInvoiceId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: true),
                    ItemName = table.Column<string>(type: "TEXT", maxLength: 220, nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuspendedInvoiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SuspendedInvoiceItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SuspendedInvoiceItems_SuspendedInvoices_SuspendedInvoiceId",
                        column: x => x.SuspendedInvoiceId,
                        principalTable: "SuspendedInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SuspendedInvoiceItems_ProductId",
                table: "SuspendedInvoiceItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SuspendedInvoiceItems_SuspendedInvoiceId",
                table: "SuspendedInvoiceItems",
                column: "SuspendedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SuspendedInvoices_CashierId_Status",
                table: "SuspendedInvoices",
                columns: new[] { "CashierId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SuspendedInvoices_HoldNumber",
                table: "SuspendedInvoices",
                column: "HoldNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SuspendedInvoices_ShiftId_Status",
                table: "SuspendedInvoices",
                columns: new[] { "ShiftId", "Status" });

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SuspendedInvoiceItems");

            migrationBuilder.DropTable(
                name: "SuspendedInvoices");
        }
    }
}

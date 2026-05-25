using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qashira.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPrintingServicesModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PrintingServiceTemplateId",
                table: "SuspendedInvoiceItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductType",
                table: "Products",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "NormalProduct");

            migrationBuilder.AddColumn<int>(
                name: "PrintingServiceTemplateId",
                table: "InvoiceItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PrintingServiceTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServiceName = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    SearchName = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    ServiceType = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    UnitName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    SellingPricePerUnit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    UsesPaper = table.Column<bool>(type: "INTEGER", nullable: false),
                    PaperConsumptionPerUnit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    UsesInk = table.Column<bool>(type: "INTEGER", nullable: false),
                    InkCostMode = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    EstimatedInkCostPerUnit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ShowInCashier = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShortcutKey = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 800, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintingServiceTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrintingMaterialConsumptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PrintingServiceTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    QuantityPerUnit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintingMaterialConsumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintingMaterialConsumptions_PrintingServiceTemplates_PrintingServiceTemplateId",
                        column: x => x.PrintingServiceTemplateId,
                        principalTable: "PrintingServiceTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PrintingMaterialConsumptions_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SuspendedInvoiceItems_PrintingServiceTemplateId",
                table: "SuspendedInvoiceItems",
                column: "PrintingServiceTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_PrintingServiceTemplateId",
                table: "InvoiceItems",
                column: "PrintingServiceTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintingMaterialConsumptions_PrintingServiceTemplateId_ProductId",
                table: "PrintingMaterialConsumptions",
                columns: new[] { "PrintingServiceTemplateId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrintingMaterialConsumptions_ProductId",
                table: "PrintingMaterialConsumptions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintingServiceTemplates_IsActive_ShowInCashier",
                table: "PrintingServiceTemplates",
                columns: new[] { "IsActive", "ShowInCashier" });

            migrationBuilder.CreateIndex(
                name: "IX_PrintingServiceTemplates_SearchName",
                table: "PrintingServiceTemplates",
                column: "SearchName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceItems_PrintingServiceTemplates_PrintingServiceTemplateId",
                table: "InvoiceItems",
                column: "PrintingServiceTemplateId",
                principalTable: "PrintingServiceTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SuspendedInvoiceItems_PrintingServiceTemplates_PrintingServiceTemplateId",
                table: "SuspendedInvoiceItems",
                column: "PrintingServiceTemplateId",
                principalTable: "PrintingServiceTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceItems_PrintingServiceTemplates_PrintingServiceTemplateId",
                table: "InvoiceItems");

            migrationBuilder.DropForeignKey(
                name: "FK_SuspendedInvoiceItems_PrintingServiceTemplates_PrintingServiceTemplateId",
                table: "SuspendedInvoiceItems");

            migrationBuilder.DropTable(
                name: "PrintingMaterialConsumptions");

            migrationBuilder.DropTable(
                name: "PrintingServiceTemplates");

            migrationBuilder.DropIndex(
                name: "IX_SuspendedInvoiceItems_PrintingServiceTemplateId",
                table: "SuspendedInvoiceItems");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceItems_PrintingServiceTemplateId",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "PrintingServiceTemplateId",
                table: "SuspendedInvoiceItems");

            migrationBuilder.DropColumn(
                name: "ProductType",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PrintingServiceTemplateId",
                table: "InvoiceItems");
        }
    }
}

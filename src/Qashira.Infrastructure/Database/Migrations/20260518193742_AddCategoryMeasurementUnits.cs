using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qashira.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryMeasurementUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PackageCount",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UnitsPerPackage",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MeasurementUnit",
                table: "Categories",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "Piece");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PackageCount",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UnitsPerPackage",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MeasurementUnit",
                table: "Categories");
        }
    }
}

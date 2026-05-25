using Qashira.Domain.Common;
using Qashira.Domain.Enums;

namespace Qashira.Domain.Entities;

public sealed class Category : Entity
{
    public string Name { get; set; } = string.Empty;
    public string SearchName { get; set; } = string.Empty;
    public MeasurementUnit MeasurementUnit { get; set; } = MeasurementUnit.Piece;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public sealed class Product : Entity
{
    public string Name { get; set; } = string.Empty;
    public string SearchName { get; set; } = string.Empty;
    public string InternalCode { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public ProductType ProductType { get; set; } = ProductType.NormalProduct;
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal StockQuantity { get; set; }
    public int PackageCount { get; set; }
    public int UnitsPerPackage { get; set; }
    public int LowStockThreshold { get; set; } = 3;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public ICollection<PrintingMaterialConsumption> PrintingMaterialConsumptions { get; set; } = new List<PrintingMaterialConsumption>();
}

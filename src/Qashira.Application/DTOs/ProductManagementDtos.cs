using Qashira.Domain.Enums;

namespace Qashira.Application.DTOs;

public sealed record ProductDetailsDto(
    int Id,
    string Name,
    string Barcode,
    string InternalCode,
    ProductType ProductType,
    int? CategoryId,
    string? CategoryName,
    decimal PurchasePrice,
    decimal SalePrice,
    decimal StockQuantity,
    int PackageCount,
    int UnitsPerPackage,
    int LowStockThreshold,
    bool IsActive)
{
    public string ProductTypeText => ProductTypeLabels.ToArabic(ProductType);
}

public sealed record CategoryOptionDto(int Id, string Name, MeasurementUnit MeasurementUnit)
{
    public string MeasurementUnitText => MeasurementUnitLabels.ToArabic(MeasurementUnit);
}

public sealed record CategoryDetailsDto(
    int Id,
    string Name,
    MeasurementUnit MeasurementUnit,
    int ProductCount,
    bool IsActive,
    DateTimeOffset CreatedAt)
{
    public string StatusText => IsActive ? "نشط" : "غير نشط";
    public string MeasurementUnitText => MeasurementUnitLabels.ToArabic(MeasurementUnit);
}

public sealed record UpsertProductRequest(
    int? Id,
    string Name,
    string? Barcode,
    string? InternalCode,
    ProductType ProductType,
    int? CategoryId,
    decimal PurchasePrice,
    decimal SalePrice,
    decimal StockQuantity,
    int PackageCount,
    int UnitsPerPackage,
    int LowStockThreshold);

public sealed record UpsertCategoryRequest(int? Id, string Name, MeasurementUnit MeasurementUnit, bool IsActive);

public sealed record MeasurementUnitOptionDto(MeasurementUnit Value, string Name);

public sealed record ProductTypeOptionDto(ProductType Value, string Name);

public static class ProductTypeLabels
{
    public static IReadOnlyList<ProductTypeOptionDto> SellableOptions { get; } =
    [
        new(ProductType.NormalProduct, "منتج عادي"),
        new(ProductType.PrintedProduct, "منتج مطبوع")
    ];

    public static string ToArabic(ProductType productType) => productType switch
    {
        ProductType.NormalProduct => "منتج عادي",
        ProductType.PrintingMaterial => "خامة طباعة",
        ProductType.PrintedProduct => "منتج مطبوع",
        ProductType.PrintingService => "خدمة طباعة",
        _ => "منتج عادي"
    };
}

public static class MeasurementUnitLabels
{
    public static IReadOnlyList<MeasurementUnitOptionDto> Options { get; } =
    [
        new(MeasurementUnit.Piece, "قطعة"),
        new(MeasurementUnit.Package, "عبوة"),
        new(MeasurementUnit.Box, "علبة"),
        new(MeasurementUnit.Carton, "كرتونة"),
        new(MeasurementUnit.Meter, "متر"),
        new(MeasurementUnit.Kilogram, "كيلو"),
        new(MeasurementUnit.Liter, "لتر")
    ];

    public static string ToArabic(MeasurementUnit unit) => unit switch
    {
        MeasurementUnit.Piece => "قطعة",
        MeasurementUnit.Package => "عبوة",
        MeasurementUnit.Box => "علبة",
        MeasurementUnit.Carton => "كرتونة",
        MeasurementUnit.Meter => "متر",
        MeasurementUnit.Kilogram => "كيلو",
        MeasurementUnit.Liter => "لتر",
        _ => "قطعة"
    };
}

public sealed record ProductImportResultDto(
    int TotalRows,
    int CreatedCount,
    int UpdatedCount,
    int RejectedCount,
    string SafetyBackupPath,
    IReadOnlyList<string> Errors);

public sealed record ProductExportResultDto(
    string ExportPath,
    int ExportedCount);

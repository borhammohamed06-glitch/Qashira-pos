using Qashira.Domain.Enums;

namespace Qashira.Application.DTOs;

public sealed record PrintingMaterialDto(
    int Id,
    string Name,
    string Barcode,
    string InternalCode,
    int? CategoryId,
    string? CategoryName,
    MeasurementUnit? MeasurementUnit,
    decimal PurchasePrice,
    decimal StockQuantity,
    int LowStockThreshold,
    bool IsActive)
{
    public string StatusText => IsActive ? "نشط" : "غير نشط";
    public string MeasurementUnitText => MeasurementUnit.HasValue
        ? MeasurementUnitLabels.ToArabic(MeasurementUnit.Value)
        : "-";
}

public sealed record UpsertPrintingMaterialRequest(
    int? Id,
    string Name,
    string? Barcode,
    int? CategoryId,
    decimal PurchasePrice,
    decimal StockQuantity,
    int LowStockThreshold);

using Qashira.Domain.Enums;

namespace Qashira.Application.DTOs;

public sealed record InventoryProductDto(
    int Id,
    string Name,
    string Barcode,
    decimal StockQuantity,
    int LowStockThreshold,
    bool IsLowStock);

public sealed record StockMovementDto(
    int Id,
    string ProductName,
    StockMovementType MovementType,
    decimal Quantity,
    decimal OldQuantity,
    decimal NewQuantity,
    string? ReferenceType,
    DateTimeOffset CreatedAt)
{
    public string MovementTypeName => MovementType switch
    {
        StockMovementType.Sale => "بيع",
        StockMovementType.Return => "مرتجع",
        StockMovementType.ManualIncrease => "زيادة يدوية",
        StockMovementType.ManualDecrease => "نقص يدوي",
        StockMovementType.Adjustment => "تسوية",
        _ => MovementType.ToString()
    };

    public string ReferenceName => ReferenceType switch
    {
        nameof(Qashira.Domain.Entities.Invoice) => "فاتورة بيع",
        nameof(Qashira.Domain.Entities.Return) => "مرتجع",
        "PrintingServiceMaterial" => "استهلاك خامات خدمة طباعة",
        "ManualStockAdjustment" => "تعديل يدوي",
        "ProductOpeningStock" => "مخزون افتتاحي",
        null or "" => "-",
        _ => ReferenceType
    };

    public string CreatedAtText => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}

public sealed record LowStockNotificationDto(
    int Id,
    int ProductId,
    string ProductName,
    string Message,
    decimal CurrentQuantity,
    int Threshold,
    DateTimeOffset CreatedAt);

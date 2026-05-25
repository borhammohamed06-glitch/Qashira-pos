using Qashira.Domain.Enums;

namespace Qashira.Application.DTOs;

public sealed record PrintingServiceTemplateListItemDto(
    int Id,
    string ServiceName,
    PrintingServiceType ServiceType,
    string UnitName,
    decimal SellingPricePerUnit,
    bool UsesPaper,
    bool UsesInk,
    decimal EstimatedInkCostPerUnit,
    bool ShowInCashier,
    bool IsActive,
    string? ShortcutKey)
{
    public string ServiceTypeText => PrintingServiceTypeLabels.ToArabic(ServiceType);
    public string StatusText => IsActive ? "نشط" : "غير نشط";
    public string CashierVisibilityText => ShowInCashier ? "ظاهر في الكاشير" : "مخفي من الكاشير";
    public string PriceText => $"{SellingPricePerUnit:0.00} ج.م / {UnitName}";
}

public sealed record PrintingServiceTemplateDetailsDto(
    int Id,
    string ServiceName,
    PrintingServiceType ServiceType,
    string UnitName,
    decimal SellingPricePerUnit,
    bool UsesPaper,
    decimal PaperConsumptionPerUnit,
    bool UsesInk,
    InkCostMode InkCostMode,
    decimal EstimatedInkCostPerUnit,
    bool ShowInCashier,
    bool IsActive,
    string? ShortcutKey,
    string? Notes,
    IReadOnlyCollection<PrintingMaterialConsumptionDto> Materials);

public sealed record PrintingMaterialConsumptionDto(
    int ProductId,
    string ProductName,
    string Barcode,
    decimal CurrentStockQuantity,
    decimal PurchasePrice,
    decimal QuantityPerUnit,
    string? Notes);

public sealed record PrintingMaterialConsumptionUpsertDto(
    int ProductId,
    decimal QuantityPerUnit,
    string? Notes);

public sealed record UpsertPrintingServiceTemplateRequest(
    int? Id,
    string ServiceName,
    PrintingServiceType ServiceType,
    string UnitName,
    decimal SellingPricePerUnit,
    bool UsesPaper,
    decimal PaperConsumptionPerUnit,
    bool UsesInk,
    InkCostMode InkCostMode,
    decimal EstimatedInkCostPerUnit,
    bool ShowInCashier,
    bool IsActive,
    string? ShortcutKey,
    string? Notes,
    IReadOnlyCollection<PrintingMaterialConsumptionUpsertDto> Materials);

public sealed record PrintingServiceTypeOptionDto(PrintingServiceType Value, string Name);

public static class PrintingServiceTypeLabels
{
    public static IReadOnlyList<PrintingServiceTypeOptionDto> Options { get; } =
    [
        new(PrintingServiceType.BlackAndWhitePrint, "طباعة أبيض وأسود"),
        new(PrintingServiceType.ColorPrint, "طباعة ألوان"),
        new(PrintingServiceType.Copy, "تصوير"),
        new(PrintingServiceType.Scan, "سكان"),
        new(PrintingServiceType.Lamination, "تغليف"),
        new(PrintingServiceType.Binding, "تجليد"),
        new(PrintingServiceType.MemoPrint, "طباعة مذكرة"),
        new(PrintingServiceType.Other, "أخرى")
    ];

    public static string ToArabic(PrintingServiceType serviceType) =>
        Options.FirstOrDefault(x => x.Value == serviceType)?.Name ?? "أخرى";
}

public sealed record InkCostModeOptionDto(InkCostMode Value, string Name);

public static class InkCostModeLabels
{
    public static IReadOnlyList<InkCostModeOptionDto> Options { get; } =
    [
        new(InkCostMode.None, "بدون تكلفة حبر"),
        new(InkCostMode.FixedEstimatedCostPerUnit, "تكلفة تقديرية ثابتة للوحدة")
    ];

    public static string ToArabic(InkCostMode inkCostMode) =>
        Options.FirstOrDefault(x => x.Value == inkCostMode)?.Name ?? "بدون تكلفة حبر";
}

public sealed record PrintingMaterialProductOptionDto(
    int Id,
    string Name,
    string Barcode,
    decimal StockQuantity,
    decimal PurchasePrice)
{
    public string DisplayText => string.IsNullOrWhiteSpace(Barcode)
        ? $"{Name} - المتاح {StockQuantity:0.###}"
        : $"{Name} | {Barcode} - المتاح {StockQuantity:0.###}";
}

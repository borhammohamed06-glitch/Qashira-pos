using Qashira.Domain.Enums;

namespace Qashira.Application.DTOs;

public sealed record CompleteSaleRequest(
    int CashierId,
    int ShiftId,
    decimal DiscountAmount,
    PaymentMethod PaymentMethod,
    IReadOnlyCollection<SaleLineRequest> Lines);

public sealed record SaleLineRequest(
    int? ProductId,
    string ItemName,
    decimal Quantity,
    decimal UnitPrice,
    ItemType ItemType,
    int? PrintingServiceTemplateId = null);

public sealed record SaleResultDto(
    int InvoiceId,
    string InvoiceNumber,
    decimal NetAmount);

public sealed record SuspendInvoiceRequest(
    int CashierId,
    int ShiftId,
    decimal DiscountAmount,
    IReadOnlyCollection<SaleLineRequest> Lines);

public sealed record SuspendedInvoiceSummaryDto(
    int Id,
    string HoldNumber,
    string CashierName,
    int LineCount,
    decimal ItemCount,
    decimal TotalAmount,
    decimal DiscountAmount,
    DateTimeOffset CreatedAt);

public sealed record SuspendedInvoiceDetailsDto(
    int Id,
    string HoldNumber,
    decimal DiscountAmount,
    IReadOnlyCollection<SuspendedInvoiceLineDto> Lines);

public sealed record SuspendedInvoiceLineDto(
    int? ProductId,
    string ItemName,
    string Barcode,
    decimal Quantity,
    decimal UnitPrice,
    ItemType ItemType,
    int? PrintingServiceTemplateId = null);

public sealed record SuspendInvoiceResultDto(
    int SuspendedInvoiceId,
    string HoldNumber,
    decimal TotalAmount);

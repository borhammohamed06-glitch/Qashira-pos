namespace Qashira.Application.DTOs;

public sealed record ReceiptDto(
    int InvoiceId,
    string InvoiceNumber,
    string ReceiptBarcodeValue,
    DateTimeOffset CreatedAt,
    string CashierName,
    decimal OriginalTotalAmount,
    decimal TotalAmount,
    decimal DiscountAmount,
    decimal ReturnedAmount,
    decimal NetAmount,
    IReadOnlyList<ReceiptLineDto> Lines);

public sealed record ReceiptLineDto(
    string ItemName,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalPrice);

namespace Qashira.Application.DTOs;

public sealed record InvoiceHistorySearchRequest(
    DateTimeOffset From,
    DateTimeOffset To,
    string? SearchText = null,
    int Take = 500);

public sealed record InvoiceHistoryListItemDto(
    int InvoiceId,
    string InvoiceNumber,
    DateTimeOffset CreatedAt,
    string CashierName,
    decimal ItemCount,
    decimal OriginalAmount,
    decimal ReturnedAmount,
    decimal NetAmount,
    string StatusName);

public sealed record InvoiceHistoryDetailsDto(
    int InvoiceId,
    string InvoiceNumber,
    DateTimeOffset CreatedAt,
    string CashierName,
    decimal TotalAmount,
    decimal DiscountAmount,
    decimal ReturnedAmount,
    decimal NetAmount,
    IReadOnlyList<InvoiceHistoryLineDto> Lines,
    IReadOnlyList<InvoiceHistoryReturnDto> Returns);

public sealed record InvoiceHistoryLineDto(
    string ItemName,
    decimal SoldQuantity,
    decimal ReturnedQuantity,
    decimal RemainingQuantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record InvoiceHistoryReturnDto(
    DateTimeOffset CreatedAt,
    string UserName,
    decimal Amount,
    string Reason);

namespace Qashira.Application.DTOs;

public sealed record InvoiceForReturnDto(
    int InvoiceId,
    string InvoiceNumber,
    decimal NetAmount,
    DateTimeOffset CreatedAt,
    IReadOnlyList<InvoiceItemForReturnDto> Items);

public sealed record ReturnInvoiceMatchDto(
    int InvoiceId,
    string InvoiceNumber,
    decimal NetAmount,
    DateTimeOffset CreatedAt);

public sealed record InvoiceItemForReturnDto(
    int InvoiceItemId,
    int? ProductId,
    string ItemName,
    decimal SoldQuantity,
    decimal AlreadyReturnedQuantity,
    decimal ReturnableQuantity,
    decimal UnitPrice);

public sealed record ReturnLineRequest(
    int InvoiceItemId,
    decimal Quantity);

public sealed record CreateReturnRequest(
    int InvoiceId,
    int UserId,
    int ShiftId,
    string? Reason,
    IReadOnlyCollection<ReturnLineRequest> Lines);

public sealed record ReturnResultDto(
    int ReturnId,
    decimal TotalReturnedAmount);

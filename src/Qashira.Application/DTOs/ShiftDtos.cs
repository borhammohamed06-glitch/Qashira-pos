namespace Qashira.Application.DTOs;

public sealed record ShiftSummaryDto(
    int ShiftId,
    decimal OpeningCash,
    decimal CashSales,
    decimal ReturnsAmount,
    decimal ExpectedCash,
    int InvoiceCount,
    DateTimeOffset OpenedAt);

public sealed record CloseShiftResultDto(
    int ShiftId,
    decimal ExpectedCash,
    decimal ClosingCash,
    decimal Difference);

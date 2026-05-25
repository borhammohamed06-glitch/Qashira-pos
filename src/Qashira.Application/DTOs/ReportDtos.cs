namespace Qashira.Application.DTOs;

public sealed record SalesReportRequest(
    DateTimeOffset From,
    DateTimeOffset To);

public sealed record SalesReportDto(
    DateTimeOffset From,
    DateTimeOffset To,
    int InvoiceCount,
    int ReturnCount,
    decimal ReturnedItemQuantity,
    decimal GrossSales,
    decimal ProductSales,
    decimal PrintingServiceSales,
    decimal Discounts,
    decimal Returns,
    decimal CostOfGoodsSold,
    decimal NetSales,
    decimal NetProfit,
    decimal WeeklyNetProfit,
    decimal MonthlyNetProfit,
    decimal AverageInvoice,
    IReadOnlyList<TopSellingProductDto> TopProducts);

public sealed record TopSellingProductDto(
    string ProductName,
    decimal Quantity,
    decimal TotalSales,
    decimal NetProfit);

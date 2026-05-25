using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Application.Permissions;
using Qashira.Domain.Entities;
using Qashira.Domain.Enums;
using Qashira.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Application.Services;

public sealed class ReportService(
    IApplicationDbContext dbContext,
    IPermissionService permissionService) : IReportService
{
    private const int ReportScanBatchSize = 1000;

    public async Task<Result<SalesReportDto>> GetSalesReportAsync(SalesReportRequest request, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanViewReports);

        if (request.To <= request.From)
        {
            return Result<SalesReportDto>.Failure("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.");
        }

        var invoices = await GetInvoicesInRangeAsync(request.From, request.To, cancellationToken);
        var returns = await GetReturnsInRangeAsync(request.From, request.To, cancellationToken);
        var returnTotals = CalculateReturnTotals(returns);

        var grossSales = invoices.Sum(x => x.TotalAmount);
        var productSales = invoices
            .SelectMany(x => x.Items)
            .Where(x => x.ItemType == ItemType.Product)
            .Sum(x => x.TotalPrice) - returnTotals.ProductAmount;
        var printingServiceSales = invoices
            .SelectMany(x => x.Items)
            .Where(x => x.ItemType == ItemType.PrintingService)
            .Sum(x => x.TotalPrice) - returnTotals.PrintingServiceAmount;
        var discounts = invoices.Sum(x => x.DiscountAmount);
        var returnAmount = returnTotals.TotalAmount;
        var returnedItemQuantity = returns.SelectMany(x => x.Items).Sum(x => x.Quantity);
        var costOfGoodsSold = CalculateCostOfGoodsSold(invoices, returnTotals);
        var netSales = invoices.Sum(x => x.NetAmount) - returnAmount;
        var netProfit = CalculateNetProfit(invoices, returnTotals);
        var weeklyNetProfit = await CalculateRangeProfitAsync(GetCurrentWeekStart(), DateTimeOffset.Now, cancellationToken);
        var monthlyNetProfit = await CalculateRangeProfitAsync(GetCurrentMonthStart(), DateTimeOffset.Now, cancellationToken);
        var averageInvoice = invoices.Count == 0 ? 0m : netSales / invoices.Count;
        var topProducts = CalculateTopProducts(invoices, returns);

        return Result<SalesReportDto>.Success(new SalesReportDto(
            request.From,
            request.To,
            invoices.Count,
            returns.Count,
            returnedItemQuantity,
            grossSales,
            productSales,
            printingServiceSales,
            discounts,
            returnAmount,
            costOfGoodsSold,
            netSales,
            netProfit,
            weeklyNetProfit,
            monthlyNetProfit,
            averageInvoice,
            topProducts));
    }

    private async Task<decimal> CalculateRangeProfitAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var invoices = await GetInvoicesInRangeAsync(from, to, cancellationToken);
        var returns = await GetReturnsInRangeAsync(from, to, cancellationToken);
        return CalculateNetProfit(invoices, CalculateReturnTotals(returns));
    }

    private async Task<List<Invoice>> GetInvoicesInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var invoices = new List<Invoice>();
        var scanned = 0;

        while (true)
        {
            var batch = await dbContext.Invoices
                .AsNoTracking()
                .Include(x => x.Items)
                .OrderByDescending(x => x.Id)
                .Skip(scanned)
                .Take(ReportScanBatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            invoices.AddRange(batch.Where(x => x.CreatedAt >= from && x.CreatedAt < to));
            scanned += batch.Count;

            if (batch.Count < ReportScanBatchSize || batch.All(x => x.CreatedAt < from))
            {
                break;
            }
        }

        return invoices;
    }

    private async Task<List<Return>> GetReturnsInRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var returns = new List<Return>();
        var scanned = 0;

        while (true)
        {
            var batch = await dbContext.Returns
                .AsNoTracking()
                .Include(x => x.Items)
                .ThenInclude(x => x.InvoiceItem)
                .OrderByDescending(x => x.Id)
                .Skip(scanned)
                .Take(ReportScanBatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            returns.AddRange(batch.Where(x => x.CreatedAt >= from && x.CreatedAt < to));
            scanned += batch.Count;

            if (batch.Count < ReportScanBatchSize || batch.All(x => x.CreatedAt < from))
            {
                break;
            }
        }

        return returns;
    }

    private static decimal CalculateNetProfit(IReadOnlyCollection<Invoice> invoices, ReturnTotals returnTotals)
    {
        var revenue = invoices
            .SelectMany(x => x.Items)
            .Sum(x => x.TotalPrice);

        var salesCost = invoices
            .SelectMany(x => x.Items)
            .Sum(x => x.TotalCost);

        var discounts = invoices.Sum(x => x.DiscountAmount);
        return revenue - salesCost - discounts - returnTotals.ProfitImpact;
    }

    private static decimal CalculateCostOfGoodsSold(IReadOnlyCollection<Invoice> invoices, ReturnTotals returnTotals)
    {
        var salesCost = invoices
            .SelectMany(x => x.Items)
            .Sum(x => x.TotalCost);

        return salesCost - returnTotals.ProductCost;
    }

    private static ReturnTotals CalculateReturnTotals(IReadOnlyCollection<Return> returns)
    {
        var returnedItems = returns.SelectMany(x => x.Items).ToArray();
        var productItems = returnedItems
            .Where(x => x.InvoiceItem.ItemType == ItemType.Product)
            .ToArray();
        var printingServiceItems = returnedItems
            .Where(x => x.InvoiceItem.ItemType == ItemType.PrintingService)
            .ToArray();
        var returnedCost = returnedItems.Sum(x => x.InvoiceItem.UnitCost * x.Quantity);

        return new ReturnTotals(
            returnedItems.Sum(x => x.TotalPrice),
            productItems.Sum(x => x.TotalPrice),
            printingServiceItems.Sum(x => x.TotalPrice),
            returnedCost,
            returnedItems.Sum(x => x.TotalPrice - (x.InvoiceItem.UnitCost * x.Quantity)));
    }

    private static IReadOnlyList<TopSellingProductDto> CalculateTopProducts(
        IReadOnlyCollection<Invoice> invoices,
        IReadOnlyCollection<Return> returns)
    {
        var returnedByProductKey = returns
            .SelectMany(x => x.Items)
            .Where(x => x.InvoiceItem.ItemType == ItemType.Product)
            .GroupBy(x => GetProductGroupKey(x.InvoiceItem.ProductId, x.InvoiceItem.ItemName))
            .ToDictionary(
                x => x.Key,
                x => new ReturnedProductTotals(
                    x.Sum(item => item.Quantity),
                    x.Sum(item => item.TotalPrice),
                    x.Sum(item => item.TotalPrice - (item.InvoiceItem.UnitCost * item.Quantity))));

        return invoices
            .SelectMany(x => x.Items)
            .Where(x => x.ItemType == ItemType.Product)
            .GroupBy(x => GetProductGroupKey(x.ProductId, x.ItemName))
            .Select(x =>
            {
                returnedByProductKey.TryGetValue(x.Key, out var returned);
                return new TopSellingProductDto(
                    x.First().ItemName,
                    x.Sum(item => item.Quantity) - (returned?.Quantity ?? 0),
                    x.Sum(item => item.TotalPrice) - (returned?.TotalSales ?? 0m),
                    x.Sum(item => item.TotalPrice - item.TotalCost) - (returned?.NetProfit ?? 0m));
            })
            .Where(x => x.Quantity > 0 || x.TotalSales > 0)
            .OrderByDescending(x => x.Quantity)
            .ThenByDescending(x => x.TotalSales)
            .Take(10)
            .ToArray();
    }

    private static DateTimeOffset GetCurrentWeekStart()
    {
        var today = DateTimeOffset.Now.Date;
        var daysSinceSaturday = ((int)today.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;
        return new DateTimeOffset(today.AddDays(-daysSinceSaturday), TimeZoneInfo.Local.GetUtcOffset(today));
    }

    private static DateTimeOffset GetCurrentMonthStart()
    {
        var today = DateTimeOffset.Now.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        return new DateTimeOffset(monthStart, TimeZoneInfo.Local.GetUtcOffset(monthStart));
    }

    private static string GetProductGroupKey(int? productId, string itemName) =>
        productId.HasValue ? $"P:{productId.Value}" : $"N:{itemName}";

    private sealed record ReturnTotals(
        decimal TotalAmount,
        decimal ProductAmount,
        decimal PrintingServiceAmount,
        decimal ProductCost,
        decimal ProfitImpact);

    private sealed record ReturnedProductTotals(decimal Quantity, decimal TotalSales, decimal NetProfit);
}

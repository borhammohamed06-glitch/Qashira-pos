using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Application.Permissions;
using Qashira.Domain.Entities;
using Qashira.Shared.Arabic;
using Qashira.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Application.Services;

public sealed class InvoiceHistoryService(
    IApplicationDbContext dbContext,
    IPermissionService permissionService) : IInvoiceHistoryService
{
    private const int MaxInvoiceHistoryRows = 500;
    private const int MaxNormalizedSearchScanRows = 900;
    private const int InvoiceHistoryScanBatchSize = 500;

    public async Task<Result<IReadOnlyList<InvoiceHistoryListItemDto>>> SearchAsync(
        InvoiceHistorySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanViewReports);

        if (request.To <= request.From)
        {
            return Result<IReadOnlyList<InvoiceHistoryListItemDto>>.Failure("تاريخ النهاية يجب أن يكون بعد تاريخ البداية.");
        }

        var searchText = request.SearchText?.Trim() ?? string.Empty;
        var normalizedSearch = ArabicTextNormalizer.NormalizeForSearch(searchText);
        var digitSearch = DigitsOnly(searchText);
        var take = Math.Clamp(request.Take, 20, MaxInvoiceHistoryRows);

        List<Invoice> invoices;
        if (string.IsNullOrWhiteSpace(searchText))
        {
            invoices = await LoadLatestInvoicesInDateRangeAsync(request.From, request.To, take, cancellationToken);
        }
        else
        {
            var databaseMatches = await IncludeInvoiceHistoryGraph(ApplyDatabaseSearch(
                    dbContext.Invoices.AsNoTracking(),
                    searchText,
                    digitSearch))
                .OrderByDescending(x => x.Id)
                .Take(MaxNormalizedSearchScanRows)
                .ToListAsync(cancellationToken);

            var candidates = databaseMatches;
            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                var latestRangeInvoices = await LoadLatestInvoicesInDateRangeAsync(
                    request.From,
                    request.To,
                    MaxNormalizedSearchScanRows,
                    cancellationToken);

                candidates = databaseMatches
                    .Concat(latestRangeInvoices)
                    .GroupBy(x => x.Id)
                    .Select(x => x.First())
                    .ToList();
            }

            invoices = candidates
                .Where(x => x.CreatedAt >= request.From && x.CreatedAt < request.To)
                .Where(x => MatchesSearch(x, searchText, normalizedSearch, digitSearch))
                .OrderByDescending(x => x.CreatedAt)
                .Take(take)
                .ToList();
        }

        var invoiceIds = invoices.Select(x => x.Id).ToArray();
        var returns = invoiceIds.Length == 0
            ? []
            : await dbContext.Returns
                .AsNoTracking()
                .Where(x => invoiceIds.Contains(x.InvoiceId))
                .ToListAsync(cancellationToken);

        var returnedByInvoiceId = returns
            .GroupBy(x => x.InvoiceId)
            .ToDictionary(x => x.Key, x => x.Sum(item => item.TotalReturnedAmount));

        var result = invoices
            .Select(x =>
            {
                var returnedAmount = returnedByInvoiceId.GetValueOrDefault(x.Id);
                return new InvoiceHistoryListItemDto(
                    x.Id,
                    x.InvoiceNumber,
                    x.CreatedAt,
                    x.Cashier.FullName,
                    x.Items.Sum(item => item.Quantity),
                    x.NetAmount,
                    returnedAmount,
                    Math.Max(0, x.NetAmount - returnedAmount),
                    GetStatusName(x.NetAmount, returnedAmount));
            })
            .ToArray();

        return Result<IReadOnlyList<InvoiceHistoryListItemDto>>.Success(result);
    }

    private static IQueryable<Invoice> IncludeInvoiceHistoryGraph(IQueryable<Invoice> query) =>
        query
            .Include(x => x.Cashier)
            .Include(x => x.Items);

    private static IQueryable<Invoice> ApplyDatabaseSearch(IQueryable<Invoice> query, string searchText, string digitSearch)
    {
        return query.Where(x =>
            (!string.IsNullOrWhiteSpace(digitSearch) && x.InvoiceNumber.Contains(digitSearch)) ||
            x.InvoiceNumber.Contains(searchText) ||
            x.Cashier.FullName.Contains(searchText) ||
            x.Items.Any(item => item.ItemName.Contains(searchText)));
    }

    private async Task<List<Invoice>> LoadLatestInvoicesInDateRangeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        int take,
        CancellationToken cancellationToken)
    {
        var matched = new List<Invoice>(take);
        var scanned = 0;

        while (matched.Count < take)
        {
            var batch = await IncludeInvoiceHistoryGraph(dbContext.Invoices.AsNoTracking())
                .OrderByDescending(x => x.Id)
                .Skip(scanned)
                .Take(InvoiceHistoryScanBatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            matched.AddRange(batch.Where(x => x.CreatedAt >= from && x.CreatedAt < to));
            scanned += batch.Count;

            if (batch.Count < InvoiceHistoryScanBatchSize || batch.All(x => x.CreatedAt < from))
            {
                break;
            }
        }

        return matched
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToList();
    }

    public async Task<Result<InvoiceHistoryDetailsDto>> GetDetailsAsync(
        int invoiceId,
        CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanViewReports);

        var invoice = await dbContext.Invoices
            .AsNoTracking()
            .Include(x => x.Cashier)
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == invoiceId, cancellationToken);

        if (invoice is null)
        {
            return Result<InvoiceHistoryDetailsDto>.Failure("لم يتم العثور على الفاتورة.");
        }

        var returns = await dbContext.Returns
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Items)
            .Where(x => x.InvoiceId == invoiceId)
            .ToListAsync(cancellationToken);

        returns = returns
            .OrderBy(x => x.CreatedAt)
            .ToList();

        var returnedByItemId = returns
            .SelectMany(x => x.Items)
            .GroupBy(x => x.InvoiceItemId)
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Quantity));

        var lines = invoice.Items
            .OrderBy(x => x.Id)
            .Select(x =>
            {
                var returnedQuantity = returnedByItemId.GetValueOrDefault(x.Id);
                var remainingQuantity = Math.Max(0, x.Quantity - returnedQuantity);
                return new InvoiceHistoryLineDto(
                    x.ItemName,
                    x.Quantity,
                    returnedQuantity,
                    remainingQuantity,
                    x.UnitPrice,
                    remainingQuantity * x.UnitPrice);
            })
            .ToArray();

        var returnRows = returns
            .Select(x => new InvoiceHistoryReturnDto(
                x.CreatedAt,
                x.User.FullName,
                x.TotalReturnedAmount,
                string.IsNullOrWhiteSpace(x.Reason) ? "-" : x.Reason))
            .ToArray();

        var returnedAmount = returns.Sum(x => x.TotalReturnedAmount);

        return Result<InvoiceHistoryDetailsDto>.Success(new InvoiceHistoryDetailsDto(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.CreatedAt,
            invoice.Cashier.FullName,
            invoice.TotalAmount,
            invoice.DiscountAmount,
            returnedAmount,
            Math.Max(0, invoice.NetAmount - returnedAmount),
            lines,
            returnRows));
    }

    private static bool MatchesSearch(
        Invoice invoice,
        string searchText,
        string normalizedSearch,
        string digitSearch)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(digitSearch))
        {
            var invoiceDigits = DigitsOnly(invoice.InvoiceNumber);
            if (invoiceDigits.EndsWith(digitSearch, StringComparison.Ordinal) ||
                invoiceDigits.Contains(digitSearch, StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (invoice.InvoiceNumber.Contains(searchText, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            return false;
        }

        return ArabicTextNormalizer.NormalizeForSearch(invoice.Cashier.FullName).Contains(normalizedSearch) ||
               invoice.Items.Any(x => ArabicTextNormalizer.NormalizeForSearch(x.ItemName).Contains(normalizedSearch));
    }

    private static string DigitsOnly(string value) =>
        new(value.Where(char.IsDigit).ToArray());

    private static string GetStatusName(decimal invoiceNetAmount, decimal returnedAmount)
    {
        if (returnedAmount <= 0)
        {
            return "مكتملة";
        }

        return returnedAmount >= invoiceNetAmount ? "مرتجعة بالكامل" : "مرتجع جزئي";
    }
}

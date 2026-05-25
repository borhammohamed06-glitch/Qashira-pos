using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Application.Services;

public sealed class ReceiptService(IApplicationDbContext dbContext) : IReceiptService
{
    public async Task<Result<ReceiptDto>> GetReceiptAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await dbContext.Invoices
            .AsNoTracking()
            .Include(x => x.Cashier)
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == invoiceId, cancellationToken);

        if (invoice is null)
        {
            return Result<ReceiptDto>.Failure("لم يتم العثور على الفاتورة.");
        }

        var returnedItemRows = await dbContext.ReturnItems
            .AsNoTracking()
            .Where(x => x.Return.InvoiceId == invoice.Id)
            .Select(x => new
            {
                x.InvoiceItemId,
                x.Quantity,
                x.TotalPrice
            })
            .ToListAsync(cancellationToken);

        var returnedItems = returnedItemRows
            .GroupBy(x => x.InvoiceItemId)
            .Select(x => new
            {
                InvoiceItemId = x.Key,
                Quantity = x.Sum(item => item.Quantity),
                Amount = x.Sum(item => item.TotalPrice)
            })
            .ToArray();

        var returnedByInvoiceItemId = returnedItems.ToDictionary(x => x.InvoiceItemId, x => x.Quantity);
        var returnedAmount = returnedItems.Sum(x => x.Amount);

        var lines = invoice.Items
            .OrderBy(x => x.Id)
            .Select(x =>
            {
                var returnedQuantity = returnedByInvoiceItemId.GetValueOrDefault(x.Id);
                var remainingQuantity = Math.Max(0, x.Quantity - returnedQuantity);
                return new ReceiptLineDto(
                    x.ItemName,
                    remainingQuantity,
                    x.UnitPrice,
                    remainingQuantity * x.UnitPrice);
            })
            .Where(x => x.Quantity > 0)
            .ToArray();

        var remainingTotal = lines.Sum(x => x.TotalPrice);
        var discount = CalculateRemainingDiscount(remainingTotal, invoice.TotalAmount, invoice.DiscountAmount);
        var remainingNet = Math.Max(0, remainingTotal - discount);

        return Result<ReceiptDto>.Success(new ReceiptDto(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.InvoiceNumber,
            invoice.CreatedAt,
            invoice.Cashier.FullName,
            invoice.TotalAmount,
            remainingTotal,
            discount,
            returnedAmount,
            remainingNet,
            lines));
    }

    private static decimal CalculateRemainingDiscount(decimal remainingTotal, decimal invoiceTotal, decimal invoiceDiscount)
    {
        if (remainingTotal <= 0 || invoiceTotal <= 0 || invoiceDiscount <= 0)
        {
            return 0;
        }

        var discountRatio = Math.Min(invoiceDiscount, invoiceTotal) / invoiceTotal;
        return Math.Min(
            remainingTotal,
            Math.Round(remainingTotal * discountRatio, 2, MidpointRounding.AwayFromZero));
    }
}

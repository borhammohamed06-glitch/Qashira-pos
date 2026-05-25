using System.Text.Json;
using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Application.Permissions;
using Qashira.Domain.Entities;
using Qashira.Domain.Enums;
using Qashira.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Application.Services;

public sealed class ReturnService(
    IApplicationDbContext dbContext,
    IPermissionService permissionService) : IReturnService
{
    public async Task<Result<IReadOnlyList<ReturnInvoiceMatchDto>>> SearchInvoicesAsync(string invoiceSearchText, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanReturnInvoice);

        var searchText = invoiceSearchText.Trim();
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return Result<IReadOnlyList<ReturnInvoiceMatchDto>>.Failure("أدخل رقم الفاتورة.");
        }

        var candidates = BuildInvoiceNumberCandidates(searchText);
        var digitSearch = ExtractDigits(searchText);
        var normalizedSearch = NormalizeInvoiceSearch(searchText);

        var invoices = (await dbContext.Invoices
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Take(300)
            .Select(x => new ReturnInvoiceMatchDto(
                x.Id,
                x.InvoiceNumber,
                x.NetAmount,
                x.CreatedAt))
            .ToListAsync(cancellationToken))
            .Select(x => new { Invoice = x, Rank = RankInvoiceMatch(x.InvoiceNumber, normalizedSearch, digitSearch, candidates) })
            .Where(x => x.Rank < 100)
            .OrderBy(x => x.Rank)
            .ThenByDescending(x => x.Invoice.InvoiceId)
            .Take(20)
            .Select(x => x.Invoice)
            .ToList();

        return Result<IReadOnlyList<ReturnInvoiceMatchDto>>.Success(invoices);
    }

    public async Task<Result<InvoiceForReturnDto>> FindInvoiceAsync(string invoiceNumber, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanReturnInvoice);

        var searchText = invoiceNumber.Trim();
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return Result<InvoiceForReturnDto>.Failure("أدخل رقم الفاتورة.");
        }

        var candidateNumbers = BuildInvoiceNumberCandidates(searchText);
        var digitSearch = ExtractDigits(searchText);

        var matchingInvoices = await dbContext.Invoices
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x =>
                candidateNumbers.Contains(x.InvoiceNumber) ||
                x.InvoiceNumber.EndsWith(searchText) ||
                (!string.IsNullOrWhiteSpace(digitSearch) && x.InvoiceNumber.EndsWith(digitSearch)))
            .OrderByDescending(x => x.Id)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (matchingInvoices.Count == 0)
        {
            return Result<InvoiceForReturnDto>.Failure("لم يتم العثور على الفاتورة.");
        }

        if (matchingInvoices.Count > 1)
        {
            return Result<InvoiceForReturnDto>.Failure("يوجد أكثر من فاتورة بنفس الأرقام. اكتب رقم أطول من رقم الفاتورة.");
        }

        var invoice = matchingInvoices[0];

        var returnedQuantityRows = await dbContext.ReturnItems
            .AsNoTracking()
            .Where(x => x.Return.InvoiceId == invoice.Id)
            .Select(x => new { x.InvoiceItemId, x.Quantity })
            .ToListAsync(cancellationToken);

        var returnedQuantities = returnedQuantityRows
            .GroupBy(x => x.InvoiceItemId)
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Quantity));

        var items = invoice.Items
            .OrderBy(x => x.Id)
            .Select(item =>
            {
                var returned = returnedQuantities.GetValueOrDefault(item.Id);
                var returnable = Math.Max(0, item.Quantity - returned);
                return new InvoiceItemForReturnDto(
                    item.Id,
                    item.ProductId,
                    item.ItemName,
                    item.Quantity,
                    returned,
                    returnable,
                    item.UnitPrice);
            })
            .ToArray();

        return Result<InvoiceForReturnDto>.Success(new InvoiceForReturnDto(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.NetAmount,
            invoice.CreatedAt,
            items));
    }

    public async Task<Result<ReturnResultDto>> CreateReturnAsync(CreateReturnRequest request, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanReturnInvoice);

        var shiftIsOpen = await dbContext.Shifts
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.ShiftId && x.CashierId == request.UserId && x.Status == ShiftStatus.Open, cancellationToken);

        if (!shiftIsOpen)
        {
            return Result<ReturnResultDto>.Failure("لا يوجد شيفت مفتوح لهذا المستخدم.");
        }

        var lines = request.Lines.Where(x => x.Quantity > 0).ToArray();
        if (lines.Length == 0)
        {
            return Result<ReturnResultDto>.Failure("اختر صنفاً واحداً على الأقل للإرجاع.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var invoice = await dbContext.Invoices
                .Include(x => x.Items)
                .SingleOrDefaultAsync(x => x.Id == request.InvoiceId, cancellationToken);

            if (invoice is null)
            {
                return Result<ReturnResultDto>.Failure("لم يتم العثور على الفاتورة.");
            }

            var returnedQuantityRows = await dbContext.ReturnItems
                .Where(x => x.Return.InvoiceId == invoice.Id)
                .Select(x => new { x.InvoiceItemId, x.Quantity })
                .ToListAsync(cancellationToken);

            var returnedQuantities = returnedQuantityRows
                .GroupBy(x => x.InvoiceItemId)
                .ToDictionary(x => x.Key, x => x.Sum(item => item.Quantity));

            var returnEntity = new Return
            {
                InvoiceId = invoice.Id,
                UserId = request.UserId,
                ShiftId = request.ShiftId,
                Reason = request.Reason,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.Returns.Add(returnEntity);
            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var line in lines)
            {
                var invoiceItem = invoice.Items.SingleOrDefault(x => x.Id == line.InvoiceItemId);
                if (invoiceItem is null)
                {
                    return Result<ReturnResultDto>.Failure("يوجد صنف غير صحيح في طلب الإرجاع.");
                }

                var alreadyReturned = returnedQuantities.GetValueOrDefault(invoiceItem.Id);
                var returnable = invoiceItem.Quantity - alreadyReturned;

                if (line.Quantity > returnable)
                {
                    return Result<ReturnResultDto>.Failure($"لا يمكن إرجاع كمية أكبر من المتاح للصنف {invoiceItem.ItemName}.");
                }

                var grossTotal = line.Quantity * invoiceItem.UnitPrice;
                var total = CalculateDiscountedReturnAmount(grossTotal, invoice.TotalAmount, invoice.DiscountAmount);
                returnEntity.Items.Add(new ReturnItem
                {
                    InvoiceItemId = invoiceItem.Id,
                    ProductId = invoiceItem.ProductId,
                    Quantity = line.Quantity,
                    UnitPrice = line.Quantity == 0 ? 0 : total / line.Quantity,
                    TotalPrice = total
                });

                returnEntity.TotalReturnedAmount += total;

                if (TryGetRestorableProductId(invoiceItem, out var productId))
                {
                    await RestoreStockAsync(productId, line.Quantity, returnEntity.Id, request.UserId, cancellationToken);
                }
            }

            var allItemsFullyReturned = invoice.Items.All(item =>
            {
                var existingReturned = returnedQuantities.GetValueOrDefault(item.Id);
                var newReturned = existingReturned + lines.Where(x => x.InvoiceItemId == item.Id).Sum(x => x.Quantity);
                return newReturned >= item.Quantity;
            });

            invoice.Status = allItemsFullyReturned ? InvoiceStatus.Returned : InvoiceStatus.PartiallyReturned;

            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = request.UserId,
                Action = AuditAction.ReturnInvoice,
                EntityName = nameof(Return),
                EntityId = returnEntity.Id.ToString(),
                NewValuesJson = ReturnValuesJson(returnEntity, invoice.InvoiceNumber, returnEntity.Items.Count),
                Description = $"تم إرجاع أصناف من الفاتورة {invoice.InvoiceNumber} بقيمة {returnEntity.TotalReturnedAmount:0.00} ج.م.",
                CreatedAt = DateTimeOffset.UtcNow
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<ReturnResultDto>.Success(
                new ReturnResultDto(returnEntity.Id, returnEntity.TotalReturnedAmount),
                "تم حفظ المرتجع بنجاح.");
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ReturnResultDto>.Failure(ex.Message);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task RestoreStockAsync(int productId, decimal quantity, int returnId, int userId, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new InvalidOperationException("لم يتم العثور على المنتج المرتجع في المخزون.");
        var oldQuantity = product.StockQuantity;
        product.StockQuantity += quantity;
        product.UpdatedAt = DateTimeOffset.UtcNow;

        dbContext.StockMovements.Add(new StockMovement
        {
            ProductId = product.Id,
            MovementType = StockMovementType.Return,
            Quantity = quantity,
            OldQuantity = oldQuantity,
            NewQuantity = product.StockQuantity,
            ReferenceType = nameof(Return),
            ReferenceId = returnId,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await StockNotificationSynchronizer.SyncLowStockAsync(dbContext, product, cancellationToken);
    }

    private static bool TryGetRestorableProductId(InvoiceItem invoiceItem, out int productId)
    {
        // Printing services consume operational materials during the original sale; returning the service refunds money
        // but does not put used paper/materials back into inventory.
        if (invoiceItem is { ItemType: ItemType.Product, ProductId: int value })
        {
            productId = value;
            return true;
        }

        productId = 0;
        return false;
    }

    private static string[] BuildInvoiceNumberCandidates(string searchText)
    {
        var normalized = searchText.Replace(" ", string.Empty);
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            normalized
        };

        if (normalized.EndsWith("-INV", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add($"INV-{normalized[..^4]}");
        }

        if (!normalized.StartsWith("INV-", StringComparison.OrdinalIgnoreCase) && normalized.All(char.IsDigit))
        {
            candidates.Add($"INV-{normalized}");
        }

        return candidates.ToArray();
    }

    private static int RankInvoiceMatch(string invoiceNumber, string normalizedSearch, string digitSearch, string[] candidates)
    {
        var normalizedInvoice = NormalizeInvoiceSearch(invoiceNumber);
        var invoiceDigits = ExtractDigits(invoiceNumber);
        var shortNumericSearch = !string.IsNullOrWhiteSpace(digitSearch)
            && digitSearch.Length <= 3
            && normalizedSearch.All(char.IsDigit);

        if (candidates.Any(x => string.Equals(x, invoiceNumber, StringComparison.OrdinalIgnoreCase)))
        {
            return 0;
        }

        if (string.Equals(normalizedInvoice, normalizedSearch, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(digitSearch) && invoiceDigits == digitSearch)
        {
            return 2;
        }

        if (!string.IsNullOrWhiteSpace(digitSearch) && invoiceDigits.EndsWith(digitSearch, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (shortNumericSearch)
        {
            return 100;
        }

        if (normalizedInvoice.EndsWith(normalizedSearch, StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (!string.IsNullOrWhiteSpace(digitSearch) && invoiceDigits.StartsWith(digitSearch, StringComparison.OrdinalIgnoreCase))
        {
            return 5;
        }

        if (normalizedInvoice.StartsWith(normalizedSearch, StringComparison.OrdinalIgnoreCase))
        {
            return 6;
        }

        if (!string.IsNullOrWhiteSpace(digitSearch) && invoiceDigits.Contains(digitSearch, StringComparison.OrdinalIgnoreCase))
        {
            return 7;
        }

        return normalizedInvoice.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ? 8 : 100;
    }

    private static string NormalizeInvoiceSearch(string value)
    {
        return value.Replace(" ", string.Empty).Trim();
    }

    private static string ExtractDigits(string value)
    {
        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static decimal CalculateDiscountedReturnAmount(decimal grossAmount, decimal invoiceTotal, decimal invoiceDiscount)
    {
        if (grossAmount <= 0 || invoiceTotal <= 0 || invoiceDiscount <= 0)
        {
            return grossAmount;
        }

        var discountRatio = Math.Min(invoiceDiscount, invoiceTotal) / invoiceTotal;
        return Math.Round(grossAmount * (1 - discountRatio), 2, MidpointRounding.AwayFromZero);
    }

    private static string ReturnValuesJson(Return returnEntity, string invoiceNumber, int itemCount) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["رقم الفاتورة"] = invoiceNumber,
            ["الشيفت"] = returnEntity.ShiftId.ToString(),
            ["قيمة المرتجع"] = $"{returnEntity.TotalReturnedAmount:0.00} ج.م",
            ["عدد الأصناف"] = itemCount.ToString(),
            ["السبب"] = string.IsNullOrWhiteSpace(returnEntity.Reason) ? "-" : returnEntity.Reason
        });
}

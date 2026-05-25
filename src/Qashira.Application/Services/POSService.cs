using System.Text.Json;
using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Application.Permissions;
using Qashira.Domain.Entities;
using Qashira.Domain.Enums;
using Qashira.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Application.Services;

public sealed class POSService(
    IApplicationDbContext dbContext,
    IPermissionService permissionService) : IPOSService
{
    private const string PrintingServiceMaterialReferenceType = "PrintingServiceMaterial";

    public async Task<Result<IReadOnlyList<SuspendedInvoiceSummaryDto>>> GetSuspendedInvoicesAsync(
        int cashierId,
        int? shiftId = null,
        CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanUsePOS);

        var query = dbContext.SuspendedInvoices
            .AsNoTracking()
            .Include(x => x.Cashier)
            .Include(x => x.Items)
            .Where(x => x.CashierId == cashierId && x.Status == SuspendedInvoiceStatus.Active);

        if (shiftId.HasValue)
        {
            query = query.Where(x => x.ShiftId == shiftId.Value);
        }

        var rows = await query.ToListAsync(cancellationToken);
        var suspendedInvoices = rows
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new SuspendedInvoiceSummaryDto(
                x.Id,
                x.HoldNumber,
                x.Cashier.FullName,
                x.Items.Count,
                x.Items.Sum(item => item.Quantity),
                x.TotalAmount,
                x.DiscountAmount,
                x.CreatedAt))
            .ToArray();

        return Result<IReadOnlyList<SuspendedInvoiceSummaryDto>>.Success(suspendedInvoices);
    }

    public async Task<Result<SuspendInvoiceResultDto>> SuspendInvoiceAsync(
        SuspendInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanUsePOS);

        var validation = await ValidateSuspendedInvoiceRequestAsync(request, cancellationToken);
        if (!validation.Succeeded)
        {
            return Result<SuspendInvoiceResultDto>.Failure(validation.Message);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var total = request.Lines.Sum(x => x.Quantity * x.UnitPrice);
            var suspendedInvoice = new SuspendedInvoice
            {
                HoldNumber = $"PENDING-{Guid.NewGuid():N}",
                CashierId = request.CashierId,
                ShiftId = request.ShiftId,
                TotalAmount = total,
                DiscountAmount = request.DiscountAmount,
                Status = SuspendedInvoiceStatus.Active,
                CreatedAt = now
            };

            foreach (var line in request.Lines)
            {
                var barcode = string.Empty;
                if (line.ProductId.HasValue)
                {
                    barcode = await dbContext.Products
                        .Where(x => x.Id == line.ProductId.Value)
                        .Select(x => x.Barcode)
                        .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
                }

                suspendedInvoice.Items.Add(new SuspendedInvoiceItem
                {
                    ProductId = line.ProductId,
                    ItemName = line.ItemName.Trim(),
                    Barcode = barcode,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    TotalPrice = line.Quantity * line.UnitPrice,
                    ItemType = line.ItemType,
                    PrintingServiceTemplateId = line.PrintingServiceTemplateId
                });
            }

            dbContext.SuspendedInvoices.Add(suspendedInvoice);
            await dbContext.SaveChangesAsync(cancellationToken);
            suspendedInvoice.HoldNumber = $"HOLD-{suspendedInvoice.Id:000000}";

            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = request.CashierId,
                Action = AuditAction.HoldInvoice,
                EntityName = nameof(SuspendedInvoice),
                EntityId = suspendedInvoice.Id.ToString(),
                NewValuesJson = SuspendedInvoiceValuesJson(suspendedInvoice),
                Description = $"تم تعليق الفاتورة {suspendedInvoice.HoldNumber} بقيمة {total:0.00} ج.م.",
                CreatedAt = now
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<SuspendInvoiceResultDto>.Success(
                new SuspendInvoiceResultDto(suspendedInvoice.Id, suspendedInvoice.HoldNumber, total),
                "تم تعليق الفاتورة بنجاح.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Result<SuspendedInvoiceDetailsDto>> ResumeSuspendedInvoiceAsync(
        int suspendedInvoiceId,
        int cashierId,
        int shiftId,
        CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanUsePOS);

        var shiftValidation = await ValidateOpenShiftAsync(cashierId, shiftId, cancellationToken);
        if (!shiftValidation.Succeeded)
        {
            return Result<SuspendedInvoiceDetailsDto>.Failure(shiftValidation.Message);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var suspendedInvoice = await dbContext.SuspendedInvoices
                .Include(x => x.Items)
                .SingleOrDefaultAsync(x =>
                    x.Id == suspendedInvoiceId &&
                    x.CashierId == cashierId &&
                    x.ShiftId == shiftId &&
                    x.Status == SuspendedInvoiceStatus.Active,
                    cancellationToken);

            if (suspendedInvoice is null)
            {
                return Result<SuspendedInvoiceDetailsDto>.Failure("لم يتم العثور على فاتورة معلقة صالحة للاسترجاع.");
            }

            var allowNegativeStock = await GetBoolSettingAsync("AllowNegativeStock", false, cancellationToken);
            var stockValidation = await ValidateStockAvailabilityWithPrintingAsync(
                suspendedInvoice.Items.Select(x => new SaleLineRequest(
                    x.ProductId,
                    x.ItemName,
                    x.Quantity,
                    x.UnitPrice,
                    x.ItemType,
                    x.PrintingServiceTemplateId)).ToArray(),
                allowNegativeStock,
                cancellationToken);

            if (!stockValidation.Succeeded)
            {
                return Result<SuspendedInvoiceDetailsDto>.Failure(stockValidation.Message);
            }

            var oldValuesJson = SuspendedInvoiceValuesJson(suspendedInvoice);
            suspendedInvoice.Status = SuspendedInvoiceStatus.Resumed;
            suspendedInvoice.UpdatedAt = DateTimeOffset.UtcNow;

            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = cashierId,
                Action = AuditAction.ResumeHeldInvoice,
                EntityName = nameof(SuspendedInvoice),
                EntityId = suspendedInvoice.Id.ToString(),
                OldValuesJson = oldValuesJson,
                NewValuesJson = SuspendedInvoiceValuesJson(suspendedInvoice),
                Description = $"تم استرجاع الفاتورة المعلقة {suspendedInvoice.HoldNumber}.",
                CreatedAt = suspendedInvoice.UpdatedAt.Value
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<SuspendedInvoiceDetailsDto>.Success(
                new SuspendedInvoiceDetailsDto(
                    suspendedInvoice.Id,
                    suspendedInvoice.HoldNumber,
                    suspendedInvoice.DiscountAmount,
                    suspendedInvoice.Items
                        .OrderBy(x => x.Id)
                        .Select(x => new SuspendedInvoiceLineDto(
                            x.ProductId,
                            x.ItemName,
                            x.Barcode,
                            x.Quantity,
                            x.UnitPrice,
                            x.ItemType,
                            x.PrintingServiceTemplateId))
                        .ToArray()),
                "تم استرجاع الفاتورة المعلقة.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Result> CancelSuspendedInvoiceAsync(
        int suspendedInvoiceId,
        int cashierId,
        int shiftId,
        CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanUsePOS);

        var shiftValidation = await ValidateOpenShiftAsync(cashierId, shiftId, cancellationToken);
        if (!shiftValidation.Succeeded)
        {
            return Result.Failure(shiftValidation.Message);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var suspendedInvoice = await dbContext.SuspendedInvoices
                .Include(x => x.Items)
                .SingleOrDefaultAsync(x =>
                    x.Id == suspendedInvoiceId &&
                    x.CashierId == cashierId &&
                    x.ShiftId == shiftId &&
                    x.Status == SuspendedInvoiceStatus.Active,
                    cancellationToken);

            if (suspendedInvoice is null)
            {
                return Result.Failure("اختر فاتورة معلقة صالحة للإلغاء.");
            }

            var oldValuesJson = SuspendedInvoiceValuesJson(suspendedInvoice);
            suspendedInvoice.Status = SuspendedInvoiceStatus.Cancelled;
            suspendedInvoice.UpdatedAt = DateTimeOffset.UtcNow;

            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = cashierId,
                Action = AuditAction.CancelHeldInvoice,
                EntityName = nameof(SuspendedInvoice),
                EntityId = suspendedInvoice.Id.ToString(),
                OldValuesJson = oldValuesJson,
                NewValuesJson = SuspendedInvoiceValuesJson(suspendedInvoice),
                Description = $"تم إلغاء الفاتورة المعلقة {suspendedInvoice.HoldNumber}.",
                CreatedAt = suspendedInvoice.UpdatedAt.Value
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success("تم إلغاء الفاتورة المعلقة.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Result<SaleResultDto>> CompleteSaleAsync(CompleteSaleRequest request, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanUsePOS);

        if (request.Lines.Count == 0)
        {
            return Result<SaleResultDto>.Failure("لا يمكن حفظ فاتورة بدون أصناف.");
        }

        if (request.DiscountAmount < 0)
        {
            return Result<SaleResultDto>.Failure("قيمة الخصم لا يمكن أن تكون أقل من صفر.");
        }

        if (request.Lines.Any(x => x.Quantity <= 0 || x.UnitPrice < 0 || string.IsNullOrWhiteSpace(x.ItemName)))
        {
            return Result<SaleResultDto>.Failure("توجد بيانات غير صحيحة في أصناف الفاتورة.");
        }

        var shiftIsOpen = await dbContext.Shifts
            .AnyAsync(x => x.Id == request.ShiftId && x.CashierId == request.CashierId && x.Status == ShiftStatus.Open, cancellationToken);

        if (!shiftIsOpen)
        {
            return Result<SaleResultDto>.Failure("لا يوجد شيفت مفتوح لهذا المستخدم.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var now = DateTimeOffset.UtcNow;
            var total = request.Lines.Sum(x => x.Quantity * x.UnitPrice);
            var net = total - request.DiscountAmount;

            if (request.DiscountAmount > 0)
            {
                permissionService.EnsureCurrentUserHas(PermissionCodes.CanApplyDiscount);
                if (!await GetBoolSettingAsync("DiscountsEnabled", true, cancellationToken))
                {
                    return Result<SaleResultDto>.Failure("الخصومات متوقفة من إعدادات النظام.");
                }
            }

            if (net < 0)
            {
                return Result<SaleResultDto>.Failure("قيمة الخصم أكبر من إجمالي الفاتورة.");
            }

            var allowNegativeStock = await GetBoolSettingAsync("AllowNegativeStock", false, cancellationToken);
            var stockValidationResult = await ValidateStockAvailabilityWithPrintingAsync(request.Lines, allowNegativeStock, cancellationToken);
            if (!stockValidationResult.Succeeded)
            {
                return Result<SaleResultDto>.Failure(stockValidationResult.Message);
            }

            var printingTemplateProfiles = await LoadPrintingTemplateProfilesAsync(request.Lines, cancellationToken);

            var invoice = new Invoice
            {
                InvoiceNumber = $"PENDING-{Guid.NewGuid():N}",
                CashierId = request.CashierId,
                ShiftId = request.ShiftId,
                TotalAmount = total,
                DiscountAmount = request.DiscountAmount,
                NetAmount = net,
                PaymentMethod = request.PaymentMethod,
                Status = InvoiceStatus.Completed,
                CreatedAt = now
            };

            dbContext.Invoices.Add(invoice);
            await dbContext.SaveChangesAsync(cancellationToken);
            invoice.InvoiceNumber = $"INV-{invoice.Id:000000}";

            foreach (var line in request.Lines)
            {
                var unitCost = 0m;
                if (line.ItemType == ItemType.Product && line.ProductId.HasValue)
                {
                    var productCost = await dbContext.Products
                        .Where(x => x.Id == line.ProductId.Value)
                        .Select(x => new ProductCostSnapshot(
                            x.PurchasePrice,
                            x.UnitsPerPackage,
                            x.Category == null ? MeasurementUnit.Piece : x.Category.MeasurementUnit))
                        .SingleAsync(cancellationToken);
                    unitCost = CalculateStockUnitCost(
                        productCost.PurchasePrice,
                        productCost.UnitsPerPackage,
                        productCost.MeasurementUnit);
                }
                else if (line.ItemType == ItemType.PrintingService &&
                         line.PrintingServiceTemplateId.HasValue &&
                         printingTemplateProfiles.TryGetValue(line.PrintingServiceTemplateId.Value, out var printingProfile))
                {
                    unitCost = printingProfile.UnitCost;
                }

                invoice.Items.Add(new InvoiceItem
                {
                    ProductId = line.ProductId,
                    PrintingServiceTemplateId = line.PrintingServiceTemplateId,
                    ItemName = line.ItemName,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    UnitCost = unitCost,
                    TotalPrice = line.Quantity * line.UnitPrice,
                    TotalCost = line.Quantity * unitCost,
                    ItemType = line.ItemType
                });

                if (line.ItemType == ItemType.Product && line.ProductId.HasValue)
                {
                    await DecreaseStockAsync(
                        line.ProductId.Value,
                        line.Quantity,
                        nameof(Invoice),
                        invoice.Id,
                        request.CashierId,
                        allowNegativeStock,
                        cancellationToken);
                }
                else if (line.ItemType == ItemType.PrintingService &&
                         line.PrintingServiceTemplateId.HasValue &&
                         printingTemplateProfiles.TryGetValue(line.PrintingServiceTemplateId.Value, out var deductionProfile))
                {
                    foreach (var material in deductionProfile.Materials)
                    {
                        await DecreaseStockAsync(
                            material.ProductId,
                            material.QuantityPerUnit * line.Quantity,
                            PrintingServiceMaterialReferenceType,
                            invoice.Id,
                            request.CashierId,
                            allowNegativeStock,
                            cancellationToken);
                    }
                }
            }

            invoice.Payments.Add(new Payment
            {
                Method = request.PaymentMethod,
                Amount = net,
                CreatedAt = now
            });

            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = request.CashierId,
                Action = AuditAction.CreateInvoice,
                EntityName = nameof(Invoice),
                EntityId = invoice.Id.ToString(),
                NewValuesJson = InvoiceValuesJson(invoice),
                Description = $"تم إنشاء فاتورة بيع رقم {invoice.InvoiceNumber} بقيمة {net:0.00} ج.م.",
                CreatedAt = now
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<SaleResultDto>.Success(new SaleResultDto(invoice.Id, invoice.InvoiceNumber, invoice.NetAmount), "تم حفظ الفاتورة بنجاح.");
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<SaleResultDto>.Failure(ex.Message);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<Result> ValidateStockAvailabilityAsync(IReadOnlyCollection<SaleLineRequest> lines, bool allowNegativeStock, CancellationToken cancellationToken)
    {
        var productQuantities = lines
            .Where(x => x.ItemType == ItemType.Product && x.ProductId.HasValue)
            .GroupBy(x => x.ProductId!.Value)
            .Select(x => new { ProductId = x.Key, RequestedQuantity = x.Sum(line => line.Quantity) })
            .ToArray();

        if (productQuantities.Length == 0)
        {
            return Result.Success();
        }

        var productIds = productQuantities.Select(x => x.ProductId).ToArray();
        var products = await dbContext.Products
            .AsNoTracking()
            .Where(x => productIds.Contains(x.Id) && x.IsActive)
            .Select(x => new { x.Id, x.Name, x.StockQuantity })
            .ToListAsync(cancellationToken);

        foreach (var line in productQuantities)
        {
            var product = products.SingleOrDefault(x => x.Id == line.ProductId);
            if (product is null)
            {
                return Result.Failure("يوجد منتج غير متاح في الفاتورة. حدّث البحث ثم حاول مرة أخرى.");
            }

            if (!allowNegativeStock && product.StockQuantity < line.RequestedQuantity)
            {
                return Result.Failure($"المخزون غير كافي للمنتج {product.Name}. المتاح {product.StockQuantity} والمطلوب {line.RequestedQuantity}.");
            }
        }

        return Result.Success();
    }

    private async Task<Result> ValidateStockAvailabilityWithPrintingAsync(
        IReadOnlyCollection<SaleLineRequest> lines,
        bool allowNegativeStock,
        CancellationToken cancellationToken)
    {
        var requiredQuantities = lines
            .Where(x => x.ItemType == ItemType.Product && x.ProductId.HasValue)
            .GroupBy(x => x.ProductId!.Value)
            .ToDictionary(x => x.Key, x => x.Sum(line => line.Quantity));

        var serviceLines = lines
            .Where(x => x.ItemType == ItemType.PrintingService && x.PrintingServiceTemplateId.HasValue)
            .ToArray();

        if (serviceLines.Length > 0)
        {
            var templateIds = serviceLines
                .Select(x => x.PrintingServiceTemplateId!.Value)
                .Distinct()
                .ToArray();

            var templates = await dbContext.PrintingServiceTemplates
                .AsNoTracking()
                .Include(x => x.MaterialConsumptions)
                .Where(x => templateIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            foreach (var line in serviceLines)
            {
                var template = templates.SingleOrDefault(x => x.Id == line.PrintingServiceTemplateId!.Value);
                if (template is null || !template.IsActive)
                {
                    return Result.Failure("خدمة الطباعة غير متاحة. حدّث شاشة الكاشير ثم حاول مرة أخرى.");
                }

                foreach (var material in template.MaterialConsumptions)
                {
                    var requiredQuantity = material.QuantityPerUnit * line.Quantity;
                    requiredQuantities[material.ProductId] = requiredQuantities.TryGetValue(material.ProductId, out var current)
                        ? current + requiredQuantity
                        : requiredQuantity;
                }
            }
        }

        if (requiredQuantities.Count == 0)
        {
            return Result.Success();
        }

        var productIds = requiredQuantities.Keys.ToArray();
        var products = await dbContext.Products
            .AsNoTracking()
            .Where(x => productIds.Contains(x.Id) && x.IsActive)
            .Select(x => new { x.Id, x.Name, x.StockQuantity })
            .ToListAsync(cancellationToken);

        foreach (var line in requiredQuantities)
        {
            var product = products.SingleOrDefault(x => x.Id == line.Key);
            if (product is null)
            {
                return Result.Failure("يوجد منتج أو خامة غير متاحة في الفاتورة. حدّث البيانات ثم حاول مرة أخرى.");
            }

            if (!allowNegativeStock && product.StockQuantity < line.Value)
            {
                return Result.Failure($"المخزون غير كافي للمنتج {product.Name}. المتاح {product.StockQuantity:0.###} والمطلوب {line.Value:0.###}.");
            }
        }

        return Result.Success();
    }

    private async Task<Dictionary<int, PrintingTemplateSaleProfile>> LoadPrintingTemplateProfilesAsync(
        IReadOnlyCollection<SaleLineRequest> lines,
        CancellationToken cancellationToken)
    {
        var templateIds = lines
            .Where(x => x.ItemType == ItemType.PrintingService && x.PrintingServiceTemplateId.HasValue)
            .Select(x => x.PrintingServiceTemplateId!.Value)
            .Distinct()
            .ToArray();

        if (templateIds.Length == 0)
        {
            return new Dictionary<int, PrintingTemplateSaleProfile>();
        }

        var templates = await dbContext.PrintingServiceTemplates
            .AsNoTracking()
            .Include(x => x.MaterialConsumptions)
            .ThenInclude(x => x.Product)
            .ThenInclude(x => x.Category)
            .Where(x => templateIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        return templates.ToDictionary(
            x => x.Id,
            x =>
            {
                var materialCost = x.MaterialConsumptions.Sum(m =>
                    m.QuantityPerUnit * CalculateStockUnitCost(
                        m.Product.PurchasePrice,
                        m.Product.UnitsPerPackage,
                        m.Product.Category?.MeasurementUnit ?? MeasurementUnit.Piece));
                var inkCost = x.UsesInk && x.InkCostMode == InkCostMode.FixedEstimatedCostPerUnit
                    ? x.EstimatedInkCostPerUnit
                    : 0m;

                return new PrintingTemplateSaleProfile(
                    materialCost + inkCost,
                    x.MaterialConsumptions
                        .Select(m => new PrintingMaterialSaleRequirement(m.ProductId, m.QuantityPerUnit))
                        .ToArray());
            });
    }

    private async Task<Result> ValidateOpenShiftAsync(int cashierId, int shiftId, CancellationToken cancellationToken)
    {
        var shiftIsOpen = await dbContext.Shifts
            .AsNoTracking()
            .AnyAsync(x => x.Id == shiftId && x.CashierId == cashierId && x.Status == ShiftStatus.Open, cancellationToken);

        return shiftIsOpen
            ? Result.Success()
            : Result.Failure("لا يوجد شيفت مفتوح لهذا المستخدم.");
    }

    private async Task<Result> ValidateSuspendedInvoiceRequestAsync(SuspendInvoiceRequest request, CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0)
        {
            return Result.Failure("لا يمكن تعليق فاتورة بدون أصناف.");
        }

        if (request.DiscountAmount < 0)
        {
            return Result.Failure("قيمة الخصم لا يمكن أن تكون أقل من صفر.");
        }

        if (request.Lines.Any(x => x.Quantity <= 0 || x.UnitPrice < 0 || string.IsNullOrWhiteSpace(x.ItemName)))
        {
            return Result.Failure("توجد بيانات غير صحيحة في أصناف الفاتورة.");
        }

        var shiftIsOpen = await dbContext.Shifts
            .AnyAsync(x => x.Id == request.ShiftId && x.CashierId == request.CashierId && x.Status == ShiftStatus.Open, cancellationToken);

        if (!shiftIsOpen)
        {
            return Result.Failure("لا يوجد شيفت مفتوح لهذا المستخدم.");
        }

        if (request.DiscountAmount > 0)
        {
            permissionService.EnsureCurrentUserHas(PermissionCodes.CanApplyDiscount);
            if (!await GetBoolSettingAsync("DiscountsEnabled", true, cancellationToken))
            {
                return Result.Failure("الخصومات متوقفة من إعدادات النظام.");
            }
        }

        var total = request.Lines.Sum(x => x.Quantity * x.UnitPrice);
        if (request.DiscountAmount > total)
        {
            return Result.Failure("قيمة الخصم أكبر من إجمالي الفاتورة.");
        }

        return Result.Success();
    }

    private async Task DecreaseStockAsync(
        int productId,
        decimal quantity,
        string referenceType,
        int invoiceId,
        int userId,
        bool allowNegativeStock,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.SingleAsync(x => x.Id == productId && x.IsActive, cancellationToken);

        if (!allowNegativeStock && product.StockQuantity < quantity)
        {
            throw new InvalidOperationException($"الكمية المتاحة من المنتج {product.Name} غير كافية.");
        }

        var oldQuantity = product.StockQuantity;
        product.StockQuantity -= quantity;
        product.UpdatedAt = DateTimeOffset.UtcNow;

        dbContext.StockMovements.Add(new StockMovement
        {
            ProductId = product.Id,
            MovementType = StockMovementType.Sale,
            Quantity = quantity,
            OldQuantity = oldQuantity,
            NewQuantity = product.StockQuantity,
            ReferenceType = referenceType,
            ReferenceId = invoiceId,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await UpsertLowStockNotificationAsync(product, cancellationToken);
    }

    private async Task UpsertLowStockNotificationAsync(Product product, CancellationToken cancellationToken)
    {
        await StockNotificationSynchronizer.SyncLowStockAsync(dbContext, product, cancellationToken);
    }

    private async Task<bool> GetBoolSettingAsync(string key, bool fallback, CancellationToken cancellationToken)
    {
        var value = await dbContext.AppSettings
            .AsNoTracking()
            .Where(x => x.Key == key)
            .Select(x => x.Value)
            .SingleOrDefaultAsync(cancellationToken);

        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static string InvoiceValuesJson(Invoice invoice) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["رقم الفاتورة"] = invoice.InvoiceNumber,
            ["الشيفت"] = invoice.ShiftId.ToString(),
            ["الإجمالي قبل الخصم"] = Money(invoice.TotalAmount),
            ["الخصم"] = Money(invoice.DiscountAmount),
            ["الصافي"] = Money(invoice.NetAmount),
            ["طريقة الدفع"] = invoice.PaymentMethod.ToString(),
            ["عدد السطور"] = invoice.Items.Count.ToString(),
            ["عدد القطع"] = invoice.Items.Sum(x => x.Quantity).ToString()
        });

    private static string SuspendedInvoiceValuesJson(SuspendedInvoice invoice) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["رقم التعليق"] = invoice.HoldNumber,
            ["الشيفت"] = invoice.ShiftId.ToString(),
            ["الإجمالي قبل الخصم"] = Money(invoice.TotalAmount),
            ["الخصم"] = Money(invoice.DiscountAmount),
            ["الصافي"] = Money(invoice.TotalAmount - invoice.DiscountAmount),
            ["عدد السطور"] = invoice.Items.Count.ToString(),
            ["عدد القطع"] = invoice.Items.Sum(x => x.Quantity).ToString(),
            ["الحالة"] = SuspendedInvoiceStatusName(invoice.Status)
        });

    private static string SuspendedInvoiceStatusName(SuspendedInvoiceStatus status) => status switch
    {
        SuspendedInvoiceStatus.Active => "معلقة",
        SuspendedInvoiceStatus.Resumed => "تم استرجاعها",
        SuspendedInvoiceStatus.Cancelled => "ملغاة",
        _ => status.ToString()
    };

    private static string Money(decimal value) => $"{value:0.00} ج.م";
    private static decimal CalculateStockUnitCost(decimal purchasePrice, int unitsPerPackage, MeasurementUnit measurementUnit)
    {
        if (measurementUnit is MeasurementUnit.Carton or MeasurementUnit.Box && unitsPerPackage > 0)
        {
            return purchasePrice / unitsPerPackage;
        }

        return purchasePrice;
    }

    private sealed record ProductCostSnapshot(
        decimal PurchasePrice,
        int UnitsPerPackage,
        MeasurementUnit MeasurementUnit);

    private sealed record PrintingTemplateSaleProfile(
        decimal UnitCost,
        IReadOnlyCollection<PrintingMaterialSaleRequirement> Materials);

    private sealed record PrintingMaterialSaleRequirement(
        int ProductId,
        decimal QuantityPerUnit);
}

using System.Text.Json;
using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Application.Permissions;
using Qashira.Domain.Entities;
using Qashira.Domain.Enums;
using Qashira.Shared.Arabic;
using Qashira.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Application.Services;

public sealed class PrintingMaterialService(
    IApplicationDbContext dbContext,
    IPermissionService permissionService,
    IBarcodeService barcodeService) : IPrintingMaterialService
{
    public async Task<IReadOnlyList<PrintingMaterialDto>> SearchAsync(
        string searchText,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageStock);

        var normalized = ArabicTextNormalizer.NormalizeForSearch(searchText);
        var query = dbContext.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.ProductType == ProductType.PrintingMaterial);

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(normalized))
        {
            query = query.Where(x =>
                x.SearchName.Contains(normalized) ||
                x.Barcode.Contains(searchText) ||
                x.InternalCode.Contains(searchText));
        }

        return await query
            .OrderBy(x => x.Name)
            .Take(200)
            .Select(x => new PrintingMaterialDto(
                x.Id,
                x.Name,
                x.Barcode,
                x.InternalCode,
                x.CategoryId,
                x.Category == null ? null : x.Category.Name,
                x.Category == null ? null : x.Category.MeasurementUnit,
                x.PurchasePrice,
                x.StockQuantity,
                x.LowStockThreshold,
                x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<PrintingMaterialDto>> SaveAsync(
        UpsertPrintingMaterialRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageStock);

        var validation = await ValidateAsync(request, cancellationToken);
        if (!validation.Succeeded)
        {
            return Result<PrintingMaterialDto>.Failure(validation.Message);
        }

        var normalizedName = ArabicTextNormalizer.NormalizeForSearch(request.Name);
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var isNew = !request.Id.HasValue || request.Id.Value == 0;
        var oldStockQuantity = 0m;
        var stockChanged = false;
        Product material;

        if (isNew)
        {
            material = new Product
            {
                ProductType = ProductType.PrintingMaterial,
                CreatedAt = now,
                Barcode = string.IsNullOrWhiteSpace(request.Barcode)
                    ? await barcodeService.GenerateUniqueBarcodeAsync(cancellationToken)
                    : request.Barcode.Trim(),
                InternalCode = await barcodeService.GenerateUniqueInternalCodeAsync(cancellationToken)
            };

            dbContext.Products.Add(material);
        }
        else
        {
            material = await dbContext.Products.SingleAsync(
                x => x.Id == request.Id!.Value && x.ProductType == ProductType.PrintingMaterial,
                cancellationToken);
            oldStockQuantity = material.StockQuantity;
            stockChanged = oldStockQuantity != request.StockQuantity;
            material.UpdatedAt = now;
            material.Barcode = string.IsNullOrWhiteSpace(request.Barcode)
                ? material.Barcode
                : request.Barcode.Trim();
        }

        material.Name = request.Name.Trim();
        material.SearchName = normalizedName;
        material.ProductType = ProductType.PrintingMaterial;
        material.CategoryId = request.CategoryId;
        material.PurchasePrice = request.PurchasePrice;
        material.SalePrice = 0m;
        material.StockQuantity = request.StockQuantity;
        material.PackageCount = 0;
        material.UnitsPerPackage = 0;
        material.LowStockThreshold = request.LowStockThreshold;
        material.IsActive = true;

        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = isNew ? AuditAction.CreateProduct : AuditAction.EditProduct,
            EntityName = nameof(Product),
            EntityId = material.Id.ToString(),
            NewValuesJson = JsonSerializer.Serialize(ToAuditShape(material)),
            Description = isNew
                ? $"تم إنشاء خامة الطباعة {material.Name}."
                : $"تم تعديل خامة الطباعة {material.Name}.",
            CreatedAt = now
        });

        if (isNew && material.StockQuantity > 0)
        {
            dbContext.StockMovements.Add(new StockMovement
            {
                ProductId = material.Id,
                MovementType = StockMovementType.ManualIncrease,
                Quantity = material.StockQuantity,
                OldQuantity = 0,
                NewQuantity = material.StockQuantity,
                ReferenceType = "InitialPrintingMaterialStock",
                UserId = userId,
                CreatedAt = now
            });
        }
        else if (stockChanged)
        {
            dbContext.StockMovements.Add(new StockMovement
            {
                ProductId = material.Id,
                MovementType = request.StockQuantity > oldStockQuantity
                    ? StockMovementType.ManualIncrease
                    : StockMovementType.ManualDecrease,
                Quantity = Math.Abs(request.StockQuantity - oldStockQuantity),
                OldQuantity = oldStockQuantity,
                NewQuantity = request.StockQuantity,
                ReferenceType = "PrintingMaterial",
                ReferenceId = material.Id,
                UserId = userId,
                CreatedAt = now
            });
        }

        if (isNew || stockChanged)
        {
            await StockNotificationSynchronizer.SyncLowStockAsync(dbContext, material, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var saved = await dbContext.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .SingleAsync(x => x.Id == material.Id, cancellationToken);

        return Result<PrintingMaterialDto>.Success(ToDto(saved), "تم حفظ خامة الطباعة بنجاح.");
    }

    public async Task<Result> SetActiveAsync(
        int materialId,
        bool isActive,
        int userId,
        CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageStock);

        var material = await dbContext.Products.SingleOrDefaultAsync(
            x => x.Id == materialId && x.ProductType == ProductType.PrintingMaterial,
            cancellationToken);

        if (material is null)
        {
            return Result.Failure("اختر خامة طباعة صحيحة.");
        }

        material.IsActive = isActive;
        material.UpdatedAt = DateTimeOffset.UtcNow;
        await StockNotificationSynchronizer.SyncLowStockAsync(dbContext, material, cancellationToken);

        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = isActive ? AuditAction.EditProduct : AuditAction.DeleteProduct,
            EntityName = nameof(Product),
            EntityId = material.Id.ToString(),
            NewValuesJson = JsonSerializer.Serialize(ToAuditShape(material)),
            Description = isActive
                ? $"تم تفعيل خامة الطباعة {material.Name}."
                : $"تم إيقاف خامة الطباعة {material.Name}.",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(isActive ? "تم تفعيل خامة الطباعة." : "تم إيقاف خامة الطباعة.");
    }

    private async Task<Result> ValidateAsync(UpsertPrintingMaterialRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure("اسم خامة الطباعة مطلوب.");
        }

        if (!request.CategoryId.HasValue)
        {
            return Result.Failure("اختر تصنيف الخامة لتحديد وحدة القياس.");
        }

        if (request.PurchasePrice < 0 || request.StockQuantity < 0 || request.LowStockThreshold < 0)
        {
            return Result.Failure("السعر والكمية وحد التنبيه لا يمكن أن تكون أقل من صفر.");
        }

        var categoryExists = await dbContext.Categories.AnyAsync(
            x => x.Id == request.CategoryId.Value && x.IsActive,
            cancellationToken);
        if (!categoryExists)
        {
            return Result.Failure("التصنيف المحدد غير موجود أو غير نشط.");
        }

        var normalizedName = ArabicTextNormalizer.NormalizeForSearch(request.Name);
        var duplicateName = await dbContext.Products.AnyAsync(
            x => x.Id != request.Id && x.SearchName == normalizedName && x.IsActive,
            cancellationToken);
        if (duplicateName)
        {
            return Result.Failure("يوجد صنف آخر بنفس الاسم.");
        }

        if (!string.IsNullOrWhiteSpace(request.Barcode))
        {
            var barcode = request.Barcode.Trim();
            var duplicateBarcode = await dbContext.Products.AnyAsync(
                x => x.Id != request.Id && x.Barcode == barcode,
                cancellationToken);
            if (duplicateBarcode)
            {
                return Result.Failure("هذا الباركود مستخدم مع صنف آخر.");
            }
        }

        return Result.Success();
    }

    private static PrintingMaterialDto ToDto(Product material) =>
        new(
            material.Id,
            material.Name,
            material.Barcode,
            material.InternalCode,
            material.CategoryId,
            material.Category?.Name,
            material.Category?.MeasurementUnit,
            material.PurchasePrice,
            material.StockQuantity,
            material.LowStockThreshold,
            material.IsActive);

    private static object ToAuditShape(Product material) => new
    {
        material.Id,
        material.Name,
        material.Barcode,
        material.InternalCode,
        ProductType = ProductTypeLabels.ToArabic(material.ProductType),
        material.CategoryId,
        material.PurchasePrice,
        material.StockQuantity,
        material.LowStockThreshold,
        material.IsActive
    };
}

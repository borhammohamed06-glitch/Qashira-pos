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

public sealed class ProductManagementService(
    IApplicationDbContext dbContext,
    IPermissionService permissionService,
    IBarcodeService barcodeService) : IProductManagementService
{
    public async Task<IReadOnlyList<ProductDetailsDto>> SearchProductsAsync(string searchText, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanEditProduct);

        var normalized = ArabicTextNormalizer.NormalizeForSearch(searchText);

        IQueryable<Product> query = dbContext.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.ProductType == ProductType.NormalProduct || x.ProductType == ProductType.PrintedProduct);

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
            .Select(x => new ProductDetailsDto(
                x.Id,
                x.Name,
                x.Barcode,
                x.InternalCode,
                x.ProductType,
                x.CategoryId,
                x.Category == null ? null : x.Category.Name,
                x.PurchasePrice,
                x.SalePrice,
                x.StockQuantity,
                x.PackageCount,
                x.UnitsPerPackage,
                x.LowStockThreshold,
                x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryOptionDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanEditProduct);

        return await dbContext.Categories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new CategoryOptionDto(x.Id, x.Name, x.MeasurementUnit))
            .ToListAsync(cancellationToken);
    }


    public async Task<Result<ProductDetailsDto>> SaveProductAsync(UpsertProductRequest request, int userId, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanEditProduct);

        var validation = await ValidateAsync(request, cancellationToken);
        if (!validation.Succeeded)
        {
            return Result<ProductDetailsDto>.Failure(validation.Message);
        }

        if (!request.CategoryId.HasValue)
        {
            return Result<ProductDetailsDto>.Failure("اختر تصنيف المنتج من إعدادات النظام أولاً.");
        }

        var categoryExists = await dbContext.Categories.AnyAsync(
            x => x.Id == request.CategoryId.Value && x.IsActive,
            cancellationToken);

        if (!categoryExists)
        {
            return Result<ProductDetailsDto>.Failure("التصنيف المحدد غير موجود أو غير نشط.");
        }

        var normalizedName = ArabicTextNormalizer.NormalizeForSearch(request.Name);
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        Product product;
        var isNew = !request.Id.HasValue || request.Id.Value == 0;
        var oldStockQuantity = 0m;
        var stockChanged = false;
        var oldLowStockThreshold = 0;
        var thresholdChanged = false;
        var oldPurchasePrice = 0m;
        var oldSalePrice = 0m;
        var priceChanged = false;
        string? oldProductValuesJson = null;

        if (isNew)
        {
            permissionService.EnsureCurrentUserHas(PermissionCodes.CanEditPrice);
            if (request.StockQuantity > 0)
            {
                permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageStock);
            }

            product = new Product
            {
                CreatedAt = now,
                Barcode = string.IsNullOrWhiteSpace(request.Barcode)
                    ? await barcodeService.GenerateUniqueBarcodeAsync(cancellationToken)
                    : request.Barcode.Trim(),
                InternalCode = await barcodeService.GenerateUniqueInternalCodeAsync(cancellationToken)
            };

            dbContext.Products.Add(product);
        }
        else
        {
            var productId = request.Id.GetValueOrDefault();
            product = await dbContext.Products.SingleAsync(x => x.Id == productId, cancellationToken);
            oldProductValuesJson = ProductValuesJson(product);

            oldPurchasePrice = product.PurchasePrice;
            oldSalePrice = product.SalePrice;
            priceChanged = oldPurchasePrice != request.PurchasePrice || oldSalePrice != request.SalePrice;
            if (priceChanged)
            {
                permissionService.EnsureCurrentUserHas(PermissionCodes.CanEditPrice);
            }

            oldStockQuantity = product.StockQuantity;
            stockChanged = oldStockQuantity != request.StockQuantity;
            oldLowStockThreshold = product.LowStockThreshold;
            thresholdChanged = oldLowStockThreshold != request.LowStockThreshold;
            if (stockChanged || thresholdChanged)
            {
                permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageStock);
            }

            product.UpdatedAt = now;
            product.Barcode = request.Barcode!.Trim();
        }

        product.Name = request.Name.Trim();
        product.SearchName = normalizedName;
        product.ProductType = request.ProductType;
        product.CategoryId = request.CategoryId;
        product.PurchasePrice = request.PurchasePrice;
        product.SalePrice = request.SalePrice;
        product.StockQuantity = request.StockQuantity;
        product.PackageCount = request.PackageCount;
        product.UnitsPerPackage = request.UnitsPerPackage;
        product.LowStockThreshold = request.LowStockThreshold;
        product.IsActive = true;

        if (!isNew)
        {
            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = AuditAction.EditProduct,
                EntityName = nameof(Product),
                EntityId = product.Id.ToString(),
                OldValuesJson = oldProductValuesJson,
                NewValuesJson = ProductValuesJson(product),
                Description = $"تم تعديل بيانات المنتج {product.Name}.",
                CreatedAt = now
            });

            if (priceChanged)
            {
                dbContext.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = AuditAction.ChangeProductPrice,
                    EntityName = nameof(Product),
                    EntityId = product.Id.ToString(),
                    OldValuesJson = ProductPriceValuesJson(oldPurchasePrice, oldSalePrice),
                    NewValuesJson = ProductPriceValuesJson(request.PurchasePrice, request.SalePrice),
                    Description = $"تم تعديل سعر المنتج {product.Name}. سعر البيع من {oldSalePrice:0.00} إلى {request.SalePrice:0.00}، وسعر الشراء من {oldPurchasePrice:0.00} إلى {request.PurchasePrice:0.00}.",
                    CreatedAt = now
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (isNew)
        {
            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = AuditAction.CreateProduct,
                EntityName = nameof(Product),
                EntityId = product.Id.ToString(),
                NewValuesJson = ProductValuesJson(product),
                Description = $"تم إنشاء المنتج {product.Name}.",
                CreatedAt = now
            });
        }

        if (isNew && product.StockQuantity > 0)
        {
            dbContext.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id,
                MovementType = StockMovementType.ManualIncrease,
                Quantity = product.StockQuantity,
                OldQuantity = 0,
                NewQuantity = product.StockQuantity,
                ReferenceType = "InitialProductStock",
                UserId = userId,
                CreatedAt = now
            });

            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = AuditAction.ChangeStockQuantity,
                EntityName = nameof(Product),
                EntityId = product.Id.ToString(),
                OldValuesJson = ProductStockValuesJson(product.Name, 0),
                NewValuesJson = ProductStockValuesJson(product.Name, product.StockQuantity),
                Description = $"تم تسجيل مخزون افتتاحي للمنتج {product.Name} بكمية {product.StockQuantity}.",
                CreatedAt = now
            });
        }
        else if (stockChanged)
        {
            dbContext.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id,
                MovementType = request.StockQuantity > oldStockQuantity ? StockMovementType.ManualIncrease : StockMovementType.ManualDecrease,
                Quantity = Math.Abs(request.StockQuantity - oldStockQuantity),
                OldQuantity = oldStockQuantity,
                NewQuantity = request.StockQuantity,
                ReferenceType = nameof(Product),
                ReferenceId = product.Id,
                UserId = userId,
                CreatedAt = now
            });

            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = AuditAction.ChangeStockQuantity,
                EntityName = nameof(Product),
                EntityId = product.Id.ToString(),
                OldValuesJson = ProductStockValuesJson(product.Name, oldStockQuantity),
                NewValuesJson = ProductStockValuesJson(product.Name, request.StockQuantity),
                Description = $"تم تعديل مخزون المنتج {product.Name} من {oldStockQuantity} إلى {request.StockQuantity}.",
                CreatedAt = now
            });
        }

        var shouldSaveFollowUpChanges = (isNew && product.StockQuantity > 0) || stockChanged;
        if (isNew || stockChanged || thresholdChanged)
        {
            await SyncLowStockNotificationInternalAsync(product, cancellationToken);
            shouldSaveFollowUpChanges = true;
        }

        if (shouldSaveFollowUpChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return Result<ProductDetailsDto>.Success(
            new ProductDetailsDto(
                product.Id,
                product.Name,
                product.Barcode,
                product.InternalCode,
                product.ProductType,
                product.CategoryId,
                null,
                product.PurchasePrice,
                product.SalePrice,
                product.StockQuantity,
                product.PackageCount,
                product.UnitsPerPackage,
                product.LowStockThreshold,
                product.IsActive),
            "تم حفظ المنتج بنجاح.");
    }

    public async Task<Result> DeactivateProductAsync(int productId, int userId, CancellationToken cancellationToken = default)
    {
        return await SetProductActiveAsync(productId, false, userId, cancellationToken);
    }

    public async Task<Result> SetProductActiveAsync(int productId, bool isActive, int userId, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(isActive ? PermissionCodes.CanEditProduct : PermissionCodes.CanDeleteProduct);

        var product = await dbContext.Products.SingleOrDefaultAsync(x => x.Id == productId, cancellationToken);
        if (product is null)
        {
            return Result.Failure("لم يتم العثور على المنتج.");
        }

        var oldValuesJson = ProductActiveValuesJson(product.Name, product.IsActive);
        product.IsActive = isActive;
        product.UpdatedAt = DateTimeOffset.UtcNow;
        await SyncLowStockNotificationInternalAsync(product, cancellationToken);

        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = isActive ? AuditAction.EditProduct : AuditAction.DeleteProduct,
            EntityName = nameof(Product),
            EntityId = product.Id.ToString(),
            OldValuesJson = oldValuesJson,
            NewValuesJson = ProductActiveValuesJson(product.Name, product.IsActive),
            Description = isActive ? $"تم تفعيل المنتج {product.Name}." : $"تم إيقاف المنتج {product.Name}.",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(isActive ? "تم تفعيل المنتج بنجاح." : "تم إيقاف المنتج بنجاح.");
    }

    private async Task SyncLowStockNotificationInternalAsync(Product product, CancellationToken cancellationToken)
    {
        await StockNotificationSynchronizer.SyncLowStockAsync(dbContext, product, cancellationToken);
    }

    private static string ProductValuesJson(Product product) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["اسم المنتج"] = product.Name,
            ["الباركود"] = product.Barcode,
            ["الكود الداخلي"] = product.InternalCode,
            ["التصنيف"] = product.CategoryId?.ToString() ?? "-",
            ["سعر الشراء"] = Money(product.PurchasePrice),
            ["سعر البيع"] = Money(product.SalePrice),
            ["المخزون"] = product.StockQuantity.ToString(),
            ["حد التنبيه"] = product.LowStockThreshold.ToString(),
            ["نشط"] = product.IsActive ? "نعم" : "لا"
        });

    private static string ProductPriceValuesJson(decimal purchasePrice, decimal salePrice) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["سعر الشراء"] = Money(purchasePrice),
            ["سعر البيع"] = Money(salePrice)
        });

    private static string ProductStockValuesJson(string productName, decimal quantity) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["المنتج"] = productName,
            ["المخزون"] = quantity.ToString()
        });

    private static string ProductActiveValuesJson(string productName, bool isActive) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["المنتج"] = productName,
            ["نشط"] = isActive ? "نعم" : "لا"
        });

    private static string Money(decimal value) => $"{value:0.00} ج.م";

    private async Task<Result> ValidateAsync(UpsertProductRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure("اسم المنتج مطلوب.");
        }

        if (request.PurchasePrice < 0 || request.SalePrice < 0)
        {
            return Result.Failure("أسعار المنتج لا يمكن أن تكون أقل من صفر.");
        }

        if (request.StockQuantity < 0)
        {
            return Result.Failure("كمية المخزون لا يمكن أن تكون أقل من صفر.");
        }

        if (request.ProductType is not (ProductType.NormalProduct or ProductType.PrintedProduct))
        {
            return Result.Failure("شاشة المنتجات تقبل المنتجات العادية والمطبوعة فقط.");
        }

        if (request.PackageCount < 0 || request.UnitsPerPackage < 0)
        {
            return Result.Failure("بيانات العلب أو الكراتين لا يمكن أن تكون أقل من صفر.");
        }

        if (request.LowStockThreshold < 0)
        {
            return Result.Failure("حد تنبيه المخزون لا يمكن أن يكون أقل من صفر.");
        }

        var normalizedName = ArabicTextNormalizer.NormalizeForSearch(request.Name);
        var duplicateName = await dbContext.Products.AnyAsync(
            x => x.Id != request.Id && x.SearchName == normalizedName && x.IsActive,
            cancellationToken);

        if (duplicateName)
        {
            return Result.Failure("يوجد منتج آخر بنفس الاسم.");
        }

        if (!string.IsNullOrWhiteSpace(request.Barcode))
        {
            var barcode = request.Barcode.Trim();
            var duplicateBarcode = await dbContext.Products.AnyAsync(
                x => x.Id != request.Id && x.Barcode == barcode,
                cancellationToken);

            if (duplicateBarcode)
            {
                return Result.Failure("هذا الباركود مستخدم مع منتج آخر.");
            }
        }

        return Result.Success();
    }
}

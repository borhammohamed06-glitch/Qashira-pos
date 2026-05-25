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

public sealed class InventoryService(
    IApplicationDbContext dbContext,
    IPermissionService permissionService) : IInventoryService
{
    public async Task<IReadOnlyList<InventoryProductDto>> SearchProductsAsync(string searchText, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageStock);

        var normalized = ArabicTextNormalizer.NormalizeForSearch(searchText);
        var query = dbContext.Products.AsNoTracking().Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(normalized))
        {
            query = query.Where(x =>
                x.SearchName.Contains(normalized) ||
                x.Barcode.Contains(searchText) ||
                x.InternalCode.Contains(searchText));
        }

        var products = await query
            .OrderBy(x => x.Name)
            .Take(200)
            .Select(x => new InventoryProductDto(
                x.Id,
                x.Name,
                x.Barcode,
                x.StockQuantity,
                x.LowStockThreshold,
                false))
            .ToListAsync(cancellationToken);

        return products
            .Select(x => x with { IsLowStock = x.StockQuantity <= x.LowStockThreshold })
            .ToArray();
    }

    public async Task<IReadOnlyList<StockMovementDto>> GetRecentMovementsAsync(int? productId = null, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageStock);

        var query = dbContext.StockMovements
            .AsNoTracking()
            .Include(x => x.Product)
            .AsQueryable();

        if (productId.HasValue)
        {
            query = query.Where(x => x.ProductId == productId.Value);
        }

        return await query
            .OrderByDescending(x => x.Id)
            .Take(150)
            .Select(x => new StockMovementDto(
                x.Id,
                x.Product.Name,
                x.MovementType,
                x.Quantity,
                x.OldQuantity,
                x.NewQuantity,
                x.ReferenceType,
                x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result> AdjustStockAsync(int productId, decimal newQuantity, string reason, int userId, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageStock);

        if (newQuantity < 0)
        {
            return Result.Failure("كمية المخزون لا يمكن أن تكون أقل من صفر.");
        }

        var product = await dbContext.Products.SingleOrDefaultAsync(x => x.Id == productId && x.IsActive, cancellationToken);
        if (product is null)
        {
            return Result.Failure("لم يتم العثور على المنتج.");
        }

        var oldQuantity = product.StockQuantity;
        if (oldQuantity == newQuantity)
        {
            return Result.Failure("لم تتغير كمية المخزون.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            product.StockQuantity = newQuantity;
            product.UpdatedAt = DateTimeOffset.UtcNow;

            var difference = newQuantity - oldQuantity;
            var movementType = difference > 0 ? StockMovementType.ManualIncrease : StockMovementType.ManualDecrease;
            var now = DateTimeOffset.UtcNow;
            var trimmedReason = string.IsNullOrWhiteSpace(reason) ? "تعديل يدوي من شاشة المخزون" : reason.Trim();

            dbContext.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id,
                MovementType = movementType,
                Quantity = Math.Abs(difference),
                OldQuantity = oldQuantity,
                NewQuantity = newQuantity,
                ReferenceType = "ManualStockAdjustment",
                UserId = userId,
                CreatedAt = now
            });

            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = AuditAction.ChangeStockQuantity,
                EntityName = nameof(Product),
                EntityId = product.Id.ToString(),
                OldValuesJson = StockValuesJson(product.Name, oldQuantity, trimmedReason),
                NewValuesJson = StockValuesJson(product.Name, newQuantity, trimmedReason),
                Description = $"تم تعديل مخزون المنتج {product.Name} من {oldQuantity} إلى {newQuantity}. السبب: {trimmedReason}",
                CreatedAt = now
            });

            await SyncLowStockNotificationInternalAsync(product, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success("تم تعديل المخزون بنجاح.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task SyncLowStockNotificationAsync(int productId, CancellationToken cancellationToken = default)
    {
        var product = await dbContext.Products.SingleAsync(x => x.Id == productId, cancellationToken);
        await SyncLowStockNotificationInternalAsync(product, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncLowStockNotificationInternalAsync(Product product, CancellationToken cancellationToken)
    {
        await StockNotificationSynchronizer.SyncLowStockAsync(dbContext, product, cancellationToken);
    }

    private static string StockValuesJson(string productName, decimal quantity, string reason) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["المنتج"] = productName,
            ["المخزون"] = quantity.ToString(),
            ["السبب"] = string.IsNullOrWhiteSpace(reason) ? "-" : reason.Trim()
        });
}

using Qashira.Application.Abstractions;
using Qashira.Domain.Entities;
using Qashira.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Application.Services;

internal static class StockNotificationSynchronizer
{
    public static async Task SyncLowStockAsync(
        IApplicationDbContext dbContext,
        Product product,
        CancellationToken cancellationToken)
    {
        var activeNotifications = await dbContext.Notifications
            .Where(x => x.ProductId == product.Id && x.Type == NotificationType.LowStock && !x.IsResolved)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var notification = activeNotifications.FirstOrDefault();
        foreach (var duplicate in activeNotifications.Skip(1))
        {
            duplicate.IsResolved = true;
            duplicate.UpdatedAt = DateTimeOffset.UtcNow;
        }

        if (product.IsActive && product.StockQuantity <= product.LowStockThreshold)
        {
            if (notification is null)
            {
                dbContext.Notifications.Add(new Notification
                {
                    ProductId = product.Id,
                    Type = NotificationType.LowStock,
                    Title = "تنبيه مخزون منخفض",
                    Message = BuildMessage(product),
                    CurrentQuantity = product.StockQuantity,
                    Threshold = product.LowStockThreshold,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                return;
            }

            notification.CurrentQuantity = product.StockQuantity;
            notification.Threshold = product.LowStockThreshold;
            notification.Message = BuildMessage(product);
            notification.UpdatedAt = DateTimeOffset.UtcNow;
            return;
        }

        if (notification is not null)
        {
            notification.IsResolved = true;
            notification.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static string BuildMessage(Product product) =>
        $"المنتج {product.Name} وصل إلى {product.StockQuantity} قطعة.";
}

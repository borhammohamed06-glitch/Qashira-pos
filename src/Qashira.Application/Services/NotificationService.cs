using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Application.Services;

public sealed class NotificationService(IApplicationDbContext dbContext) : INotificationService
{
    public async Task<IReadOnlyList<LowStockNotificationDto>> GetActiveLowStockAsync(CancellationToken cancellationToken = default)
    {
        var notifications = await dbContext.Notifications
            .AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.Type == NotificationType.LowStock && !x.IsResolved)
            .Select(x => new LowStockNotificationDto(
                x.Id,
                x.ProductId,
                x.Product.Name,
                x.Message,
                x.CurrentQuantity,
                x.Threshold,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return notifications
            .OrderBy(x => x.CurrentQuantity)
            .ThenBy(x => x.ProductName)
            .ToArray();
    }

    public async Task<int> CountActiveLowStockAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Notifications.CountAsync(
            x => x.Type == NotificationType.LowStock && !x.IsResolved,
            cancellationToken);
    }
}

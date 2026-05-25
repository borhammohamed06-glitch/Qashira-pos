using Qashira.Application.DTOs;

namespace Qashira.Application.Abstractions;

public interface INotificationService
{
    Task<IReadOnlyList<LowStockNotificationDto>> GetActiveLowStockAsync(CancellationToken cancellationToken = default);
    Task<int> CountActiveLowStockAsync(CancellationToken cancellationToken = default);
}

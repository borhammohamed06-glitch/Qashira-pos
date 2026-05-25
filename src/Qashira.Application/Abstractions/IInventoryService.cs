using Qashira.Application.DTOs;
using Qashira.Domain.Enums;
using Qashira.Shared.Results;

namespace Qashira.Application.Abstractions;

public interface IInventoryService
{
    Task<IReadOnlyList<InventoryProductDto>> SearchProductsAsync(string searchText, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockMovementDto>> GetRecentMovementsAsync(int? productId = null, CancellationToken cancellationToken = default);
    Task<Result> AdjustStockAsync(int productId, decimal newQuantity, string reason, int userId, CancellationToken cancellationToken = default);
    Task SyncLowStockNotificationAsync(int productId, CancellationToken cancellationToken = default);
}

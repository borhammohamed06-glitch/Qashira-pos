using Qashira.Application.DTOs;
using Qashira.Shared.Results;

namespace Qashira.Application.Abstractions;

public interface IProductManagementService
{
    Task<IReadOnlyList<ProductDetailsDto>> SearchProductsAsync(string searchText, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryOptionDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<Result<ProductDetailsDto>> SaveProductAsync(UpsertProductRequest request, int userId, CancellationToken cancellationToken = default);
    Task<Result> DeactivateProductAsync(int productId, int userId, CancellationToken cancellationToken = default);
    Task<Result> SetProductActiveAsync(int productId, bool isActive, int userId, CancellationToken cancellationToken = default);
}

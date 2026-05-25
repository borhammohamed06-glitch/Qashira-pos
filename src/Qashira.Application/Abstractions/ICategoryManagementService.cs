using Qashira.Application.DTOs;
using Qashira.Shared.Results;

namespace Qashira.Application.Abstractions;

public interface ICategoryManagementService
{
    Task<IReadOnlyList<CategoryDetailsDto>> GetCategoriesAsync(bool includeInactive = true, CancellationToken cancellationToken = default);
    Task<Result<CategoryDetailsDto>> SaveCategoryAsync(UpsertCategoryRequest request, int userId, CancellationToken cancellationToken = default);
    Task<Result> SetCategoryActiveAsync(int categoryId, bool isActive, int userId, CancellationToken cancellationToken = default);
}

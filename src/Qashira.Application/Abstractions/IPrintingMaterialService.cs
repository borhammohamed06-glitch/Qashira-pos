using Qashira.Application.DTOs;
using Qashira.Shared.Results;

namespace Qashira.Application.Abstractions;

public interface IPrintingMaterialService
{
    Task<IReadOnlyList<PrintingMaterialDto>> SearchAsync(
        string searchText,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<Result<PrintingMaterialDto>> SaveAsync(
        UpsertPrintingMaterialRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    Task<Result> SetActiveAsync(
        int materialId,
        bool isActive,
        int userId,
        CancellationToken cancellationToken = default);
}

using Qashira.Application.DTOs;
using Qashira.Shared.Results;

namespace Qashira.Application.Abstractions;

public interface IPrintingServiceTemplateService
{
    Task<IReadOnlyList<PrintingServiceTemplateListItemDto>> SearchAsync(
        string searchText,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<PrintingServiceTemplateDetailsDto?> GetAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PrintingServiceTemplateListItemDto>> GetCashierTemplatesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PrintingMaterialProductOptionDto>> GetMaterialProductsAsync(
        string searchText = "",
        CancellationToken cancellationToken = default);

    Task<Result<PrintingServiceTemplateDetailsDto>> SaveAsync(
        UpsertPrintingServiceTemplateRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    Task<Result> ToggleActiveAsync(
        int templateId,
        int userId,
        CancellationToken cancellationToken = default);
}

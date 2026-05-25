using Qashira.Application.DTOs;
using Qashira.Shared.Results;

namespace Qashira.Application.Abstractions;

public interface IInvoiceHistoryService
{
    Task<Result<IReadOnlyList<InvoiceHistoryListItemDto>>> SearchAsync(
        InvoiceHistorySearchRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<InvoiceHistoryDetailsDto>> GetDetailsAsync(
        int invoiceId,
        CancellationToken cancellationToken = default);
}

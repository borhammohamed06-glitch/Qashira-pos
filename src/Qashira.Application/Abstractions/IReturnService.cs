using Qashira.Application.DTOs;
using Qashira.Shared.Results;

namespace Qashira.Application.Abstractions;

public interface IReturnService
{
    Task<Result<IReadOnlyList<ReturnInvoiceMatchDto>>> SearchInvoicesAsync(string invoiceSearchText, CancellationToken cancellationToken = default);
    Task<Result<InvoiceForReturnDto>> FindInvoiceAsync(string invoiceNumber, CancellationToken cancellationToken = default);
    Task<Result<ReturnResultDto>> CreateReturnAsync(CreateReturnRequest request, CancellationToken cancellationToken = default);
}

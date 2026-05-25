using Qashira.Application.DTOs;
using Qashira.Shared.Results;

namespace Qashira.Application.Abstractions;

public interface IPOSService
{
    Task<Result<SaleResultDto>> CompleteSaleAsync(CompleteSaleRequest request, CancellationToken cancellationToken = default);
    Task<Result<SuspendInvoiceResultDto>> SuspendInvoiceAsync(SuspendInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<SuspendedInvoiceSummaryDto>>> GetSuspendedInvoicesAsync(int cashierId, int? shiftId = null, CancellationToken cancellationToken = default);
    Task<Result<SuspendedInvoiceDetailsDto>> ResumeSuspendedInvoiceAsync(int suspendedInvoiceId, int cashierId, int shiftId, CancellationToken cancellationToken = default);
    Task<Result> CancelSuspendedInvoiceAsync(int suspendedInvoiceId, int cashierId, int shiftId, CancellationToken cancellationToken = default);
}

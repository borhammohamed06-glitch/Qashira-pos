using Qashira.Application.DTOs;
using Qashira.Shared.Results;

namespace Qashira.Application.Abstractions;

public interface IReceiptService
{
    Task<Result<ReceiptDto>> GetReceiptAsync(int invoiceId, CancellationToken cancellationToken = default);
}

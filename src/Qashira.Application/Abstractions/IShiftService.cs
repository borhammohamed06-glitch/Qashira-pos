using Qashira.Application.DTOs;
using Qashira.Shared.Results;

namespace Qashira.Application.Abstractions;

public interface IShiftService
{
    Task<int?> GetOpenShiftIdAsync(int cashierId, CancellationToken cancellationToken = default);
    Task<Result<int>> OpenShiftAsync(int cashierId, decimal openingCash, CancellationToken cancellationToken = default);
    Task<Result<ShiftSummaryDto>> GetOpenShiftSummaryAsync(int cashierId, CancellationToken cancellationToken = default);
    Task<Result<CloseShiftResultDto>> CloseShiftAsync(int cashierId, decimal closingCash, CancellationToken cancellationToken = default);
}

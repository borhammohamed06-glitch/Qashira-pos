using Qashira.Application.DTOs;
using Qashira.Shared.Results;

namespace Qashira.Application.Abstractions;

public interface IReportService
{
    Task<Result<SalesReportDto>> GetSalesReportAsync(SalesReportRequest request, CancellationToken cancellationToken = default);
}

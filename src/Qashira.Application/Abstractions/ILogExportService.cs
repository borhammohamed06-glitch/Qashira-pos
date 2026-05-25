using Qashira.Application.DTOs;
using Qashira.Shared.Results;

namespace Qashira.Application.Abstractions;

public interface ILogExportService
{
    Task<Result<LogExportOperationDto>> ExportLogsAsync(int userId, CancellationToken cancellationToken = default);
}

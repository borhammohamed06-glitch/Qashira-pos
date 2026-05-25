using Qashira.Application.DTOs;

namespace Qashira.Application.Abstractions;

public interface ILogArchiveStorage
{
    Task<LogExportOperationDto> ExportLogsAsync(CancellationToken cancellationToken = default);
    Task<int> CleanupOldExportsAsync(CancellationToken cancellationToken = default);
}

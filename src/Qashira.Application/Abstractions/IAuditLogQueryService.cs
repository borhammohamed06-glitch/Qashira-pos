using Qashira.Application.DTOs;
using Qashira.Shared.Results;

namespace Qashira.Application.Abstractions;

public interface IAuditLogQueryService
{
    Task<Result<AuditLogFilterOptionsDto>> GetFilterOptionsAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AuditLogEntryDto>>> SearchAsync(
        AuditLogSearchRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    Task<Result<AuditOperationDetailDto>> GetDetailsAsync(
        int auditLogId,
        int userId,
        CancellationToken cancellationToken = default);
}

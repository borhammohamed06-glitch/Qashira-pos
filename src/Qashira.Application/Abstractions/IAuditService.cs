using Qashira.Domain.Enums;

namespace Qashira.Application.Abstractions;

public interface IAuditService
{
    Task WriteAsync(
        AuditAction action,
        string description,
        int? userId = null,
        string? entityName = null,
        string? entityId = null,
        string? oldValuesJson = null,
        string? newValuesJson = null,
        CancellationToken cancellationToken = default);
}

using Qashira.Application.Abstractions;
using Qashira.Domain.Entities;
using Qashira.Domain.Enums;

namespace Qashira.Application.Services;

public sealed class AuditService(IApplicationDbContext dbContext) : IAuditService
{
    public async Task WriteAsync(
        AuditAction action,
        string description,
        int? userId = null,
        string? entityName = null,
        string? entityId = null,
        string? oldValuesJson = null,
        string? newValuesJson = null,
        CancellationToken cancellationToken = default)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            Action = action,
            UserId = userId,
            Description = description,
            EntityName = entityName,
            EntityId = entityId,
            OldValuesJson = oldValuesJson,
            NewValuesJson = newValuesJson,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

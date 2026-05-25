using Qashira.Domain.Enums;

namespace Qashira.Application.DTOs;

public sealed record AuditLogSearchRequest(
    DateTimeOffset From,
    DateTimeOffset To,
    string? SearchText = null,
    int? UserId = null,
    AuditAction? Action = null,
    int Take = 300);

public sealed record AuditLogEntryDto(
    int Id,
    DateTimeOffset CreatedAt,
    string UserName,
    AuditAction Action,
    string ActionName,
    string EntityName,
    string EntityId,
    string Description);

public sealed record AuditLogFilterOptionsDto(
    IReadOnlyList<AuditUserFilterOptionDto> Users,
    IReadOnlyList<AuditActionFilterOptionDto> Actions);

public sealed record AuditUserFilterOptionDto(
    int? UserId,
    string DisplayName);

public sealed record AuditActionFilterOptionDto(
    AuditAction? Action,
    string DisplayName);

public sealed record AuditOperationDetailDto(
    string Title,
    string Summary,
    IReadOnlyList<AuditDetailFieldDto> Fields,
    IReadOnlyList<AuditDetailLineDto> Lines,
    IReadOnlyList<AuditTimelineEntryDto> Timeline);

public sealed record AuditDetailFieldDto(
    string Label,
    string Value);

public sealed record AuditDetailLineDto(
    string Name,
    string Type,
    string Quantity,
    string UnitPrice,
    string Total,
    string Profit);

public sealed record AuditTimelineEntryDto(
    DateTimeOffset CreatedAt,
    string ActionName,
    string Description);

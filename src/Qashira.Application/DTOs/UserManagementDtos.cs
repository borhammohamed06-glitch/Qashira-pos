namespace Qashira.Application.DTOs;

public sealed record RoleOptionDto(int Id, string Name, string DisplayName);

public sealed record PermissionOptionDto(
    int Id,
    string Code,
    string Name,
    bool IsGranted);

public sealed record UserDetailsDto(
    int Id,
    string FullName,
    string Username,
    int RoleId,
    string RoleName,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

public sealed record UpsertUserRequest(
    int? Id,
    string FullName,
    string Username,
    string? Password,
    int RoleId,
    bool IsActive,
    IReadOnlyCollection<string> PermissionCodes);

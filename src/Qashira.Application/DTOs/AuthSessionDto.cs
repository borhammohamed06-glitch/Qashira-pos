namespace Qashira.Application.DTOs;

public sealed record AuthSessionDto(
    int UserId,
    string FullName,
    string Username,
    IReadOnlyCollection<string> Permissions,
    bool MustChangePassword);

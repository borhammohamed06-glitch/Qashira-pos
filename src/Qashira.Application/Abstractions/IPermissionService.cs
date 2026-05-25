namespace Qashira.Application.Abstractions;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(int userId, string permissionCode, CancellationToken cancellationToken = default);
    void EnsureCurrentUserHas(string permissionCode);
}

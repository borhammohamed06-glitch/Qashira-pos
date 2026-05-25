using Qashira.Application.DTOs;
using Qashira.Shared.Results;

namespace Qashira.Application.Abstractions;

public interface IUserManagementService
{
    Task<IReadOnlyList<UserDetailsDto>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleOptionDto>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PermissionOptionDto>> GetUserPermissionsAsync(int? userId, int roleId, CancellationToken cancellationToken = default);
    Task<Result<UserDetailsDto>> SaveUserAsync(UpsertUserRequest request, int adminUserId, CancellationToken cancellationToken = default);
    Task<Result> SetUserActiveAsync(int userId, bool isActive, int adminUserId, CancellationToken cancellationToken = default);
}

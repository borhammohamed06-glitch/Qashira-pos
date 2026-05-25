using Qashira.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Application.Services;

public sealed class PermissionService(IApplicationDbContext dbContext, ICurrentUserSession currentUserSession) : IPermissionService
{
    public async Task<bool> HasPermissionAsync(int userId, string permissionCode, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive)
            .Select(x => new { x.Id, x.RoleId })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return false;
        }

        return await dbContext.UserPermissions
            .AnyAsync(x => x.UserId == user.Id && x.Permission.Code == permissionCode, cancellationToken);
    }

    public void EnsureCurrentUserHas(string permissionCode)
    {
        if (!currentUserSession.IsAuthenticated || !currentUserSession.Permissions.Contains(permissionCode))
        {
            throw new UnauthorizedAccessException("ليس لديك صلاحية لتنفيذ هذه العملية.");
        }
    }
}

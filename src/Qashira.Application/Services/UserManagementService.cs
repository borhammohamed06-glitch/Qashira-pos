using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Application.Permissions;
using Qashira.Domain.Entities;
using Qashira.Domain.Enums;
using Qashira.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Application.Services;

public sealed class UserManagementService(
    IApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
    IPermissionService permissionService) : IUserManagementService
{
    public async Task<IReadOnlyList<UserDetailsDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageUsers);

        return await dbContext.Users
            .AsNoTracking()
            .Include(x => x.Role)
            .OrderBy(x => x.FullName)
            .Select(x => new UserDetailsDto(
                x.Id,
                x.FullName,
                x.Username,
                x.RoleId,
                RoleDisplayName(x.Role.Name, x.Role.DisplayName),
                x.IsActive,
                x.CreatedAt,
                x.LastLoginAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoleOptionDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageUsers);

        return await dbContext.Roles
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new RoleOptionDto(x.Id, x.Name, RoleDisplayName(x.Name, x.DisplayName)))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PermissionOptionDto>> GetUserPermissionsAsync(int? userId, int roleId, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageUsers);

        int[] grantedPermissionIds;
        if (userId.HasValue && await dbContext.UserPermissions.AnyAsync(x => x.UserId == userId.Value, cancellationToken))
        {
            grantedPermissionIds = await dbContext.UserPermissions
                .AsNoTracking()
                .Where(x => x.UserId == userId.Value)
                .Select(x => x.PermissionId)
                .ToArrayAsync(cancellationToken);
        }
        else
        {
            grantedPermissionIds = await dbContext.RolePermissions
                .AsNoTracking()
                .Where(x => x.RoleId == roleId)
                .Select(x => x.PermissionId)
                .ToArrayAsync(cancellationToken);
        }

        var granted = grantedPermissionIds.ToHashSet();

        return await dbContext.Permissions
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new PermissionOptionDto(
                x.Id,
                x.Code,
                x.Name,
                granted.Contains(x.Id)))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Result<UserDetailsDto>> SaveUserAsync(UpsertUserRequest request, int adminUserId, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageUsers);

        var fullName = request.FullName.Trim();
        var username = request.Username.Trim();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result<UserDetailsDto>.Failure("اكتب اسم المستخدم بالكامل.");
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            return Result<UserDetailsDto>.Failure("اكتب اسم الدخول.");
        }

        var role = await dbContext.Roles.SingleOrDefaultAsync(x => x.Id == request.RoleId, cancellationToken);
        if (role is null)
        {
            return Result<UserDetailsDto>.Failure("اختر دوراً صحيحاً للمستخدم.");
        }

        var usernameExists = await dbContext.Users.AnyAsync(
            x => x.Username == username && x.Id != request.Id,
            cancellationToken);
        if (usernameExists)
        {
            return Result<UserDetailsDto>.Failure("اسم الدخول مستخدم بالفعل.");
        }

        var normalizedPermissionCodes = request.PermissionCodes
            .Where(x => PermissionCodes.All.Contains(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedPermissionCodes.Length == 0)
        {
            return Result<UserDetailsDto>.Failure("اختر صلاحية واحدة على الأقل للمستخدم.");
        }

        var permissionIds = await dbContext.Permissions
            .Where(x => normalizedPermissionCodes.Contains(x.Code))
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);

        var isNew = !request.Id.HasValue;
        User user;

        if (isNew)
        {
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            {
                return Result<UserDetailsDto>.Failure("كلمة المرور يجب ألا تقل عن 6 أحرف.");
            }

            var hash = passwordHasher.HashPassword(request.Password, out var salt);
            user = new User
            {
                FullName = fullName,
                Username = username,
                PasswordHash = hash,
                PasswordSalt = salt,
                RoleId = request.RoleId,
                IsActive = request.IsActive,
                MustChangePassword = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var userId = request.Id.GetValueOrDefault();
            user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
                ?? throw new InvalidOperationException("لم يتم العثور على المستخدم المطلوب.");

            user.FullName = fullName;
            user.Username = username;
            user.RoleId = request.RoleId;
            user.IsActive = request.IsActive;

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                if (request.Password.Length < 6)
                {
                    return Result<UserDetailsDto>.Failure("كلمة المرور يجب ألا تقل عن 6 أحرف.");
                }

                user.PasswordHash = passwordHasher.HashPassword(request.Password, out var salt);
                user.PasswordSalt = salt;
                user.MustChangePassword = true;
            }
        }

        if (!await WouldKeepUserManagementAccessAsync(user.Id, request.IsActive, permissionIds, cancellationToken))
        {
            return Result<UserDetailsDto>.Failure("لا يمكن حفظ هذه الصلاحيات لأنها ستجعل النظام بدون أي مستخدم نشط يستطيع إدارة المستخدمين.");
        }

        await SaveUserPermissionsAsync(user.Id, permissionIds, cancellationToken);

        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            Action = isNew ? AuditAction.CreateUser : AuditAction.EditUser,
            EntityName = nameof(User),
            EntityId = isNew ? null : user.Id.ToString(),
            Description = isNew
                ? $"تم إنشاء المستخدم {fullName}."
                : $"تم تعديل المستخدم {fullName}.",
            CreatedAt = DateTimeOffset.UtcNow
        });

        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            Action = AuditAction.ChangePermissions,
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            Description = $"تم تحديث صلاحيات المستخدم {fullName}.",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        var roleName = await dbContext.Roles
            .Where(x => x.Id == user.RoleId)
            .Select(x => RoleDisplayName(x.Name, x.DisplayName))
            .SingleAsync(cancellationToken);

        return Result<UserDetailsDto>.Success(
            new UserDetailsDto(user.Id, user.FullName, user.Username, user.RoleId, roleName, user.IsActive, user.CreatedAt, user.LastLoginAt),
            isNew ? "تم إنشاء المستخدم بنجاح." : "تم حفظ بيانات المستخدم بنجاح.");
    }

    private async Task SaveUserPermissionsAsync(int userId, IReadOnlyCollection<int> permissionIds, CancellationToken cancellationToken)
    {
        var existing = await dbContext.UserPermissions
            .Where(x => x.UserId == userId)
            .ToArrayAsync(cancellationToken);

        dbContext.UserPermissions.RemoveRange(existing.Where(x => !permissionIds.Contains(x.PermissionId)));

        var existingIds = existing.Select(x => x.PermissionId).ToHashSet();
        foreach (var permissionId in permissionIds.Where(x => !existingIds.Contains(x)))
        {
            dbContext.UserPermissions.Add(new UserPermission
            {
                UserId = userId,
                PermissionId = permissionId
            });
        }
    }

    private async Task<bool> WouldKeepUserManagementAccessAsync(
        int changedUserId,
        bool changedUserIsActive,
        IReadOnlyCollection<int> changedPermissionIds,
        CancellationToken cancellationToken)
    {
        var manageUsersPermissionId = await dbContext.Permissions
            .Where(x => x.Code == PermissionCodes.CanManageUsers)
            .Select(x => x.Id)
            .SingleAsync(cancellationToken);

        if (changedUserIsActive && changedPermissionIds.Contains(manageUsersPermissionId))
        {
            return true;
        }

        return await dbContext.Users
            .AsNoTracking()
            .Where(x => x.IsActive && x.Id != changedUserId)
            .AnyAsync(
                x => x.UserPermissions.Any(up => up.PermissionId == manageUsersPermissionId),
                cancellationToken);
    }

    public async Task<Result> SetUserActiveAsync(int userId, bool isActive, int adminUserId, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageUsers);

        if (userId == adminUserId && !isActive)
        {
            return Result.Failure("لا يمكن إيقاف المستخدم الحالي أثناء تسجيل الدخول به.");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure("لم يتم العثور على المستخدم المطلوب.");
        }

        if (!isActive)
        {
            var currentPermissionIds = await dbContext.UserPermissions
                .Where(x => x.UserId == userId)
                .Select(x => x.PermissionId)
                .ToArrayAsync(cancellationToken);

            if (!await WouldKeepUserManagementAccessAsync(userId, false, currentPermissionIds, cancellationToken))
            {
                return Result.Failure("لا يمكن إيقاف هذا المستخدم لأنه آخر مستخدم نشط يستطيع إدارة المستخدمين.");
            }
        }

        user.IsActive = isActive;
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            Action = AuditAction.EditUser,
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            Description = isActive ? $"تم تفعيل المستخدم {user.FullName}." : $"تم إيقاف المستخدم {user.FullName}.",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(isActive ? "تم تفعيل المستخدم." : "تم إيقاف المستخدم.");
    }

    private static string RoleDisplayName(string name, string displayName) => name switch
    {
        "Admin" => "مدير النظام",
        "Manager" => "مدير الفرع",
        "Cashier" => "كاشير",
        _ => string.IsNullOrWhiteSpace(displayName) ? name : displayName
    };
}

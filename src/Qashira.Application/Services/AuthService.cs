using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Domain.Entities;
using Qashira.Domain.Enums;
using Qashira.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Application.Services;

public sealed class AuthService(
    IApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
    ICurrentUserSession currentUserSession,
    IAuditService auditService) : IAuthService
{
    public async Task<Result<AuthSessionDto>> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var normalizedUsername = username.Trim();
        var user = await dbContext.Users
            .Include(x => x.UserPermissions)
            .ThenInclude(x => x.Permission)
            .SingleOrDefaultAsync(x => x.Username == normalizedUsername, cancellationToken);

        if (user is null || !user.IsActive || !passwordHasher.Verify(password, user.PasswordHash))
        {
            return Result<AuthSessionDto>.Failure("اسم المستخدم أو كلمة المرور غير صحيحة.");
        }

        var permissionCodes = user.UserPermissions
            .Select(x => x.Permission.Code)
            .OrderBy(x => x)
            .ToArray();

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        currentUserSession.SignIn(user.Id, user.Username, user.FullName, permissionCodes);
        await auditService.WriteAsync(
            AuditAction.Login,
            $"تم تسجيل دخول المستخدم {user.FullName}.",
            user.Id,
            nameof(User),
            user.Id.ToString(),
            cancellationToken: cancellationToken);

        return Result<AuthSessionDto>.Success(
            new AuthSessionDto(user.Id, user.FullName, user.Username, permissionCodes, user.MustChangePassword),
            "تم تسجيل الدخول بنجاح.");
    }

    public async Task<Result> ChangeRequiredPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken = default)
    {
        if (currentUserSession.UserId != userId)
        {
            return Result.Failure("يجب تسجيل الدخول أولاً.");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken);
        if (user is null)
        {
            return Result.Failure("لم يتم العثور على المستخدم.");
        }

        var validationMessage = ValidateStrongPassword(newPassword, user.Username);
        if (validationMessage is not null)
        {
            return Result.Failure(validationMessage);
        }

        user.PasswordHash = passwordHasher.HashPassword(newPassword, out var salt);
        user.PasswordSalt = salt;
        user.MustChangePassword = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            AuditAction.SettingsChanged,
            $"تم تغيير كلمة مرور المستخدم {user.FullName} بعد تسجيل الدخول الأول.",
            user.Id,
            nameof(User),
            user.Id.ToString(),
            cancellationToken: cancellationToken);

        return Result.Success("تم تغيير كلمة المرور بنجاح.");
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUserSession.UserId;
        var fullName = currentUserSession.FullName;
        currentUserSession.SignOut();

        if (userId.HasValue)
        {
            await auditService.WriteAsync(
                AuditAction.Logout,
                $"تم تسجيل خروج المستخدم {fullName}.",
                userId,
                nameof(User),
                userId.Value.ToString(),
                cancellationToken: cancellationToken);
        }
    }

    private static string? ValidateStrongPassword(string password, string username)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return "كلمة المرور يجب أن تكون 8 أحرف على الأقل.";
        }

        if (password.Equals("Admin@123", StringComparison.OrdinalIgnoreCase))
        {
            return "لا يمكن استخدام كلمة المرور الافتراضية مرة أخرى.";
        }

        if (password.Contains(username, StringComparison.OrdinalIgnoreCase))
        {
            return "كلمة المرور لا يجب أن تحتوي على اسم الدخول.";
        }

        if (!password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            !password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            return "كلمة المرور يجب أن تحتوي على حرف كبير وحرف صغير ورقم ورمز.";
        }

        return null;
    }
}

using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Application.Permissions;
using Qashira.Domain.Entities;
using Qashira.Domain.Enums;
using Qashira.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Application.Services;

public sealed class SystemSettingsService(
    IApplicationDbContext dbContext,
    IPermissionService permissionService) : ISystemSettingsService
{
    public async Task<SystemSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await dbContext.AppSettings
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Key, x => x.Value, cancellationToken);

        return new SystemSettingsDto(
            settings.GetValueOrDefault("StoreName", "مكتبة"),
            settings.GetValueOrDefault("Currency", "ج.م"),
            ParseInt(settings.GetValueOrDefault("DefaultLowStockThreshold", "3"), 3),
            ParseBool(settings.GetValueOrDefault("AllowNegativeStock", "false")),
            ParseBool(settings.GetValueOrDefault("DiscountsEnabled", "true")));
    }

    public async Task<Result> SaveSettingsAsync(SystemSettingsDto settings, int userId, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageSettings);

        var storeName = settings.StoreName.Trim();
        var currency = settings.Currency.Trim();

        if (string.IsNullOrWhiteSpace(storeName))
        {
            return Result.Failure("اسم المكتبة مطلوب.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            return Result.Failure("اختصار العملة مطلوب.");
        }

        if (settings.DefaultLowStockThreshold < 0)
        {
            return Result.Failure("حد تنبيه المخزون لا يمكن أن يكون أقل من صفر.");
        }

        await UpsertSettingAsync("StoreName", storeName, cancellationToken);
        await UpsertSettingAsync("Currency", currency, cancellationToken);
        await UpsertSettingAsync("DefaultLowStockThreshold", settings.DefaultLowStockThreshold.ToString(), cancellationToken);
        await UpsertSettingAsync("AllowNegativeStock", settings.AllowNegativeStock ? "true" : "false", cancellationToken);
        await UpsertSettingAsync("DiscountsEnabled", settings.DiscountsEnabled ? "true" : "false", cancellationToken);

        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = AuditAction.SettingsChanged,
            EntityName = nameof(AppSetting),
            EntityId = "SystemSettings",
            Description = $"تم تغيير إعدادات النظام العامة. المكتبة: {storeName}.",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success("تم حفظ إعدادات النظام بنجاح.");
    }

    private async Task UpsertSettingAsync(string key, string value, CancellationToken cancellationToken)
    {
        var setting = await dbContext.AppSettings.SingleOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (setting is null)
        {
            dbContext.AppSettings.Add(new AppSetting { Key = key, Value = value });
            return;
        }

        setting.Value = value;
    }

    private static bool ParseBool(string? value)
    {
        return bool.TryParse(value, out var result) && result;
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, out var result) ? result : fallback;
    }
}

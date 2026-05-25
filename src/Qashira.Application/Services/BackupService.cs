using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Application.Permissions;
using Qashira.Domain.Enums;
using Qashira.Shared.Results;
using Microsoft.Extensions.Logging;

namespace Qashira.Application.Services;

public sealed class BackupService(
    IBackupStorage backupStorage,
    IPermissionService permissionService,
    IAuditService auditService,
    ILogger<BackupService> logger) : IBackupService
{
    public Task<IReadOnlyList<BackupFileDto>> GetBackupsAsync(CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanBackupRestore);
        return backupStorage.GetBackupsAsync(cancellationToken);
    }

    public async Task<Result<BackupOperationDto>> CreateBackupAsync(int userId, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanBackupRestore);

        try
        {
            var backupPath = await backupStorage.CreateBackupAsync(cancellationToken);
            await WriteBackupAuditAsync(
                AuditAction.BackupCreated,
                $"تم إنشاء نسخة احتياطية: {Path.GetFileName(backupPath)}",
                userId,
                backupPath,
                cancellationToken);

            return Result<BackupOperationDto>.Success(
                new BackupOperationDto(backupPath),
                "تم إنشاء النسخة الاحتياطية بنجاح.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Manual backup failed.");
            return Result<BackupOperationDto>.Failure("تعذر إنشاء النسخة الاحتياطية. تم تسجيل التفاصيل في ملف السجل.");
        }
    }

    public async Task<Result<BackupOperationDto>> ImportBackupAsync(string sourceBackupPath, int userId, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanBackupRestore);

        if (string.IsNullOrWhiteSpace(sourceBackupPath) || !File.Exists(sourceBackupPath))
        {
            return Result<BackupOperationDto>.Failure("اختر ملف نسخة احتياطية صحيح أولاً.");
        }

        try
        {
            var isValid = await backupStorage.ValidateBackupAsync(sourceBackupPath, cancellationToken);
            if (!isValid)
            {
                return Result<BackupOperationDto>.Failure("ملف النسخة الاحتياطية غير صالح ولا يمكن استيراده.");
            }

            var importedPath = await backupStorage.ImportBackupAsync(sourceBackupPath, cancellationToken);
            await WriteBackupAuditAsync(
                AuditAction.BackupImported,
                $"تم استيراد نسخة احتياطية: {Path.GetFileName(importedPath)}",
                userId,
                importedPath,
                cancellationToken);

            return Result<BackupOperationDto>.Success(
                new BackupOperationDto(importedPath),
                "تم استيراد النسخة الاحتياطية بنجاح. يمكنك اختيارها من القائمة ثم استرجاعها.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Backup import failed for {SourceBackupPath}.", sourceBackupPath);
            return Result<BackupOperationDto>.Failure("تعذر استيراد النسخة الاحتياطية. تم تسجيل التفاصيل في ملف السجل.");
        }
    }

    public async Task<Result<BackupOperationDto>> RestoreBackupAsync(string backupPath, int userId, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanBackupRestore);

        if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
        {
            return Result<BackupOperationDto>.Failure("اختر نسخة احتياطية صحيحة أولاً.");
        }

        try
        {
            var isValid = await backupStorage.ValidateBackupAsync(backupPath, cancellationToken);
            if (!isValid)
            {
                return Result<BackupOperationDto>.Failure("ملف النسخة الاحتياطية غير صالح ولا يمكن استرجاعه.");
            }

            var safetyBackupPath = await backupStorage.CreateSafetyBackupAsync(cancellationToken);
            await backupStorage.RestoreBackupAsync(backupPath, cancellationToken);

            await WriteBackupAuditAsync(
                AuditAction.RestorePerformed,
                $"تم استرجاع نسخة احتياطية: {Path.GetFileName(backupPath)}. نسخة الأمان: {Path.GetFileName(safetyBackupPath)}",
                userId,
                backupPath,
                cancellationToken);

            return Result<BackupOperationDto>.Success(
                new BackupOperationDto(backupPath, safetyBackupPath),
                "تم استرجاع النسخة الاحتياطية بنجاح. أغلق البرنامج وافتحه مرة أخرى لتحميل البيانات المسترجعة.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Backup restore failed for {BackupPath}.", backupPath);
            return Result<BackupOperationDto>.Failure("تعذر استرجاع النسخة الاحتياطية. تم تسجيل التفاصيل في ملف السجل.");
        }
    }

    public async Task<Result<BackupOperationDto>> ExportBackupAsync(string backupPath, string exportPath, int userId, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanBackupRestore);

        if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
        {
            return Result<BackupOperationDto>.Failure("اختر نسخة احتياطية صحيحة أولاً.");
        }

        if (string.IsNullOrWhiteSpace(exportPath))
        {
            return Result<BackupOperationDto>.Failure("اختر مكان حفظ النسخة الاحتياطية أولاً.");
        }

        try
        {
            var isValid = await backupStorage.ValidateBackupAsync(backupPath, cancellationToken);
            if (!isValid)
            {
                return Result<BackupOperationDto>.Failure("ملف النسخة الاحتياطية غير صالح ولا يمكن تصديره.");
            }

            var exportedPath = await backupStorage.ExportBackupAsync(backupPath, exportPath, cancellationToken);
            await WriteBackupAuditAsync(
                AuditAction.BackupExported,
                $"تم تصدير نسخة احتياطية: {Path.GetFileName(exportedPath)}",
                userId,
                exportedPath,
                cancellationToken);

            return Result<BackupOperationDto>.Success(
                new BackupOperationDto(backupPath, ExportPath: exportedPath),
                $"تم تصدير النسخة الاحتياطية بنجاح: {exportedPath}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Backup export failed for {BackupPath}.", backupPath);
            return Result<BackupOperationDto>.Failure("تعذر تصدير النسخة الاحتياطية. تم تسجيل التفاصيل في ملف السجل.");
        }
    }

    public async Task<Result> DeleteBackupAsync(string backupPath, int userId, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanBackupRestore);

        if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
        {
            return Result.Failure("اختر نسخة احتياطية صحيحة أولاً.");
        }

        try
        {
            var fileName = Path.GetFileName(backupPath);
            await backupStorage.DeleteBackupAsync(backupPath, cancellationToken);
            await WriteBackupAuditAsync(
                AuditAction.BackupDeleted,
                $"تم حذف نسخة احتياطية: {fileName}",
                userId,
                backupPath,
                cancellationToken);

            return Result.Success("تم حذف النسخة الاحتياطية المحددة.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Backup delete failed for {BackupPath}.", backupPath);
            return Result.Failure("تعذر حذف النسخة الاحتياطية. تم تسجيل التفاصيل في ملف السجل.");
        }
    }

    private async Task WriteBackupAuditAsync(
        AuditAction action,
        string description,
        int userId,
        string backupPath,
        CancellationToken cancellationToken)
    {
        try
        {
            await auditService.WriteAsync(
                action,
                description,
                userId,
                "Backup",
                backupPath,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Backup audit write failed for {BackupPath}.", backupPath);
        }
    }
}

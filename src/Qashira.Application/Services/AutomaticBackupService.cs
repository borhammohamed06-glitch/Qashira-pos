using Qashira.Application.Abstractions;
using Qashira.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Qashira.Application.Services;

public sealed class AutomaticBackupService(
    IBackupStorage backupStorage,
    ILogArchiveStorage logArchiveStorage,
    IAuditService auditService,
    ILogger<AutomaticBackupService> logger) : IAutomaticBackupService
{
    private const string AutomaticBackupPrefix = "qashira-auto-";

    public async Task RunStartupBackupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await backupStorage.CleanupOldManagedBackupsAsync(cancellationToken);
            await logArchiveStorage.CleanupOldExportsAsync(cancellationToken);

            var today = DateTimeOffset.Now.Date;
            var backups = await backupStorage.GetBackupsAsync(cancellationToken);
            var hasTodayAutomaticBackup = backups.Any(x =>
                x.FileName.StartsWith(AutomaticBackupPrefix, StringComparison.OrdinalIgnoreCase) &&
                x.CreatedAt.LocalDateTime.Date == today);

            if (hasTodayAutomaticBackup)
            {
                return;
            }

            var backupPath = await backupStorage.CreateAutomaticBackupAsync(cancellationToken);
            await auditService.WriteAsync(
                AuditAction.BackupCreated,
                $"تم إنشاء نسخة احتياطية تلقائية: {Path.GetFileName(backupPath)}",
                entityName: "Backup",
                entityId: backupPath,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Automatic startup backup failed.");
        }
    }
}

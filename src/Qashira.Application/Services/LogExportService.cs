using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Application.Permissions;
using Qashira.Domain.Enums;
using Qashira.Shared.Results;
using Microsoft.Extensions.Logging;

namespace Qashira.Application.Services;

public sealed class LogExportService(
    ILogArchiveStorage logArchiveStorage,
    IPermissionService permissionService,
    IAuditService auditService,
    ILogger<LogExportService> logger) : ILogExportService
{
    public async Task<Result<LogExportOperationDto>> ExportLogsAsync(int userId, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanBackupRestore);

        try
        {
            var export = await logArchiveStorage.ExportLogsAsync(cancellationToken);
            if (export.FileCount == 0)
            {
                return Result<LogExportOperationDto>.Failure("لا توجد ملفات سجل لتصديرها.");
            }

            await auditService.WriteAsync(
                AuditAction.LogsExported,
                $"تم تصدير ملفات السجل: {Path.GetFileName(export.ExportPath)}",
                userId,
                "LogExport",
                export.ExportPath,
                cancellationToken: cancellationToken);

            return Result<LogExportOperationDto>.Success(
                export,
                $"تم تصدير ملفات السجل بنجاح: {export.ExportPath}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Log export failed.");
            return Result<LogExportOperationDto>.Failure("تعذر تصدير ملفات السجل. تم تسجيل التفاصيل في ملف السجل.");
        }
    }
}

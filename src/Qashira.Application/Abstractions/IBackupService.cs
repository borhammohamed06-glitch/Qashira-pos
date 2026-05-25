using Qashira.Application.DTOs;
using Qashira.Shared.Results;

namespace Qashira.Application.Abstractions;

public interface IBackupService
{
    Task<IReadOnlyList<BackupFileDto>> GetBackupsAsync(CancellationToken cancellationToken = default);
    Task<Result<BackupOperationDto>> CreateBackupAsync(int userId, CancellationToken cancellationToken = default);
    Task<Result<BackupOperationDto>> ImportBackupAsync(string sourceBackupPath, int userId, CancellationToken cancellationToken = default);
    Task<Result<BackupOperationDto>> RestoreBackupAsync(string backupPath, int userId, CancellationToken cancellationToken = default);
    Task<Result<BackupOperationDto>> ExportBackupAsync(string backupPath, string exportPath, int userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteBackupAsync(string backupPath, int userId, CancellationToken cancellationToken = default);
}

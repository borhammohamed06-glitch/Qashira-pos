using Qashira.Application.DTOs;

namespace Qashira.Application.Abstractions;

public interface IBackupStorage
{
    Task<IReadOnlyList<BackupFileDto>> GetBackupsAsync(CancellationToken cancellationToken = default);
    Task<string> CreateBackupAsync(CancellationToken cancellationToken = default);
    Task<string> CreateAutomaticBackupAsync(CancellationToken cancellationToken = default);
    Task<string> ImportBackupAsync(string sourceBackupPath, CancellationToken cancellationToken = default);
    Task<bool> ValidateBackupAsync(string backupPath, CancellationToken cancellationToken = default);
    Task<string> CreateSafetyBackupAsync(CancellationToken cancellationToken = default);
    Task<string> CreateImportSafetyBackupAsync(CancellationToken cancellationToken = default);
    Task RestoreBackupAsync(string backupPath, CancellationToken cancellationToken = default);
    Task<string> ExportBackupAsync(string backupPath, string exportPath, CancellationToken cancellationToken = default);
    Task DeleteBackupAsync(string backupPath, CancellationToken cancellationToken = default);
    Task<int> CleanupOldManagedBackupsAsync(CancellationToken cancellationToken = default);
}

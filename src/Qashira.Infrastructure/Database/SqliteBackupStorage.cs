using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Shared.Constants;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Infrastructure.Database;

public sealed class SqliteBackupStorage(QashiraDbContext dbContext) : IBackupStorage
{
    private const string BackupExtension = ".db";
    private const int KeepAutomaticBackupDays = 30;
    private const int KeepSafetyBackupDays = 14;
    private static readonly string[] RequiredTables =
    [
        "__EFMigrationsHistory",
        "AppSettings",
        "AuditLogs",
        "Categories",
        "ErrorLogs",
        "InvoiceItems",
        "Invoices",
        "Notifications",
        "Payments",
        "Permissions",
        "PrintingMaterialConsumptions",
        "PrintingServiceTemplates",
        "Products",
        "PrintOrders",
        "ReturnItems",
        "Returns",
        "RolePermissions",
        "Roles",
        "Shifts",
        "StockMovements",
        "SuspendedInvoiceItems",
        "SuspendedInvoices",
        "UserPermissions",
        "Users"
    ];

    public Task<IReadOnlyList<BackupFileDto>> GetBackupsAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppPaths.BackupsPath);

        var backups = Directory
            .EnumerateFiles(AppPaths.BackupsPath, $"qashira-*{BackupExtension}")
            .Select(path =>
            {
                var file = new FileInfo(path);
                return new BackupFileDto(file.Name, file.FullName, file.Length, file.CreationTime);
            })
            .OrderByDescending(x => x.CreatedAt)
            .ToArray();

        return Task.FromResult<IReadOnlyList<BackupFileDto>>(backups);
    }

    public async Task<string> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppPaths.BackupsPath);
        var backupPath = Path.Combine(AppPaths.BackupsPath, $"qashira-backup-{DateTime.Now:yyyyMMdd-HHmmss}{BackupExtension}");
        await CreateSqliteSnapshotAsync(backupPath, cancellationToken);
        return backupPath;
    }

    public async Task<string> CreateAutomaticBackupAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppPaths.BackupsPath);
        var backupPath = Path.Combine(AppPaths.BackupsPath, $"qashira-auto-{DateTime.Now:yyyyMMdd-HHmmss}{BackupExtension}");
        await CreateSqliteSnapshotAsync(backupPath, cancellationToken);
        return backupPath;
    }

    public async Task<bool> ValidateBackupAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(backupPath) ||
                !File.Exists(backupPath) ||
                new FileInfo(backupPath).Length == 0)
            {
                return false;
            }

            await using var connection = new SqliteConnection($"Data Source={backupPath};Mode=ReadOnly");
            await connection.OpenAsync(cancellationToken);

            await using (var integrityCommand = connection.CreateCommand())
            {
                integrityCommand.CommandText = "PRAGMA integrity_check;";
                var integrityResult = Convert.ToString(await integrityCommand.ExecuteScalarAsync(cancellationToken));
                if (!string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            await using var command = connection.CreateCommand();
            var tableParameters = RequiredTables
                .Select((_, index) => $"$table{index}")
                .ToArray();
            command.CommandText = $"""
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table'
                  AND name IN ({string.Join(", ", tableParameters)});
                """;

            for (var index = 0; index < RequiredTables.Length; index++)
            {
                command.Parameters.AddWithValue(tableParameters[index], RequiredTables[index]);
            }

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result) == RequiredTables.Length;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> CreateSafetyBackupAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppPaths.BackupsPath);
        var safetyBackupPath = Path.Combine(AppPaths.BackupsPath, $"qashira-before-restore-{DateTime.Now:yyyyMMdd-HHmmss}{BackupExtension}");
        await CreateSqliteSnapshotAsync(safetyBackupPath, cancellationToken);
        return safetyBackupPath;
    }

    public async Task<string> CreateImportSafetyBackupAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppPaths.BackupsPath);
        var safetyBackupPath = Path.Combine(AppPaths.BackupsPath, $"qashira-before-import-{DateTime.Now:yyyyMMdd-HHmmss}{BackupExtension}");
        await CreateSqliteSnapshotAsync(safetyBackupPath, cancellationToken);
        return safetyBackupPath;
    }

    public async Task RestoreBackupAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        var tempRestorePath = $"{AppPaths.DatabasePath}.restore-tmp";
        if (File.Exists(tempRestorePath))
        {
            File.Delete(tempRestorePath);
        }

        await using (var source = new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        await using (var destination = new FileStream(tempRestorePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await source.CopyToAsync(destination, cancellationToken);
        }

        if (!await ValidateBackupAsync(tempRestorePath, cancellationToken))
        {
            File.Delete(tempRestorePath);
            throw new InvalidOperationException("The restored SQLite file did not pass validation.");
        }

        dbContext.Database.CloseConnection();
        SqliteConnection.ClearAllPools();

        File.Move(tempRestorePath, AppPaths.DatabasePath, overwrite: true);

        SqliteConnection.ClearAllPools();
    }

    public async Task<string> ImportBackupAsync(string sourceBackupPath, CancellationToken cancellationToken = default)
    {
        if (!await ValidateBackupAsync(sourceBackupPath, cancellationToken))
        {
            throw new InvalidOperationException("Imported backup did not pass validation.");
        }

        Directory.CreateDirectory(AppPaths.BackupsPath);
        var destinationPath = Path.Combine(AppPaths.BackupsPath, $"qashira-imported-{DateTime.Now:yyyyMMdd-HHmmss}{BackupExtension}");
        await CopyFileAsync(sourceBackupPath, destinationPath, cancellationToken);

        if (!await ValidateBackupAsync(destinationPath, cancellationToken))
        {
            File.Delete(destinationPath);
            throw new InvalidOperationException("Copied imported backup did not pass validation.");
        }

        return destinationPath;
    }

    public async Task<string> ExportBackupAsync(string backupPath, string exportPath, CancellationToken cancellationToken = default)
    {
        if (!await ValidateBackupAsync(backupPath, cancellationToken))
        {
            throw new InvalidOperationException("Exported backup did not pass validation.");
        }

        var fullSource = Path.GetFullPath(backupPath);
        var fullDestination = Path.GetFullPath(exportPath);
        if (string.Equals(fullSource, fullDestination, StringComparison.OrdinalIgnoreCase))
        {
            return fullDestination;
        }

        var destinationDirectory = Path.GetDirectoryName(fullDestination);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        await CopyFileAsync(fullSource, fullDestination, cancellationToken);
        return fullDestination;
    }

    public Task DeleteBackupAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(backupPath);
        if (!IsManagedBackupPath(fullPath))
        {
            throw new InvalidOperationException("Only managed backup files can be deleted.");
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public Task<int> CleanupOldManagedBackupsAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppPaths.BackupsPath);

        var now = DateTimeOffset.Now;
        var deleted = 0;
        foreach (var path in Directory.EnumerateFiles(AppPaths.BackupsPath, $"qashira-*{BackupExtension}"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = new FileInfo(path);
            var age = now - file.CreationTime;
            var shouldDelete =
                file.Name.StartsWith("qashira-auto-", StringComparison.OrdinalIgnoreCase) && age.TotalDays > KeepAutomaticBackupDays ||
                file.Name.StartsWith("qashira-before-restore-", StringComparison.OrdinalIgnoreCase) && age.TotalDays > KeepSafetyBackupDays ||
                file.Name.StartsWith("qashira-before-import-", StringComparison.OrdinalIgnoreCase) && age.TotalDays > KeepSafetyBackupDays;

            if (!shouldDelete)
            {
                continue;
            }

            try
            {
                file.Delete();
                deleted++;
            }
            catch
            {
                // The next maintenance run can retry locked files.
            }
        }

        return Task.FromResult(deleted);
    }

    private static async Task CreateSqliteSnapshotAsync(string backupPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(AppPaths.DatabasePath))
        {
            throw new FileNotFoundException("SQLite database file was not found.", AppPaths.DatabasePath);
        }

        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }

        await using var connection = new SqliteConnection($"Data Source={AppPaths.DatabasePath}");
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "VACUUM INTO $backupPath;";
        command.Parameters.AddWithValue("$backupPath", backupPath);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(destination, cancellationToken);
    }

    private static bool IsManagedBackupPath(string backupPath)
    {
        var backupRoot = Path.GetFullPath(AppPaths.BackupsPath);
        var fullPath = Path.GetFullPath(backupPath);
        return fullPath.StartsWith(backupRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(fullPath).StartsWith("qashira-", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Path.GetExtension(fullPath), BackupExtension, StringComparison.OrdinalIgnoreCase);
    }
}

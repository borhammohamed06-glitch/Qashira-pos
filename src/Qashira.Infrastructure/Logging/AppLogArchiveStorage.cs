using System.IO.Compression;
using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Shared.Constants;

namespace Qashira.Infrastructure.Logging;

public sealed class AppLogArchiveStorage : ILogArchiveStorage
{
    private const int KeepExportDays = 14;

    public async Task<LogExportOperationDto> ExportLogsAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppPaths.LogsPath);
        Directory.CreateDirectory(AppPaths.LogExportsPath);
        await CleanupOldExportsAsync(cancellationToken);

        var logFiles = Directory
            .EnumerateFiles(AppPaths.LogsPath, "*.txt", SearchOption.TopDirectoryOnly)
            .Where(File.Exists)
            .OrderBy(Path.GetFileName)
            .ToArray();

        if (logFiles.Length == 0)
        {
            return new LogExportOperationDto(string.Empty, 0);
        }

        var exportPath = Path.Combine(AppPaths.LogExportsPath, $"qashira-logs-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        if (File.Exists(exportPath))
        {
            File.Delete(exportPath);
        }

        using var archive = ZipFile.Open(exportPath, ZipArchiveMode.Create);
        foreach (var logFile in logFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = archive.CreateEntry(Path.GetFileName(logFile), CompressionLevel.Optimal);
            await using var source = new FileStream(
                logFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            await using var destination = entry.Open();
            await source.CopyToAsync(destination, cancellationToken);
        }

        return new LogExportOperationDto(exportPath, logFiles.Length);
    }

    public Task<int> CleanupOldExportsAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppPaths.LogExportsPath);

        var cutoff = DateTime.Now.AddDays(-KeepExportDays);
        var deleted = 0;
        foreach (var path in Directory.EnumerateFiles(AppPaths.LogExportsPath, "qashira-logs-*.zip"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = new FileInfo(path);
            if (file.CreationTime >= cutoff)
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
                // A locked support package can be cleaned on the next startup/export.
            }
        }

        return Task.FromResult(deleted);
    }
}
